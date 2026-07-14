using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageArchive.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using QRCoder;

namespace ImageArchive.Encoders;

/// <summary>
/// Reference PNG (APNG-capable) encoder/decoder for ImageArchive (RFC 1.0.0).
/// </summary>
public sealed class PngEncoder : IImageEncoder
{
    public string FormatName => "png";
    public string MimeType => "image/png";

    public const int FrameWidth = 1024;
    public const int FrameHeight = 1024;
    public const int HeaderHeight = 50;
    public const int FooterHeight = 50;
    public const int DataHeight = FrameHeight - HeaderHeight - FooterHeight; // 924
    public const int BytesPerFrame = FrameWidth * DataHeight * 3; // 2,838,528
    public const int QrSize = 47;
    public const int QrTotalSize = 50; // including margins (2px top/right, 1px bottom/left)

    public async Task EncodeAsync(
        Stream dataStream,
        Manifest manifest,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        // Ensure we can seek / know length
        if (!dataStream.CanSeek)
        {
            var ms = new MemoryStream();
            await dataStream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            dataStream = ms;
        }

        long totalBytes = dataStream.Length;
        int frameCount = (int)Math.Ceiling(totalBytes / (double)BytesPerFrame);
        if (frameCount == 0) frameCount = 1;

        var frames = new List<Image<Rgba32>>();
        var frameShas = new List<string>();

        byte[] buffer = new byte[BytesPerFrame];

        for (int i = 0; i < frameCount; i++)
        {
            int bytesRead = await dataStream.ReadAsync(buffer.AsMemory(0, BytesPerFrame), cancellationToken);
            if (bytesRead < BytesPerFrame)
            {
                Array.Clear(buffer, bytesRead, BytesPerFrame - bytesRead);
            }

            // Per-frame SHA-256 of the *actual* data bytes that were read (not the padding)
            string frameSha = Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, bytesRead))).ToLowerInvariant();
            frameShas.Add(frameSha);

            var frame = CreateFrame(buffer, bytesRead, i, frameCount, frameSha, manifest);
            frames.Add(frame);
        }

        // Write as multi-frame PNG
        await using var fs = File.Create(outputPath);

        using var master = frames[0];
        for (int i = 1; i < frames.Count; i++)
        {
            master.Frames.AddFrame(frames[i].Frames.RootFrame);
            frames[i].Dispose();
        }

        // Set frame delay to 1 minute (6000 * 1/100s = 60s)
        for (int i = 0; i < master.Frames.Count; i++)
        {
            var meta = master.Frames[i].Metadata.GetPngMetadata();
            meta.FrameDelay = 6000;
        }

        // Embed all metadata as tEXt chunks
        var pngMeta = master.Metadata.GetPngMetadata();
        EmbedTextChunks(pngMeta, manifest, frameShas);

        var encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            CompressionLevel = PngCompressionLevel.BestCompression,
            TextEncoding = Encoding.UTF8
        };

        await master.SaveAsPngAsync(fs, encoder, cancellationToken);
        master.Dispose();
    }

    public async Task<DecodeResult> DecodeAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(inputPath, cancellationToken);

        int frameCount = image.Frames.Count;
        var frameShas = new List<string>();
        var dataMs = new MemoryStream();

        // Extract metadata from text chunks
        var pngMeta = image.Metadata.GetPngMetadata();
        var manifest = ExtractManifestFromTextChunks(pngMeta);

        for (int f = 0; f < frameCount; f++)
        {
            using var frameImage = image.Frames.CloneFrame(f);

            // Extract RGB data from data region
            var frameData = new byte[BytesPerFrame];
            int offset = 0;

            for (int y = HeaderHeight; y < FrameHeight - FooterHeight; y++)
            {
                for (int x = 0; x < FrameWidth; x++)
                {
                    var pixel = frameImage[x, y];
                    frameData[offset++] = pixel.R;
                    frameData[offset++] = pixel.G;
                    frameData[offset++] = pixel.B;
                }
            }

            // Recompute SHA of the full data region (including padding zeros)
            string computedFullSha = Convert.ToHexString(SHA256.HashData(frameData)).ToLowerInvariant();
            frameShas.Add(computedFullSha);

            await dataMs.WriteAsync(frameData, cancellationToken);
        }

        dataMs.Position = 0;

        return new DecodeResult
        {
            DataStream = dataMs,
            Manifest = manifest,
            FrameSha256s = frameShas
        };
    }

    private static Image<Rgba32> CreateFrame(
        byte[] data,
        int actualBytes,
        int frameIndex,
        int totalFrames,
        string frameSha,
        Manifest manifest)
    {
        var image = new Image<Rgba32>(FrameWidth, FrameHeight);

        // White backgrounds for header & footer
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.White, new Rectangle(0, 0, FrameWidth, HeaderHeight));
            ctx.Fill(Color.White, new Rectangle(0, FrameHeight - FooterHeight, FrameWidth, FooterHeight));
        });

        // Write RGB data into the data region
        int dataOffset = 0;
        for (int y = HeaderHeight; y < FrameHeight - FooterHeight; y++)
        {
            for (int x = 0; x < FrameWidth; x++)
            {
                if (dataOffset + 2 < data.Length)
                {
                    byte r = data[dataOffset++];
                    byte g = data[dataOffset++];
                    byte b = data[dataOffset++];
                    image[x, y] = new Rgba32(r, g, b, 255);
                }
                else
                {
                    image[x, y] = new Rgba32(0, 0, 0, 255);
                }
            }
        }

        // ---- Header ----
        DrawHeaderContent(image, manifest, frameIndex);

        // Top-right QR (user-definable / intended for source commit URL)
        string topQrContent = manifest.Header?.QrCode?.Content
                              ?? manifest.Archive.SourceUrl
                              ?? "https://github.com/sharpninja/ImageArchive";
        if (manifest.Header?.QrCode?.Enabled != false)
        {
            DrawQrCode(image, topQrContent, FrameWidth - QrTotalSize, 0);
        }

        // ---- Footer ----
        string bottomRightContent = manifest.Archive.SourceUrl
                                   ?? "https://github.com/sharpninja/ImageArchive";

        DrawQrCode(image, frameSha, 0, FrameHeight - FooterHeight);                    // left
        DrawQrCode(image, bottomRightContent, FrameWidth - QrTotalSize, FrameHeight - FooterHeight); // right

        // Centered text between the two QR codes
        DrawFooterText(image, frameIndex + 1, totalFrames, frameSha);

        return image;
    }

    private static void DrawHeaderContent(Image<Rgba32> image, Manifest manifest, int frameIndex)
    {
        string text = manifest.Archive.OriginalFileName
                      ?? Path.GetFileName(manifest.Archive.Source)
                      ?? "ImageArchive";

        if (!string.IsNullOrWhiteSpace(manifest.Header?.Text))
            text = manifest.Header.Text;

        try
        {
            var font = SystemFonts.CreateFont("DejaVu Sans", 14, FontStyle.Bold);
            var options = new RichTextOptions(font)
            {
                Origin = new PointF(8, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            image.Mutate(ctx => ctx.DrawText(options, text, Color.Black));
        }
        catch
        {
            // Fallback if system font is missing
        }
    }

    private static void DrawFooterText(Image<Rgba32> image, int frameNumber, int totalFrames, string frameSha)
    {
        try
        {
            var font = SystemFonts.CreateFont("DejaVu Sans", 8, FontStyle.Regular);
            string line1 = $"Frame {frameNumber} of {totalFrames}";
            string line2 = frameSha;

            float availableWidth = FrameWidth - (QrTotalSize * 2);
            float centerX = QrTotalSize + (availableWidth / 2f);

            var options1 = new RichTextOptions(font)
            {
                Origin = new PointF(centerX, FrameHeight - FooterHeight + 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            var options2 = new RichTextOptions(font)
            {
                Origin = new PointF(centerX, FrameHeight - FooterHeight + 22),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            image.Mutate(ctx =>
            {
                ctx.DrawText(options1, line1, Color.Black);
                ctx.DrawText(options2, line2, Color.Black);
            });
        }
        catch
        {
            // Ignore if font missing
        }
    }

    private static void DrawQrCode(Image<Rgba32> image, string content, int x, int y)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        byte[] qrBytes = qrCode.GetGraphic(1);

        using var qrImage = Image.Load<Rgba32>(qrBytes);

        using var target = new Image<Rgba32>(QrTotalSize, QrTotalSize);
        target.Mutate(ctx =>
        {
            ctx.Fill(Color.White);
            qrImage.Mutate(q => q.Resize(QrSize, QrSize));
            ctx.DrawImage(qrImage, new Point(1, 2), 1f); // left=1, top=2
        });

        image.Mutate(ctx => ctx.DrawImage(target, new Point(x, y), 1f));
    }

    private static void EmbedTextChunks(PngMetadata meta, Manifest manifest, IReadOnlyList<string> frameShas)
    {
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                meta.TextData.Add(new PngTextData(key, value, string.Empty, string.Empty));
        }

        Add("version", manifest.Version);
        Add("encoderName", manifest.Encoder.Name);
        Add("encoderVersion", manifest.Encoder.Version);
        Add("encoderSha256", manifest.Encoder.Sha256);
        Add("archiveType", manifest.Archive.Type);
        Add("mimeType", manifest.Archive.MimeType);
        Add("sourceUrl", manifest.Archive.SourceUrl);
        Add("commitHash", manifest.Archive.CommitHash);
        Add("originalFileName", manifest.Archive.OriginalFileName);
        Add("totalFrames", frameShas.Count.ToString());

        // Embed the full schema if available
        try
        {
            string schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "schema", "imagearchive-schema.json"));
            if (File.Exists(schemaPath))
            {
                Add("jsonSchema", File.ReadAllText(schemaPath));
            }
        }
        catch { /* ignore */ }

        Add("jsonManifest", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = false }));
    }

    private static Manifest ExtractManifestFromTextChunks(PngMetadata meta)
    {
        var dict = meta.TextData.ToDictionary(t => t.Keyword, t => t.Value, StringComparer.OrdinalIgnoreCase);

        string Get(string key) => dict.TryGetValue(key, out var v) ? v : "";

        var manifest = new Manifest
        {
            Version = Get("version") is { Length: > 0 } v ? v : "1.0.0",
            Encoder = new EncoderInfo
            {
                Name = Get("encoderName"),
                Version = Get("encoderVersion"),
                Sha256 = Get("encoderSha256")
            },
            Archive = new ArchiveInfo
            {
                Type = Get("archiveType"),
                MimeType = Get("mimeType"),
                SourceUrl = Get("sourceUrl"),
                CommitHash = Get("commitHash"),
                OriginalFileName = Get("originalFileName")
            }
        };

        if (dict.TryGetValue("jsonManifest", out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var full = JsonSerializer.Deserialize<Manifest>(json);
                if (full != null) return full;
            }
            catch { /* fall through */ }
        }

        return manifest;
    }
}
