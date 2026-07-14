using System.Buffers.Binary;
using System.Text;
using ImageArchive.Abstractions;
using ImageArchive.Geometry;
using SkiaSharp;

namespace ImageArchive.Codecs.Skia;

/// <summary>
/// Animated WebP (RIFF/WEBP) with ANIM + ANMF. Each frame is SkiaSharp-encoded WebP bitstream.
/// Metadata stored in a custom "META" chunk (length-prefixed pairs) after VP8X for round-trip of RFC text fields.
/// </summary>
internal static class WebpAnimContainer
{
    public static void Write(Stream output, IReadOnlyList<FrameBitmap> frames, ArchiveMetadata metadata, int delayMs)
    {
        if (frames.Count == 0) throw new ImageArchiveException("No frames.");

        using var body = new MemoryStream();
        // VP8X: animation flag (bit 1), ICC etc. flags; canvas size
        // https://developers.google.com/speed/webp/docs/riff_container
        var vp8x = new byte[10];
        vp8x[0] = 0x02; // animation
        // canvas width-1, height-1 as 24-bit LE
        Write24(vp8x.AsSpan(4), FrameGeometry.Width - 1);
        Write24(vp8x.AsSpan(7), FrameGeometry.Height - 1);
        WriteChunk(body, "VP8X", vp8x);

        // ANIM: bgcolor + loop
        var anim = new byte[6];
        // bgcolor ARGB = white opaque
        anim[0] = 255; anim[1] = 255; anim[2] = 255; anim[3] = 255;
        BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(4), 0); // loop forever
        WriteChunk(body, "ANIM", anim);

        // META custom chunk with delay + metadata (FourCC 'META')
        using (var metaMs = new MemoryStream())
        using (var bw = new BinaryWriter(metaMs, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(delayMs);
            var pairs = MetaPairs(metadata);
            bw.Write(pairs.Count);
            foreach (var (k, v) in pairs)
            {
                var kb = Encoding.UTF8.GetBytes(k);
                var vb = Encoding.UTF8.GetBytes(v);
                bw.Write(kb.Length); bw.Write(kb);
                bw.Write(vb.Length); bw.Write(vb);
            }
            WriteChunk(body, "META", metaMs.ToArray());
        }

        foreach (var frame in frames)
        {
            var rgb = SkiaRaster.ThroughSkia(frame);
            using var sk = new SKBitmap(FrameGeometry.Width, FrameGeometry.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            var dest = sk.GetPixelSpan();
            for (var i = 0; i < FrameGeometry.Width * FrameGeometry.Height; i++)
            {
                dest[i * 4] = rgb[i * 3];
                dest[i * 4 + 1] = rgb[i * 3 + 1];
                dest[i * 4 + 2] = rgb[i * 3 + 2];
                dest[i * 4 + 3] = 255;
            }
            using var webpData = sk.Encode(SKEncodedImageFormat.Webp, 100)
                ?? throw new ImageArchiveException("Skia WebP encode failed.");
            var still = webpData.ToArray();
            // Strip RIFF header if present; ANMF wants frame data
            var bitstream = ExtractWebpBitstream(still);

            // ANMF: 16-byte header + bitstream
            var anmf = new byte[16 + bitstream.Length];
            // x,y = 0 (24-bit)
            Write24(anmf.AsSpan(0), 0);
            Write24(anmf.AsSpan(3), 0);
            Write24(anmf.AsSpan(6), FrameGeometry.Width - 1);
            Write24(anmf.AsSpan(9), FrameGeometry.Height - 1);
            Write24(anmf.AsSpan(12), (uint)Math.Min(delayMs, 0xFFFFFF));
            anmf[15] = 0; // flags: no blend, dispose none
            bitstream.CopyTo(anmf, 16);
            WriteChunk(body, "ANMF", anmf);
        }

        var payload = body.ToArray();
        // RIFF header
        output.Write(Encoding.ASCII.GetBytes("RIFF"));
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)(4 + payload.Length)); // WEBP + payload
        output.Write(size);
        output.Write(Encoding.ASCII.GetBytes("WEBP"));
        output.Write(payload);
    }

    public static DecodeContainerResult Read(Stream input)
    {
        using var br = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        var riff = Encoding.ASCII.GetString(br.ReadBytes(4));
        if (riff != "RIFF") throw new ImageArchiveException("Not a RIFF/WebP file.");
        br.ReadUInt32(); // size
        var webp = Encoding.ASCII.GetString(br.ReadBytes(4));
        if (webp != "WEBP") throw new ImageArchiveException("Not a WEBP file.");

        int delay = FrameGeometry.AnimationDelayMilliseconds;
        var meta = new ArchiveMetadata();
        var frames = new List<FrameBitmap>();

        while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
        {
            var fourcc = Encoding.ASCII.GetString(br.ReadBytes(4));
            var len = br.ReadUInt32();
            var data = br.ReadBytes((int)len);
            if ((len & 1) != 0 && br.BaseStream.Position < br.BaseStream.Length)
                br.ReadByte(); // pad

            if (fourcc == "META")
            {
                using var ms = new MemoryStream(data);
                using var r = new BinaryReader(ms);
                delay = r.ReadInt32();
                var count = r.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var k = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
                    var v = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
                    ApplyMeta(meta, k, v);
                }
            }
            else if (fourcc == "ANMF" && data.Length > 16)
            {
                // duration in bytes 12-14
                var dur = data[12] | (data[13] << 8) | (data[14] << 16);
                if (dur > 0) delay = dur;
                var bitstream = data.AsSpan(16).ToArray();
                // rebuild minimal RIFF WEBP for Skia decode
                var still = WrapAsWebp(bitstream);
                using var sk = SKBitmap.Decode(still)
                    ?? throw new ImageArchiveException("Skia failed to decode WebP frame.");
                frames.Add(new FrameBitmap
                {
                    Width = FrameGeometry.Width,
                    Height = FrameGeometry.Height,
                    Format = PixelFormat.Rgb24,
                    Pixels = RgbFromSk(sk)
                });
            }
        }

        if (frames.Count == 0)
            throw new ImageArchiveException("Animated WebP contained no ANMF frames.");

        return new DecodeContainerResult
        {
            Frames = frames,
            Metadata = meta,
            DelayMilliseconds = delay
        };
    }

    private static byte[] ExtractWebpBitstream(byte[] riffWebp)
    {
        // If already RIFF, return payload after WEBP fourcc chunks as-is for embedding
        if (riffWebp.Length > 12 && Encoding.ASCII.GetString(riffWebp, 0, 4) == "RIFF")
            return riffWebp.AsSpan(12).ToArray(); // from first chunk
        return riffWebp;
    }

    private static byte[] WrapAsWebp(byte[] chunksOrRiff)
    {
        if (chunksOrRiff.Length > 12 && Encoding.ASCII.GetString(chunksOrRiff, 0, 4) == "RIFF")
            return chunksOrRiff;
        // chunksOrRiff is chunk list starting at VP8X/VP8L etc.
        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("RIFF"));
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)(4 + chunksOrRiff.Length));
        ms.Write(size);
        ms.Write(Encoding.ASCII.GetBytes("WEBP"));
        ms.Write(chunksOrRiff);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string fourcc, byte[] data)
    {
        s.Write(Encoding.ASCII.GetBytes(fourcc));
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(len, (uint)data.Length);
        s.Write(len);
        s.Write(data);
        if ((data.Length & 1) != 0) s.WriteByte(0);
    }

    private static void Write24(Span<byte> dest, int value)
    {
        dest[0] = (byte)(value & 0xFF);
        dest[1] = (byte)((value >> 8) & 0xFF);
        dest[2] = (byte)((value >> 16) & 0xFF);
    }

    private static void Write24(Span<byte> dest, uint value) => Write24(dest, (int)value);

    private static byte[] RgbFromSk(SKBitmap bmp)
    {
        var rgb = new byte[FrameGeometry.Width * FrameGeometry.Height * 3];
        for (var y = 0; y < FrameGeometry.Height && y < bmp.Height; y++)
        for (var x = 0; x < FrameGeometry.Width && x < bmp.Width; x++)
        {
            var c = bmp.GetPixel(x, y);
            var i = (y * FrameGeometry.Width + x) * 3;
            rgb[i] = c.Red; rgb[i + 1] = c.Green; rgb[i + 2] = c.Blue;
        }
        return rgb;
    }

    private static List<(string, string)> MetaPairs(ArchiveMetadata m)
    {
        var list = new List<(string, string)>
        {
            ("encoderName", m.EncoderName),
            ("encoderVersion", m.EncoderVersion),
            ("encoderSha256", m.EncoderSha256),
            ("mimeType", m.MimeType),
            ("archiveType", m.ArchiveType.ToString().ToLowerInvariant()),
            ("jsonSchema", m.JsonSchema),
            ("jsonManifest", m.JsonManifest)
        };
        if (m.SourceUrl != null) list.Add(("sourceUrl", m.SourceUrl));
        if (m.CommitHash != null) list.Add(("commitHash", m.CommitHash));
        if (m.OriginalFileName != null) list.Add(("originalFileName", m.OriginalFileName));
        foreach (var kv in m.AdditionalTextChunks) list.Add((kv.Key, kv.Value));
        return list;
    }

    private static void ApplyMeta(ArchiveMetadata m, string k, string v)
    {
        switch (k)
        {
            case "encoderName": m.EncoderName = v; break;
            case "encoderVersion": m.EncoderVersion = v; break;
            case "encoderSha256": m.EncoderSha256 = v; break;
            case "mimeType": m.MimeType = v; break;
            case "archiveType":
                m.ArchiveType = Enum.TryParse<ArchiveType>(v, true, out var at) ? at : ArchiveType.Raw;
                break;
            case "jsonSchema": m.JsonSchema = v; break;
            case "jsonManifest": m.JsonManifest = v; break;
            case "sourceUrl": m.SourceUrl = v; break;
            case "commitHash": m.CommitHash = v; break;
            case "originalFileName": m.OriginalFileName = v; break;
            default: m.AdditionalTextChunks[k] = v; break;
        }
    }
}
