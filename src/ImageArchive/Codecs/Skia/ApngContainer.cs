using System.Buffers.Binary;
using System.Text;
using ImageArchive.Abstractions;
using ImageArchive.Geometry;
using SkiaSharp;

namespace ImageArchive.Codecs.Skia;

/// <summary>Real APNG writer/reader. Frame pixels go through SkiaSharp PNG encode/decode.</summary>
internal static class ApngContainer
{
    public static void Write(Stream output, IReadOnlyList<FrameBitmap> frames, ArchiveMetadata metadata, int delayMs)
    {
        if (frames.Count == 0) throw new ImageArchiveException("No frames.");
        output.Write(PngChunkIo.Signature);

        // IHDR
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, FrameGeometry.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], FrameGeometry.Height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // truecolor RGB
        ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        PngChunkIo.WriteChunk(output, "IHDR", ihdr);

        // acTL
        Span<byte> actl = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(actl, (uint)frames.Count);
        BinaryPrimitives.WriteUInt32BigEndian(actl[4..], 0); // infinite loops
        PngChunkIo.WriteChunk(output, "acTL", actl);

        // delay as tEXt + metadata tEXt
        PngChunkIo.WriteChunk(output, "tEXt", PngChunkIo.BuildTextChunkPayload("frameDelayMs", delayMs.ToString()));
        foreach (var (k, v) in MetadataPairs(metadata))
            PngChunkIo.WriteChunk(output, "tEXt", PngChunkIo.BuildTextChunkPayload(k, v));

        uint seq = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            // Skia round-trip for raster
            var rgb = SkiaRaster.ThroughSkia(frame);

            // fcTL
            var fctl = new byte[26];
            BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(0), seq++);
            BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(4), (uint)FrameGeometry.Width);
            BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(8), (uint)FrameGeometry.Height);
            BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(12), 0); // x
            BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(16), 0); // y
            // delay_num / delay_den : delayMs / 1000 seconds => num=delayMs, den=1000
            BinaryPrimitives.WriteUInt16BigEndian(fctl.AsSpan(20), (ushort)Math.Min(delayMs, 65535));
            BinaryPrimitives.WriteUInt16BigEndian(fctl.AsSpan(22), 1000);
            fctl[24] = 1; // dispose: background
            fctl[25] = 0; // blend source
            PngChunkIo.WriteChunk(output, "fcTL", fctl);

            var idat = PngChunkIo.CompressRgb24AsIdat(rgb, FrameGeometry.Width, FrameGeometry.Height);
            if (i == 0)
            {
                PngChunkIo.WriteChunk(output, "IDAT", idat);
            }
            else
            {
                var fdat = new byte[4 + idat.Length];
                BinaryPrimitives.WriteUInt32BigEndian(fdat.AsSpan(0), seq++);
                idat.CopyTo(fdat, 4);
                PngChunkIo.WriteChunk(output, "fdAT", fdat);
            }
        }

        PngChunkIo.WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
    }

    public static DecodeContainerResult Read(Stream input)
    {
        var chunks = PngChunkIo.ReadChunks(input);
        var meta = new ArchiveMetadata();
        int? delay = null;
        uint frameCountHint = 0;
        var canvasWidth = FrameGeometry.DefaultWidth;
        var canvasHeight = FrameGeometry.DefaultWidth;
        foreach (var (type, data) in chunks)
        {
            if (type == "IHDR" && data.Length >= 8)
            {
                canvasWidth = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                canvasHeight = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
            }
            if (type == "acTL" && data.Length >= 4)
                frameCountHint = BinaryPrimitives.ReadUInt32BigEndian(data);
            if (type == "tEXt")
            {
                var tv = PngChunkIo.ParseTextChunk(data);
                if (tv == null) continue;
                if (tv.Value.Key == "frameDelayMs" && int.TryParse(tv.Value.Value, out var d))
                    delay = d;
                else
                    ApplyMeta(meta, tv.Value.Key, tv.Value.Value);
            }
        }

        if (canvasWidth != canvasHeight)
            throw new ImageArchiveException($"APNG canvas must be square (got {canvasWidth}x{canvasHeight}).");
        FrameGeometry.ValidateWidth(canvasWidth);

        // Rebuild frames from fcTL + IDAT/fdAT sequences; decode each via full PNG + Skia
        var frames = new List<FrameBitmap>();
        var idatParts = new List<byte[]>();
        void FlushFrame()
        {
            if (idatParts.Count == 0) return;
            var total = idatParts.Sum(p => p.Length);
            var idat = new byte[total];
            var o = 0;
            foreach (var p in idatParts)
            {
                p.CopyTo(idat, o);
                o += p.Length;
            }
            idatParts.Clear();
            frames.Add(DecodeIdatFrameViaSkia(idat, canvasWidth, canvasHeight));
        }

        foreach (var (type, data) in chunks)
        {
            if (type == "fcTL")
            {
                FlushFrame();
            }
            else if (type == "IDAT")
            {
                idatParts.Add(data);
            }
            else if (type == "fdAT" && data.Length > 4)
            {
                FlushFrame();
                idatParts.Add(data.AsSpan(4).ToArray());
                FlushFrame();
            }
        }
        FlushFrame();

        if (frames.Count == 0)
            throw new ImageArchiveException("APNG contained no frames.");

        return new DecodeContainerResult
        {
            Frames = frames,
            Metadata = meta,
            DelayMilliseconds = delay ?? FrameGeometry.AnimationDelayMilliseconds
        };
    }

    private static FrameBitmap DecodeIdatFrameViaSkia(byte[] idatZlib, int width, int height)
    {
        // Build a minimal still PNG and decode with Skia (lossless RGB path)
        using var ms = new MemoryStream();
        ms.Write(PngChunkIo.Signature);
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8; ihdr[9] = 2; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        PngChunkIo.WriteChunk(ms, "IHDR", ihdr);
        PngChunkIo.WriteChunk(ms, "IDAT", idatZlib);
        PngChunkIo.WriteChunk(ms, "IEND", ReadOnlySpan<byte>.Empty);
        using var sk = SKBitmap.Decode(ms.ToArray())
            ?? throw new ImageArchiveException("Skia failed to decode APNG frame PNG.");
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var c = sk.GetPixel(x, y);
            var i = (y * width + x) * 3;
            rgb[i] = c.Red; rgb[i + 1] = c.Green; rgb[i + 2] = c.Blue;
        }
        return new FrameBitmap
        {
            Width = width,
            Height = height,
            Format = PixelFormat.Rgb24,
            Pixels = rgb
        };
    }

    private static List<(string k, string v)> MetadataPairs(ArchiveMetadata m)
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
        foreach (var kv in m.AdditionalTextChunks)
            list.Add((kv.Key, kv.Value));
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

internal static class SkiaRaster
{
    public static byte[] ThroughSkia(FrameBitmap frame)
    {
        using var sk = new SKBitmap(frame.Width, frame.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var dest = sk.GetPixelSpan();
        var bpp = frame.BytesPerPixel;
        for (var i = 0; i < frame.Width * frame.Height; i++)
        {
            var si = i * bpp;
            dest[i * 4] = frame.Pixels[si];
            dest[i * 4 + 1] = frame.Pixels[si + 1];
            dest[i * 4 + 2] = frame.Pixels[si + 2];
            dest[i * 4 + 3] = 255;
        }
        using var data = sk.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new ImageArchiveException("Skia PNG encode failed.");
        using var decoded = SKBitmap.Decode(data)
            ?? throw new ImageArchiveException("Skia PNG decode failed.");
        return ThroughSkiaRgb(FromSk(decoded), decoded.Width, decoded.Height);
    }

    public static byte[] ThroughSkiaRgb(byte[] rgb, int w, int h)
    {
        using var sk = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var dest = sk.GetPixelSpan();
        for (var i = 0; i < w * h; i++)
        {
            dest[i * 4] = rgb[i * 3];
            dest[i * 4 + 1] = rgb[i * 3 + 1];
            dest[i * 4 + 2] = rgb[i * 3 + 2];
            dest[i * 4 + 3] = 255;
        }
        using var data = sk.Encode(SKEncodedImageFormat.Png, 100)!;
        using var decoded = SKBitmap.Decode(data)!;
        return FromSk(decoded);
    }

    private static byte[] FromSk(SKBitmap bmp)
    {
        var rgb = new byte[bmp.Width * bmp.Height * 3];
        for (var y = 0; y < bmp.Height; y++)
        for (var x = 0; x < bmp.Width; x++)
        {
            var c = bmp.GetPixel(x, y);
            var i = (y * bmp.Width + x) * 3;
            rgb[i] = c.Red; rgb[i + 1] = c.Green; rgb[i + 2] = c.Blue;
        }
        return rgb;
    }
}
