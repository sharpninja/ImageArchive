using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using ImageArchive;
using ImageArchive.Abstractions;
using ImageArchive.Codecs.Skia;
using ImageArchive.Geometry;
using ImageArchive.Internal;
using ImageArchive.Manifest;
using ImageArchive.Qr;

namespace ImageArchive.IntegrationTests;

public class FlagshipE2ETests
{
    [Fact]
    [Trait("AC", "AC-FR-E2E-001-1")]
    [Trait("AC", "AC-FR-E2E-001-2")]
    [Trait("AC", "AC-FR-E2E-001-3")]
    [Trait("AC", "AC-FR-E2E-001-4")]
    [Trait("AC", "AC-FR-E2E-001-5")]
    [Trait("AC", "AC-FR-E2E-001-6")]
    [Trait("AC", "AC-FR-E2E-001-7")]
    public void Clone_tar_encode_qr_meta_extract_compare()
    {
        var root = Path.Combine(Path.GetTempPath(), "ia-e2e-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var cloneDir = Path.Combine(root, "clone");
            var repoRoot = FindRepoRoot();
            if (!TryShallowClone(repoRoot, cloneDir))
            {
                Directory.CreateDirectory(Path.Combine(cloneDir, ".git"));
                File.WriteAllText(Path.Combine(cloneDir, ".git", "HEAD"), "ref: refs/heads/main\n");
                File.WriteAllText(Path.Combine(cloneDir, "file.txt"), "fixture-worktree");
                Directory.CreateDirectory(Path.Combine(cloneDir, "sub"));
                File.WriteAllText(Path.Combine(cloneDir, "sub", "a.txt"), "nested");
            }

            using var tarStream = ImageArchive.IO.DefaultArchiveSourceLoader.BuildGitWorktreeTarGz(cloneDir);
            var tarBytes = tarStream.ToArray();

            var headerUrl = "https://github.com/sharpninja/ImageArchive";
            var toolUrl = "https://github.com/sharpninja/ImageArchive/commit/deadbeef";
            var manifest = new ImageArchiveManifest
            {
                Version = "1.0.0",
                Encoder = new EncoderManifestSection
                {
                    Name = "ImageArchive.E2E",
                    Version = "1.0.0",
                    Sha256 = new string('d', 64)
                },
                Archive = new ArchiveManifestSection
                {
                    Type = ArchiveType.Raw,
                    MimeType = "application/gzip",
                    Source = "archive.tar.gz",
                    SourceUrl = headerUrl
                },
                Output = new OutputManifestSection { Path = "archive.png", Format = ContainerFormat.Png },
                Header = new HeaderManifestSection
                {
                    Type = HeaderContentType.Text,
                    Text = "E2E",
                    QrCode = new QrCodeManifestSection { Content = headerUrl, Enabled = true }
                },
                Frames = new List<FrameManifestSection> { new() }
            };

            var imagePath = Path.Combine(root, "archive.png");
            EncodeResult enc;
            using (var fs = File.Create(imagePath))
            {
                enc = new ImageArchiveEncoder().Encode(manifest, new MemoryStream(tarBytes), fs,
                    new ImageArchiveEncodeOptions { ToolCommitUrl = toolUrl });
            }

            // PNG magic
            var magic = new byte[4];
            using (var fs = File.OpenRead(imagePath))
                _ = fs.Read(magic);
            Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, magic);

            // Container delay + metadata + per-frame QR
            using (var fs = File.OpenRead(imagePath))
            {
                var container = MultiFrameContainer.Read(fs);
                Assert.Equal(FrameGeometry.AnimationDelayMilliseconds, container.DelayMilliseconds);
                Assert.Equal("ImageArchive.E2E", container.Metadata.EncoderName);
                Assert.False(string.IsNullOrEmpty(container.Metadata.JsonManifest));
                Assert.Contains("streamSha256", container.Metadata.JsonManifest, StringComparison.OrdinalIgnoreCase);

                var qr = new DefaultQrCodeService();
                for (var i = 0; i < container.Frames.Count; i++)
                {
                    var frame = container.Frames[i];
                    var data = PixelPacker.ExtractDataRegion(frame);
                    var sha = ImageArchive.Integrity.Sha256Hex.Compute(data);
                    Assert.True(container.Metadata.AdditionalTextChunks.TryGetValue($"frameSha256.{i}", out var stored));
                    Assert.Equal(sha, stored, ignoreCase: true);
                    Assert.True(FrameRenderer.ValidateQrCellMargins(frame, leftFooter: true));
                    Assert.True(FrameRenderer.FooterHasTextInk(frame));
                }
            }

            // Decode payload
            using var image = File.OpenRead(imagePath);
            var decoded = new ImageArchiveDecoder().Decode(image);
            Assert.Equal("ImageArchive.E2E", decoded.Metadata.EncoderName);
            using var payloadMs = new MemoryStream();
            decoded.ArchiveStream.CopyTo(payloadMs);
            Assert.Equal(tarBytes, payloadMs.ToArray());

            // Extract tar.gz and recursively compare to clone (exclude bin/obj)
            var extractDir = Path.Combine(root, "extracted");
            Directory.CreateDirectory(extractDir);
            ExtractTarGz(payloadMs.ToArray(), extractDir);
            AssertDirectoryTreeEqual(cloneDir, extractDir);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    private static void ExtractTarGz(byte[] tarGz, string destDir)
    {
        using var input = new MemoryStream(tarGz);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var tar = new TarReader(gz);
        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) != null)
        {
            if (entry.EntryType is TarEntryType.Directory) continue;
            var path = Path.Combine(destDir, entry.Name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = File.Create(path);
            entry.DataStream?.CopyTo(fs);
        }
    }

    private static void AssertDirectoryTreeEqual(string expectedRoot, string actualRoot)
    {
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".vs" };
        string Rel(string root, string full) => Path.GetRelativePath(root, full).Replace('\\', '/');

        var expectedFiles = Directory.EnumerateFiles(expectedRoot, "*", SearchOption.AllDirectories)
            .Where(f => !Rel(expectedRoot, f).Split('/').Any(exclude.Contains))
            .Select(f => Rel(expectedRoot, f))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var actualFiles = Directory.EnumerateFiles(actualRoot, "*", SearchOption.AllDirectories)
            .Select(f => Rel(actualRoot, f))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expectedFiles, actualFiles);
        foreach (var rel in expectedFiles)
        {
            var e = File.ReadAllBytes(Path.Combine(expectedRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
            var a = File.ReadAllBytes(Path.Combine(actualRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Equal(e, a);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "ImageArchive-RFC.md")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static bool TryShallowClone(string sourceRepo, string dest)
    {
        try
        {
            if (!Directory.Exists(Path.Combine(sourceRepo, ".git"))) return false;
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone --depth 1 \"{sourceRepo}\" \"{dest}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(120_000);
            return p.ExitCode == 0 && Directory.Exists(Path.Combine(dest, ".git"));
        }
        catch { return false; }
    }
}
