using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageArchive.Encoders;
using ImageArchive.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;
using ZXing;

namespace ImageArchive.IntegrationTests;

/// <summary>
/// Full round-trip integration test for ImageArchive.
/// 
/// 1. Clones (or uses) the HEAD of the ImageArchive repository
/// 2. Creates a compressed tarball (.tar.gz)
/// 3. Builds an ImageArchive from the tarball
/// 4. Validates QR codes and text metadata in the resulting image
/// 5. Extracts the data stream
/// 6. Verifies the extracted tarball matches the original
/// </summary>
public class ImageArchiveRoundTripTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string TestWorkDir = Path.Combine(Path.GetTempPath(), "ImageArchive.IntegrationTests", Guid.NewGuid().ToString("N")[..8]);

    public ImageArchiveRoundTripTests()
    {
        Directory.CreateDirectory(TestWorkDir);
    }

    [Fact]
    public async Task RoundTrip_Clone_Tar_Encode_Validate_Decode_Compare()
    {
        // ------------------------------------------------------------------
        // 1. Prepare a clean clone / copy of the current repository HEAD
        // ------------------------------------------------------------------
        string cloneDir = Path.Combine(TestWorkDir, "repo");
        Directory.CreateDirectory(cloneDir);

        // We copy the current source tree (excluding bin/obj/.git) so the test
        // works even when run offline or inside CI without network.
        CopyDirectory(RepoRoot, cloneDir, exclude: new[] { "bin", "obj", ".git", "tests", "artifacts" });

        // Capture the current commit (if .git exists) for metadata
        string? commitHash = null;
        string? sourceUrl = "https://github.com/sharpninja/ImageArchive";
        try
        {
            var gitDir = Path.Combine(RepoRoot, ".git");
            if (Directory.Exists(gitDir))
            {
                commitHash = RunGit("rev-parse HEAD", RepoRoot).Trim();
            }
        }
        catch { /* optional */ }

        // ------------------------------------------------------------------
        // 2. Create a compressed tarball of the repository
        // ------------------------------------------------------------------
        string tarballPath = Path.Combine(TestWorkDir, "repo.tar.gz");
        CreateTarGz(cloneDir, tarballPath);

        Assert.True(File.Exists(tarballPath), "Tarball was not created");
        long originalTarSize = new FileInfo(tarballPath).Length;
        Assert.True(originalTarSize > 100, "Tarball is suspiciously small");

        byte[] originalTarBytes = await File.ReadAllBytesAsync(tarballPath);
        string originalTarSha = Convert.ToHexString(SHA256.HashData(originalTarBytes)).ToLowerInvariant();

        // ------------------------------------------------------------------
        // 3. Build a manifest and encode the ImageArchive
        // ------------------------------------------------------------------
        string imagePath = Path.Combine(TestWorkDir, "archive.png");

        var manifest = new Manifest
        {
            Version = "1.0.0",
            Encoder = new EncoderInfo
            {
                Name = "ImageArchive.IntegrationTests",
                Version = "1.0.0",
                Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("ImageArchive.IntegrationTests"))).ToLowerInvariant()
            },
            Archive = new ArchiveInfo
            {
                Type = "tar",
                MimeType = "application/gzip",
                Source = tarballPath,
                SourceUrl = sourceUrl,
                CommitHash = commitHash,
                OriginalFileName = "repo.tar.gz"
            },
            Output = new OutputInfo
            {
                Path = imagePath,
                Format = "png"
            },
            Header = new HeaderInfo
            {
                Type = "text",
                Text = "ImageArchive Integration Test",
                QrCode = new QrCodeInfo
                {
                    Content = sourceUrl + (commitHash != null ? $"/tree/{commitHash}" : ""),
                    Enabled = true
                }
            },
            Frames = new List<FrameInfo> { new() }
        };

        var encoder = new PngEncoder();
        await using (var dataStream = File.OpenRead(tarballPath))
        {
            await encoder.EncodeAsync(dataStream, manifest, imagePath);
        }

        Assert.True(File.Exists(imagePath), "ImageArchive was not created");
        Assert.True(new FileInfo(imagePath).Length > 10_000, "ImageArchive is too small");

        // ------------------------------------------------------------------
        // 4. Validate the image metadata and QR codes
        // ------------------------------------------------------------------
        using var image = await Image.LoadAsync<Rgba32>(imagePath);

        // Text chunks
        var pngMeta = image.Metadata.GetPngMetadata();
        var textDict = pngMeta.TextData.ToDictionary(t => t.Keyword, t => t.Value, StringComparer.OrdinalIgnoreCase);

        Assert.True(textDict.ContainsKey("version"), "Missing version text chunk");
        Assert.Equal("1.0.0", textDict["version"]);

        Assert.True(textDict.ContainsKey("encoderName"));
        Assert.Equal("ImageArchive.IntegrationTests", textDict["encoderName"]);

        Assert.True(textDict.ContainsKey("mimeType"));
        Assert.Equal("application/gzip", textDict["mimeType"]);

        Assert.True(textDict.ContainsKey("archiveType"));
        Assert.Equal("tar", textDict["archiveType"]);

        Assert.True(textDict.ContainsKey("originalFileName"));
        Assert.Equal("repo.tar.gz", textDict["originalFileName"]);

        Assert.True(textDict.ContainsKey("jsonManifest"), "jsonManifest text chunk missing");
        Assert.True(textDict.ContainsKey("totalFrames"));

        // QR codes – decode them with ZXing
        // Top-right QR
        string? topRightQr = DecodeQrFromRegion(image, FrameWidth - PngEncoder.QrTotalSize, 0, PngEncoder.QrTotalSize, PngEncoder.QrTotalSize);
        Assert.False(string.IsNullOrEmpty(topRightQr), "Top-right QR code could not be decoded");
        Assert.Contains("github.com/sharpninja/ImageArchive", topRightQr);

        // Bottom-left QR (should be the frame SHA)
        string? bottomLeftQr = DecodeQrFromRegion(image, 0, FrameHeight - PngEncoder.FooterHeight, PngEncoder.QrTotalSize, PngEncoder.QrTotalSize);
        Assert.False(string.IsNullOrEmpty(bottomLeftQr), "Bottom-left QR code could not be decoded");
        Assert.Equal(64, bottomLeftQr!.Length); // SHA-256 hex
        Assert.True(bottomLeftQr.All(c => "0123456789abcdef".Contains(c)), "Bottom-left QR is not a valid hex SHA");

        // Bottom-right QR
        string? bottomRightQr = DecodeQrFromRegion(image, FrameWidth - PngEncoder.QrTotalSize, FrameHeight - PngEncoder.FooterHeight, PngEncoder.QrTotalSize, PngEncoder.QrTotalSize);
        Assert.False(string.IsNullOrEmpty(bottomRightQr), "Bottom-right QR code could not be decoded");
        Assert.Contains("github.com/sharpninja/ImageArchive", bottomRightQr);

        // ------------------------------------------------------------------
        // 5. Decode the ImageArchive and extract the data stream
        // ------------------------------------------------------------------
        var decodeResult = await encoder.DecodeAsync(imagePath);

        Assert.NotNull(decodeResult.DataStream);
        Assert.NotNull(decodeResult.Manifest);
        Assert.NotEmpty(decodeResult.FrameSha256s);

        // The decoded stream contains full frames (padded). We need the original size.
        // For a single-frame archive that is smaller than one frame, the padding is zeros.
        // We trim trailing zeros carefully by knowing the original size.
        byte[] decodedBytes;
        using (var ms = new MemoryStream())
        {
            await decodeResult.DataStream.CopyToAsync(ms);
            decodedBytes = ms.ToArray();
        }

        // The original tarball size is known
        Assert.True(decodedBytes.Length >= originalTarBytes.Length,
            $"Decoded stream shorter than original: {decodedBytes.Length} < {originalTarBytes.Length}");

        // The first originalTarBytes.Length bytes must match exactly
        byte[] extractedTar = decodedBytes.AsSpan(0, originalTarBytes.Length).ToArray();
        string extractedSha = Convert.ToHexString(SHA256.HashData(extractedTar)).ToLowerInvariant();

        Assert.Equal(originalTarSha, extractedSha);

        // Write the extracted tarball for further inspection if needed
        string extractedPath = Path.Combine(TestWorkDir, "extracted.tar.gz");
        await File.WriteAllBytesAsync(extractedPath, extractedTar);

        // ------------------------------------------------------------------
        // 6. Optional: extract the tarball and compare directory trees
        // ------------------------------------------------------------------
        string extractDir = Path.Combine(TestWorkDir, "extracted");
        Directory.CreateDirectory(extractDir);
        ExtractTarGz(extractedPath, extractDir);

        // Compare a few key files
        Assert.True(File.Exists(Path.Combine(extractDir, "README.md")), "README.md missing after extraction");
        Assert.True(File.Exists(Path.Combine(extractDir, "ImageArchive.sln")), "Solution file missing");

        // Content comparison of README
        string originalReadme = await File.ReadAllTextAsync(Path.Combine(cloneDir, "README.md"));
        string extractedReadme = await File.ReadAllTextAsync(Path.Combine(extractDir, "README.md"));
        Assert.Equal(originalReadme, extractedReadme);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string FindRepoRoot()
    {
        // Walk up from the test assembly location until we find the solution
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "ImageArchive.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir) ?? dir;
        }
        // Fallback for the layout used in this environment
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static void CopyDirectory(string source, string dest, string[] exclude)
    {
        foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, dirPath);
            if (exclude.Any(e => relative.StartsWith(e, StringComparison.OrdinalIgnoreCase) ||
                                  relative.Contains(Path.DirectorySeparatorChar + e + Path.DirectorySeparatorChar) ||
                                  relative.EndsWith(Path.DirectorySeparatorChar + e)))
                continue;

            Directory.CreateDirectory(Path.Combine(dest, relative));
        }

        foreach (string filePath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, filePath);
            if (exclude.Any(e => relative.StartsWith(e, StringComparison.OrdinalIgnoreCase) ||
                                  relative.Contains(Path.DirectorySeparatorChar + e + Path.DirectorySeparatorChar)))
                continue;

            string target = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(filePath, target, true);
        }
    }

    private static void CreateTarGz(string sourceDir, string tarGzPath)
    {
        // Use the system tar command for simplicity and correctness
        var psi = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-czf \"{tarGzPath}\" -C \"{sourceDir}\" .",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"tar failed: {proc.StandardError.ReadToEnd()}");
    }

    private static void ExtractTarGz(string tarGzPath, string destDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xzf \"{tarGzPath}\" -C \"{destDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"tar extract failed: {proc.StandardError.ReadToEnd()}");
    }

    private static string RunGit(string args, string workingDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi)!;
        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return output;
    }

    private static string? DecodeQrFromRegion(Image<Rgba32> image, int x, int y, int w, int h)
    {
        // Crop the region
        using var region = image.Clone(ctx => ctx.Crop(new Rectangle(x, y, w, h)));

        // Convert to a simple byte array luminance source for maximum compatibility
        int width = region.Width;
        int height = region.Height;
        var pixels = new byte[width * height];
        int i = 0;
        for (int yy = 0; yy < height; yy++)
        {
            for (int xx = 0; xx < width; xx++)
            {
                var p = region[xx, yy];
                // Simple luminance
                pixels[i++] = (byte)((p.R * 0.299) + (p.G * 0.587) + (p.B * 0.114));
            }
        }

        var luminance = new ZXing.RGBLuminanceSource(pixels, width, height, ZXing.RGBLuminanceSource.BitmapFormat.Gray8);

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new ZXing.Common.DecodingOptions
            {
                PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                TryHarder = true,
                PureBarcode = true
            }
        };

        var result = reader.Decode(luminance);
        return result?.Text;
    }

    // Constants matching the encoder
    private const int FrameWidth = 1024;
    private const int FrameHeight = 1024;
}
