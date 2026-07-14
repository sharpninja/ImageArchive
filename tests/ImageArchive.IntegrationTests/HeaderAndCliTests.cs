using ImageArchive;
using ImageArchive.Abstractions;
using ImageArchive.Geometry;
using ImageArchive.Manifest;
using SkiaSharp;

namespace ImageArchive.IntegrationTests;

public class HeaderAndCliTests
{
    [Fact]
    [Trait("AC", "AC-FR-HDR-001-4")]
    public void Header_1024x50_image_is_embedded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ia-hdr-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            var imgPath = Path.Combine(dir, "header.png");
            using (var bmp = new SKBitmap(1024, 50))
            {
                for (var x = 0; x < 1024; x++)
                for (var y = 0; y < 50; y++)
                    bmp.SetPixel(x, y, new SKColor((byte)(x % 256), (byte)(y * 5), 128));
                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.OpenWrite(imgPath);
                data.SaveTo(fs);
            }

            var manifest = BaseManifest();
            manifest.Header = new HeaderManifestSection
            {
                Type = HeaderContentType.Image,
                ImagePath = imgPath,
                QrCode = new QrCodeManifestSection { Content = "https://ex.com/h", Enabled = true }
            };
            var payload = new byte[] { 10, 20, 30 };
            using var outMs = new MemoryStream();
            new ImageArchiveEncoder().Encode(manifest, new MemoryStream(payload), outMs,
                new ImageArchiveEncodeOptions { WorkingDirectory = dir });
            Assert.True(outMs.Length > 0);
            outMs.Position = 0;
            var decoded = new ImageArchiveDecoder().Decode(outMs);
            Assert.Equal(payload, ReadAll(decoded.ArchiveStream));
        }
        finally { try { Directory.Delete(dir, true); } catch { /* ignore */ } }
    }

    [Fact]
    [Trait("AC", "AC-FR-HDR-001-5")]
    public void Header_folder_round_robins()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ia-fld-" + Guid.NewGuid().ToString("n"));
        var folder = Path.Combine(dir, "frames");
        Directory.CreateDirectory(folder);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var p = Path.Combine(folder, $"{i:000}.png");
                using var bmp = new SKBitmap(100, 50);
                bmp.Erase(new SKColor((byte)(i * 40), 0, 0));
                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.OpenWrite(p);
                data.SaveTo(fs);
            }

            // Large payload => multiple frames
            var payload = new byte[FrameGeometry.FrameCapacityBytes + 100];
            Random.Shared.NextBytes(payload);
            var manifest = BaseManifest();
            manifest.Header = new HeaderManifestSection
            {
                Type = HeaderContentType.Folder,
                FolderPath = folder,
                QrCode = new QrCodeManifestSection { Enabled = false }
            };
            using var outMs = new MemoryStream();
            var result = new ImageArchiveEncoder().Encode(manifest, new MemoryStream(payload), outMs,
                new ImageArchiveEncodeOptions { WorkingDirectory = dir });
            Assert.True(result.FrameCount >= 2);
            outMs.Position = 0;
            var decoded = new ImageArchiveDecoder().Decode(outMs);
            Assert.Equal(payload, ReadAll(decoded.ArchiveStream));
        }
        finally { try { Directory.Delete(dir, true); } catch { /* ignore */ } }
    }

    [Fact]
    public void Cli_init_writes_blank_manifest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ia-init-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "manifest.json");
            var exit = ImageArchive.Cli.Program.Main(new[] { "init", "--output", path });
            Assert.Equal(0, exit);
            Assert.True(File.Exists(path));
            var json = File.ReadAllText(path);
            var result = new JsonSchemaManifestValidator().ValidateJson(json);
            Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
            var m = ManifestJson.Deserialize(json);
            Assert.Equal("1.0.0", m.Version);
            Assert.Equal(ArchiveType.Raw, m.Archive.Type);
            Assert.True(m.Frames.Count >= 1);

            // without --force, second init fails
            var again = ImageArchive.Cli.Program.Main(new[] { "init", "--output", path });
            Assert.Equal(1, again);

            var forced = ImageArchive.Cli.Program.Main(new[] { "init", "--output", path, "--force" });
            Assert.Equal(0, forced);
        }
        finally { try { Directory.Delete(dir, true); } catch { /* ignore */ } }
    }

    [Fact]
    [Trait("AC", "AC-FR-CLI-001-1")]
    [Trait("AC", "AC-FR-CLI-001-2")]
    public void Cli_encode_decode_exit_zero()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ia-cli-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            var payloadPath = Path.Combine(dir, "data.bin");
            var payload = System.Text.Encoding.UTF8.GetBytes("cli-round-trip-payload");
            File.WriteAllBytes(payloadPath, payload);
            var outImage = Path.Combine(dir, "out.iar");
            var manifestPath = Path.Combine(dir, "m.json");
            var sha = new string('b', 64);
            File.WriteAllText(manifestPath, $$"""
            {
              "version": "1.0.0",
              "encoder": { "name": "cli-test", "version": "1.0.0", "sha256": "{{sha}}" },
              "archive": { "type": "raw", "mimeType": "application/octet-stream", "source": "data.bin" },
              "output": { "path": "out.iar", "format": "png" },
              "header": { "type": "text", "text": "cli", "qrCode": { "content": "https://ex.com", "enabled": true } },
              "frames": [ {} ]
            }
            """);

            var encodeExit = ImageArchive.Cli.Program.Main(new[] { "encode", "--manifest", manifestPath });
            Assert.Equal(0, encodeExit);
            Assert.True(File.Exists(outImage));

            var extracted = Path.Combine(dir, "extracted.bin");
            var decodeExit = ImageArchive.Cli.Program.Main(new[] { "decode", "--input", outImage, "--output", extracted });
            Assert.Equal(0, decodeExit);
            Assert.Equal(payload, File.ReadAllBytes(extracted));
        }
        finally { try { Directory.Delete(dir, true); } catch { /* ignore */ } }
    }

    [Fact]
    [Trait("AC", "AC-FR-ARCH-001-1")]
    [Trait("AC", "AC-FR-E2E-001-6")]
    public void Git_worktree_tar_round_trip_compare()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ia-git-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            // Minimal fake git worktree
            Directory.CreateDirectory(Path.Combine(dir, ".git"));
            File.WriteAllText(Path.Combine(dir, ".git", "HEAD"), "ref: refs/heads/main\n");
            File.WriteAllText(Path.Combine(dir, "readme.txt"), "hello worktree");
            Directory.CreateDirectory(Path.Combine(dir, "bin"));
            File.WriteAllText(Path.Combine(dir, "bin", "skip.dll"), "x");

            using var tar = ImageArchive.IO.DefaultArchiveSourceLoader.BuildGitWorktreeTarGz(dir);
            var bytes = tar.ToArray();
            Assert.True(bytes.Length > 20);

            var manifest = BaseManifest();
            manifest.Archive.Type = ArchiveType.Raw;
            manifest.Archive.MimeType = "application/gzip";
            using var outMs = new MemoryStream();
            new ImageArchiveEncoder().Encode(manifest, new MemoryStream(bytes), outMs, new ImageArchiveEncodeOptions());
            outMs.Position = 0;
            var decoded = new ImageArchiveDecoder().Decode(outMs);
            var round = ReadAll(decoded.ArchiveStream);
            Assert.Equal(bytes, round);
        }
        finally { try { Directory.Delete(dir, true); } catch { /* ignore */ } }
    }

    private static ImageArchiveManifest BaseManifest() => new()
    {
        Version = "1.0.0",
        Encoder = new EncoderManifestSection { Name = "t", Version = "1", Sha256 = new string('c', 64) },
        Archive = new ArchiveManifestSection { Type = ArchiveType.Raw, MimeType = "application/octet-stream", Source = "x" },
        Output = new OutputManifestSection { Path = "o.iar", Format = ContainerFormat.Png },
        Frames = new List<FrameManifestSection> { new() }
    };

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
