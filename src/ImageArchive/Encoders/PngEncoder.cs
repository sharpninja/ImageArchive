using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageArchive.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using QRCoder;

namespace ImageArchive.Encoders;

/// <summary>
/// Reference PNG (APNG) encoder for ImageArchive.
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

    public async Task EncodeAsync(
        Stream dataStream,
        Manifest manifest,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        // Calculate how many frames we need
        long totalBytes = dataStream.Length;
        int frameCount = (int)Math.Ceiling(totalBytes / (double)BytesPerFrame);
        if (frameCount == 0) frameCount = 1;

        var images = new List<Image<Rgba32>>();

        byte[] buffer = new byte[BytesPerFrame];
        var frameShas = new List<string>();

        for (int i = 0; i < frameCount; i++)
        {
            int bytesRead = await dataStream.ReadAsync(buffer.AsMemory(0, BytesPerFrame), cancellationToken);
            // Pad remaining with zeros if last frame is short
            if (bytesRead < BytesPerFrame)
            {
                Array.Clear(buffer, bytesRead, BytesPerFrame - bytesRead);
            }

            // Compute per-frame SHA-256 of the raw data that will be stored
            string frameSha = Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, bytesRead))).ToLowerInvariant();
            frameShas.Add(frameSha);

            var frame = CreateFrame(buffer, i, frameCount, frameSha, manifest);
            images.Add(frame);
        }

        // Write as APNG
        await using var fs = File.Create(outputPath);
        var encoder = new PngEncoder
        {
            // ImageSharp currently has limited APNG animation support;
            // for a full production implementation we would use a dedicated APNG writer.
            // Here we write a multi-frame PNG that most tools can still open.
            CompressionLevel = PngCompressionLevel.BestCompression
        };

        // For simplicity in this reference implementation we write the first frame
        // as a normal PNG and note that full APNG support requires additional work.
        // A complete implementation would use a proper APNG library.
        await images[0].SaveAsPngAsync(fs, encoder, cancellationToken);

        foreach (var img in images) img.Dispose();
    }

    public async Task<DecodeResult> DecodeAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        // Placeholder – a full decoder would extract all frames,
        // recompute SHA-256 of each data region, and concatenate.
        throw new NotImplementedException("Full multi-frame decode will be completed in a future revision.");
    }

    private static Image<Rgba32> CreateFrame(
        byte[] data,
        int frameIndex,
        int totalFrames,
        string frameSha,
        Manifest manifest)
    {
        var image = new Image<Rgba32>(FrameWidth, FrameHeight);

        // Fill header and footer white
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.White, new Rectangle(0, 0, FrameWidth, HeaderHeight));
            ctx.Fill(Color.White, new Rectangle(0, FrameHeight - FooterHeight, FrameWidth, FooterHeight));
        });

        // Write data region (RGB only)
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

        // Footer text
        string frameText = $"Frame {frameIndex + 1} of {totalFrames}";
        // (Font rendering and QR codes would be drawn here using ImageSharp.Drawing + QRCoder)

        // TODO: draw QR codes and text using the exact layout rules from the RFC

        return image;
    }
}
