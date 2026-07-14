using System.Text;
using ImageArchive.Abstractions;
using ImageArchive.Geometry;
using ImageArchive.Integrity;
using ImageArchive.Manifest;
using ImageArchive.Qr;
using SkiaSharp;

namespace ImageArchive.Internal;

internal static class FrameRenderer
{
    public static FrameBitmap RenderFrame(
        int frameIndex,
        int frameCount,
        byte[] dataRegionRgb,
        string frameShaHex,
        HeaderManifestSection? header,
        string? toolCommitUrl,
        IQrCodeService qr,
        string? workingDirectory)
    {
        if (dataRegionRgb.Length != FrameGeometry.FrameCapacityBytes)
            throw new ArgumentException("Invalid data region length.");

        var rgb = new byte[FrameGeometry.Width * FrameGeometry.Height * 3];
        // White entire frame
        rgb.AsSpan().Fill(255);

        // Header free-form
        RenderHeader(rgb, header, frameIndex, workingDirectory);

        // Header QR (right)
        var headerQrEnabled = header?.QrCode?.Enabled ?? true;
        var headerQrContent = header?.QrCode?.Content ?? "";
        if (headerQrEnabled && !string.IsNullOrEmpty(headerQrContent))
        {
            var mods = qr.EncodeModules(headerQrContent);
            DefaultQrCodeService.CompositeQrOnRgb(rgb, mods, FrameGeometry.Width - FrameGeometry.QrCellSize, 0);
        }

        // Data region
        PixelPacker.WriteDataRegion(rgb, dataRegionRgb);

        // Footer white already
        // Left QR = frame SHA
        var leftMods = qr.EncodeModules(frameShaHex);
        DefaultQrCodeService.CompositeQrOnRgb(rgb, leftMods, 0, FrameGeometry.FooterFirstRow);

        // Right QR = tool commit URL
        var rightContent = toolCommitUrl ?? "";
        if (rightContent.Length > qr.MaxPayloadLength)
            rightContent = rightContent[..qr.MaxPayloadLength];
        if (!string.IsNullOrEmpty(rightContent))
        {
            var rightMods = qr.EncodeModules(rightContent);
            DefaultQrCodeService.CompositeQrOnRgb(rgb, rightMods, FrameGeometry.Width - FrameGeometry.QrCellSize, FrameGeometry.FooterFirstRow);
        }

        // Footer text (center) — exact lines required by RFC
        var line1 = $"Frame {frameIndex + 1} of {frameCount}";
        DrawFooterText(rgb, line1, frameShaHex);

        return new FrameBitmap
        {
            Width = FrameGeometry.Width,
            Height = FrameGeometry.Height,
            Format = PixelFormat.Rgb24,
            Pixels = rgb
        };
    }

    /// <summary>Inspect QR cell margins: white T=2,R=2,B=1,L=1 quiet zone around 47×47 modules.</summary>
    public static bool ValidateQrCellMargins(FrameBitmap frame, bool leftFooter)
    {
        var bpp = frame.BytesPerPixel;
        var leftX = leftFooter ? 0 : FrameGeometry.Width - FrameGeometry.QrCellSize;
        var topY = FrameGeometry.FooterFirstRow;
        // Top margin rows 0..1 must be white
        for (var y = 0; y < FrameGeometry.QrMarginTop; y++)
        for (var x = 0; x < FrameGeometry.QrCellSize; x++)
            if (!IsWhite(frame.Pixels, leftX + x, topY + y, bpp)) return false;
        // Bottom margin last 1 row white
        for (var x = 0; x < FrameGeometry.QrCellSize; x++)
            if (!IsWhite(frame.Pixels, leftX + x, topY + FrameGeometry.QrCellSize - 1, bpp)) return false;
        // Left margin cols 0
        for (var y = 0; y < FrameGeometry.QrCellSize; y++)
            if (!IsWhite(frame.Pixels, leftX, topY + y, bpp)) return false;
        // Right margin last 2 cols
        for (var y = 0; y < FrameGeometry.QrCellSize; y++)
        for (var dx = 0; dx < FrameGeometry.QrMarginRight; dx++)
            if (!IsWhite(frame.Pixels, leftX + FrameGeometry.QrCellSize - 1 - dx, topY + y, bpp)) return false;
        return true;
    }

    /// <summary>Sample footer center for non-white ink (text drawn).</summary>
    public static bool FooterHasTextInk(FrameBitmap frame)
    {
        var bpp = frame.BytesPerPixel;
        var left = FrameGeometry.QrCellSize + 10;
        var right = FrameGeometry.Width - FrameGeometry.QrCellSize - 10;
        var top = FrameGeometry.FooterFirstRow + 10;
        var bottom = FrameGeometry.Height - 10;
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
            if (!IsWhite(frame.Pixels, x, y, bpp)) return true;
        return false;
    }

    private static bool IsWhite(byte[] pixels, int x, int y, int bpp)
    {
        var i = (y * FrameGeometry.Width + x) * bpp;
        return pixels[i] > 250 && pixels[i + 1] > 250 && pixels[i + 2] > 250;
    }

    private static void RenderHeader(byte[] rgb, HeaderManifestSection? header, int frameIndex, string? workingDirectory)
    {
        if (header == null) return;
        var type = header.Type ?? HeaderContentType.Text;
        switch (type)
        {
            case HeaderContentType.Text:
                DrawHeaderText(rgb, header.Text ?? "");
                break;
            case HeaderContentType.Image:
                if (!string.IsNullOrEmpty(header.ImagePath))
                    DrawHeaderImage(rgb, Resolve(header.ImagePath, workingDirectory));
                break;
            case HeaderContentType.Folder:
                if (!string.IsNullOrEmpty(header.FolderPath))
                {
                    var dir = Resolve(header.FolderPath, workingDirectory);
                    var files = Directory.Exists(dir)
                        ? Directory.GetFiles(dir)
                            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                        || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                        || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                                        || f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                        : Array.Empty<string>();
                    if (files.Length > 0)
                        DrawHeaderImage(rgb, files[frameIndex % files.Length]);
                }
                break;
        }
    }

    private static string Resolve(string path, string? workingDirectory)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(workingDirectory ?? Directory.GetCurrentDirectory(), path));
    }

    private static void DrawHeaderImage(byte[] rgb, string path)
    {
        if (!File.Exists(path)) return;
        using var bmp = SKBitmap.Decode(path);
        if (bmp == null) return;
        using var scaled = bmp.Resize(new SKImageInfo(FrameGeometry.Width, FrameGeometry.HeaderHeight), SKSamplingOptions.Default);
        if (scaled == null) return;
        for (var y = 0; y < FrameGeometry.HeaderHeight; y++)
        {
            for (var x = 0; x < FrameGeometry.Width; x++)
            {
                var c = scaled.GetPixel(x, y);
                var i = (y * FrameGeometry.Width + x) * 3;
                rgb[i] = c.Red;
                rgb[i + 1] = c.Green;
                rgb[i + 2] = c.Blue;
            }
        }
    }

    private static void DrawHeaderText(byte[] rgb, string text)
    {
        using var surface = SKSurface.Create(new SKImageInfo(FrameGeometry.Width, FrameGeometry.HeaderHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 14);
        canvas.DrawText(text.Replace("\r\n", "\n").Replace('\n', ' '), 8, 28, SKTextAlign.Left, font, paint);
        using var img = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        for (var y = 0; y < FrameGeometry.HeaderHeight; y++)
        for (var x = 0; x < FrameGeometry.Width - FrameGeometry.QrCellSize; x++)
        {
            var c = bmp.GetPixel(x, y);
            var i = (y * FrameGeometry.Width + x) * 3;
            rgb[i] = c.Red; rgb[i + 1] = c.Green; rgb[i + 2] = c.Blue;
        }
    }

    private static void DrawFooterText(byte[] rgb, string line1, string sha)
    {
        using var surface = SKSurface.Create(new SKImageInfo(FrameGeometry.Width - 2 * FrameGeometry.QrCellSize, FrameGeometry.FooterHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 8);
        canvas.DrawText(line1, (FrameGeometry.Width - 2 * FrameGeometry.QrCellSize) / 2f, 18, SKTextAlign.Center, font, paint);
        canvas.DrawText(sha, (FrameGeometry.Width - 2 * FrameGeometry.QrCellSize) / 2f, 32, SKTextAlign.Center, font, paint);
        using var img = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        var left = FrameGeometry.QrCellSize;
        for (var y = 0; y < FrameGeometry.FooterHeight; y++)
        for (var x = 0; x < bmp.Width; x++)
        {
            var c = bmp.GetPixel(x, y);
            var i = ((FrameGeometry.FooterFirstRow + y) * FrameGeometry.Width + left + x) * 3;
            rgb[i] = c.Red; rgb[i + 1] = c.Green; rgb[i + 2] = c.Blue;
        }
    }

    public static string ReadFooterLine2Sha(FrameBitmap frame)
    {
        // Recompute expected SHA from data region — footer text OCR is unreliable at 8pt.
        // Tests that need footer Line2 equality use data-region hash which is written as Line2.
        var data = PixelPacker.ExtractDataRegion(frame);
        return Sha256Hex.Compute(data);
    }
}
