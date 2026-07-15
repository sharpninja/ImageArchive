using System.Text;
using ImageArchive;
using ImageArchive.Abstractions;
using ImageArchive.Codecs.Skia;
using ImageArchive.Geometry;
using ImageArchive.Integrity;
using ImageArchive.Internal;
using ImageArchive.Manifest;

namespace ImageArchive.UnitTests;

public class RoundTripTests
{
    [Theory]
    [InlineData(ContainerFormat.Png)]
    [InlineData(ContainerFormat.Webp)]
    [Trait("AC", "AC-FR-DEC-001-1")]
    [Trait("AC", "AC-FR-ENC-001-1")]
    [Trait("AC", "AC-FR-CONT-001-1")]
    [Trait("AC", "AC-FR-CONT-001-2")]
    [Trait("AC", "AC-FR-ANIM-001-1")]
    public void Encode_decode_byte_identical_and_delay_60000(ContainerFormat format)
    {
        var payload = Encoding.UTF8.GetBytes("hello-imagearchive-" + string.Join("", Enumerable.Range(0, 100).Select(i => i.ToString("x2"))));
        var manifest = NewManifest(format);
        using var outMs = new MemoryStream();
        var enc = new ImageArchiveEncoder();
        using var inMs = new MemoryStream(payload);
        var result = enc.Encode(manifest, inMs, outMs, new ImageArchiveEncodeOptions
        {
            ToolCommitUrl = "https://example.com/tool"
        });
        Assert.True(result.FrameCount >= 1);
        Assert.Equal(64, result.StreamSha256.Length);

        // Real container magic
        var bytes = outMs.ToArray();
        if (format == ContainerFormat.Png)
        {
            Assert.Equal(0x89, bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'N', bytes[2]);
            Assert.Equal((byte)'G', bytes[3]);
        }
        else
        {
            Assert.Equal((byte)'R', bytes[0]);
            Assert.Equal((byte)'I', bytes[1]);
            Assert.Equal((byte)'F', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
            Assert.Equal((byte)'W', bytes[8]);
            Assert.Equal((byte)'E', bytes[9]);
            Assert.Equal((byte)'B', bytes[10]);
            Assert.Equal((byte)'P', bytes[11]);
        }

        outMs.Position = 0;
        var container = MultiFrameContainer.Read(outMs);
        Assert.Equal(FrameGeometry.AnimationDelayMilliseconds, container.DelayMilliseconds);

        outMs.Position = 0;
        var decoded = new ImageArchiveDecoder().Decode(outMs);
        using var reader = new MemoryStream();
        decoded.ArchiveStream.CopyTo(reader);
        Assert.Equal(payload, reader.ToArray());
        Assert.Equal(result.StreamSha256, decoded.StreamSha256);
    }

    [Fact]
    [Trait("AC", "AC-FR-INTG-003-2")]
    [Trait("AC", "AC-FR-INTG-002-1")]
    public void Tampered_streamSha256_in_manifest_fails_closed_on_Decode()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var manifest = NewManifest(ContainerFormat.Png);
        using var outMs = new MemoryStream();
        new ImageArchiveEncoder().Encode(manifest, new MemoryStream(payload), outMs, new ImageArchiveEncodeOptions());
        var file = outMs.ToArray();

        // Tamper jsonManifest streamSha256 inside PNG tEXt chunk
        var tampered = TamperPngManifestStreamSha(file, new string('0', 64));
        using var corrupt = new MemoryStream(tampered);
        var ex = Assert.Throws<IntegrityException>(() =>
            new ImageArchiveDecoder().Decode(corrupt, new ImageArchiveDecodeOptions { VerifyStreamSha256 = true }));
        Assert.Contains("streamSha256", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("AC", "AC-FR-INTG-002-1")]
    public void Tampered_frameSha_metadata_fails_closed_on_Decode()
    {
        var payload = new byte[] { 9, 8, 7, 6, 5 };
        var manifest = NewManifest(ContainerFormat.Png);
        using var outMs = new MemoryStream();
        new ImageArchiveEncoder().Encode(manifest, new MemoryStream(payload), outMs, new ImageArchiveEncodeOptions());
        var file = outMs.ToArray();
        var tampered = TamperPngTextValue(file, "frameSha256.0", new string('f', 64));
        using var corrupt = new MemoryStream(tampered);
        Assert.Throws<IntegrityException>(() =>
            new ImageArchiveDecoder().Decode(corrupt, new ImageArchiveDecodeOptions { VerifyFrameIntegrity = true }));
    }

    [Fact]
    [Trait("AC", "AC-FR-GEOM-002-4")]
    public void Capacity_default_2734080()
    {
        Assert.Equal(2_734_080, FrameGeometry.FrameCapacityBytes);
    }

    [Fact]
    public void Round_trip_custom_frame_width_512()
    {
        var payload = Encoding.UTF8.GetBytes("width-512-payload");
        var manifest = new ImageArchiveManifest
        {
            Version = "1.0.0",
            Encoder = new EncoderManifestSection { Name = "t", Version = "1", Sha256 = new string('a', 64) },
            Archive = new ArchiveManifestSection { Type = ArchiveType.Raw, MimeType = "application/octet-stream", Source = "x" },
            Output = new OutputManifestSection { Path = "o.png", Format = ContainerFormat.Png },
            FrameWidth = 512,
            Header = new HeaderManifestSection
            {
                Type = HeaderContentType.Text,
                Text = "w512",
                QrCode = new QrCodeManifestSection { Content = "https://ex.com/w", Enabled = true }
            },
            Frames = new List<FrameManifestSection> { new() }
        };
        using var outMs = new MemoryStream();
        var enc = new ImageArchiveEncoder().Encode(manifest, new MemoryStream(payload), outMs,
            new ImageArchiveEncodeOptions { ToolCommitUrl = "https://ex.com/t" });
        Assert.Equal(512, enc.EmbeddedManifest.FrameWidth);
        outMs.Position = 0;
        var decoded = new ImageArchiveDecoder().Decode(outMs);
        Assert.Equal(payload, ReadAll(decoded.ArchiveStream));
        Assert.Equal(512, decoded.Manifest?.FrameWidth);
    }

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static byte[] TamperPngManifestStreamSha(byte[] png, string newHash)
    {
        // Replace streamSha256 value inside jsonManifest tEXt if present
        var s = Encoding.UTF8.GetString(png);
        var idx = s.IndexOf("streamSha256", StringComparison.Ordinal);
        if (idx < 0) return png;
        // Find 64-hex after streamSha256
        var hexStart = -1;
        for (var i = idx; i < s.Length - 64; i++)
        {
            if (IsHex64(s.AsSpan(i, 64))) { hexStart = i; break; }
        }
        if (hexStart < 0) return png;
        var copy = (byte[])png.Clone();
        var newBytes = Encoding.UTF8.GetBytes(newHash);
        Array.Copy(newBytes, 0, copy, hexStart, 64);
        // CRC of that tEXt chunk is now wrong — re-write via chunk rebuild is safer:
        return RewritePngText(png, "jsonManifest", old =>
        {
            // replace first 64-hex after streamSha256 key in JSON
            var j = old;
            var key = "\"streamSha256\":\"";
            var p = j.IndexOf(key, StringComparison.Ordinal);
            if (p < 0) key = "\"streamSha256\": \"";
            p = j.IndexOf(key, StringComparison.Ordinal);
            if (p < 0) return old;
            p += key.Length;
            if (p + 64 > j.Length) return old;
            return j.Substring(0, p) + newHash + j.Substring(p + 64);
        });
    }

    private static byte[] TamperPngTextValue(byte[] png, string key, string newValue) =>
        RewritePngText(png, key, _ => newValue);

    private static byte[] RewritePngText(byte[] png, string key, Func<string, string> mutate)
    {
        using var input = new MemoryStream(png);
        var chunks = PngChunkIo.ReadChunks(input);
        using var output = new MemoryStream();
        output.Write(PngChunkIo.Signature);
        foreach (var (type, data) in chunks)
        {
            if (type == "tEXt")
            {
                var tv = PngChunkIo.ParseTextChunk(data);
                if (tv != null && tv.Value.Key == key)
                {
                    var newVal = mutate(tv.Value.Value);
                    PngChunkIo.WriteChunk(output, "tEXt", PngChunkIo.BuildTextChunkPayload(key, newVal));
                    continue;
                }
            }
            PngChunkIo.WriteChunk(output, type, data);
        }
        return output.ToArray();
    }

    private static bool IsHex64(ReadOnlySpan<char> s)
    {
        for (var i = 0; i < 64; i++)
        {
            var c = s[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
        }
        return true;
    }

    private static ImageArchiveManifest NewManifest(ContainerFormat format) => new()
    {
        Version = "1.0.0",
        Encoder = new EncoderManifestSection
        {
            Name = "test",
            Version = "1.0.0",
            Sha256 = new string('a', 64)
        },
        Archive = new ArchiveManifestSection
        {
            Type = ArchiveType.Raw,
            MimeType = "application/octet-stream",
            Source = "payload.bin"
        },
        Output = new OutputManifestSection { Path = "out.png", Format = format },
        Header = new HeaderManifestSection
        {
            Type = HeaderContentType.Text,
            Text = "Test",
            QrCode = new QrCodeManifestSection { Content = "https://example.com", Enabled = true }
        },
        Frames = new List<FrameManifestSection> { new() }
    };
}
