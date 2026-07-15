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
        string? workingDirectory,
        bool dark = false)
    {
        if (dataRegionRgb.Length != FrameGeometry.FrameCapacityBytes)
            throw new ArgumentException("Invalid data region length.");

        var rgb = new byte[FrameGeometry.Width * FrameGeometry.Height * 3];
        // Light chrome default (white). Data region is overwritten by payload packing.
        rgb.AsSpan().Fill(255);
        if (dark)
        {
            FillBand(rgb, 0, FrameGeometry.HeaderHeight, 0, 0, 0);
            FillBand(rgb, FrameGeometry.FooterFirstRow, FrameGeometry.FooterHeight, 0, 0, 0);
        }

        // Header free-form
        RenderHeader(rgb, header, frameIndex, workingDirectory, dark);

        // Header QR (right) — always black modules on white cell (phone scanners fail on inverted QR).
        var headerQrEnabled = header?.QrCode?.Enabled ?? true;
        var headerQrContent = header?.QrCode?.Content ?? "";
        if (headerQrEnabled && !string.IsNullOrEmpty(headerQrContent))
        {
            var mods = qr.EncodeModules(headerQrContent);
            DefaultQrCodeService.CompositeQrOnRgb(rgb, mods, FrameGeometry.Width - FrameGeometry.QrCellSize, 0, invert: false);
        }

        // Data region
        PixelPacker.WriteDataRegion(rgb, dataRegionRgb);

        // Left QR = frame SHA (standard polarity)
        var leftMods = qr.EncodeModules(frameShaHex);
        DefaultQrCodeService.CompositeQrOnRgb(rgb, leftMods, 0, FrameGeometry.FooterFirstRow, invert: false);

        // Right QR = tool commit URL (standard polarity)
        var rightContent = toolCommitUrl ?? "";
        if (rightContent.Length > qr.MaxPayloadLength)
            rightContent = rightContent[..qr.MaxPayloadLength];
        if (!string.IsNullOrEmpty(rightContent))
        {
            var rightMods = qr.EncodeModules(rightContent);
            DefaultQrCodeService.CompositeQrOnRgb(rgb, rightMods, FrameGeometry.Width - FrameGeometry.QrCellSize, FrameGeometry.FooterFirstRow, invert: false);
        }

        // Footer text (center)
        var line1 = $"Frame {frameIndex + 1} of {frameCount}";
        DrawFooterText(rgb, line1, frameShaHex, dark);

        return new FrameBitmap
        {
            Width = FrameGeometry.Width,
            Height = FrameGeometry.Height,
            Format = PixelFormat.Rgb24,
            Pixels = rgb
        };
    }

    /// <summary>Inspect QR cell margins: white 1 px quiet zone (QR cells stay light even under dark chrome).</summary>
    public static bool ValidateQrCellMargins(FrameBitmap frame, bool leftFooter, bool dark = false)
    {
        _ = dark; // margins remain white for scannable black-on-white QR
        var bpp = frame.BytesPerPixel;
        var leftX = leftFooter ? 0 : FrameGeometry.Width - FrameGeometry.QrCellSize;
        var topY = FrameGeometry.FooterFirstRow;
        // Top margin
        for (var y = 0; y < FrameGeometry.QrMarginTop; y++)
        for (var x = 0; x < FrameGeometry.QrCellSize; x++)
            if (!IsWhite(frame.Pixels, leftX + x, topY + y, bpp)) return false;
        // Bottom margin
        for (var y = 0; y < FrameGeometry.QrMarginBottom; y++)
        for (var x = 0; x < FrameGeometry.QrCellSize; x++)
            if (!IsWhite(frame.Pixels, leftX + x, topY + FrameGeometry.QrCellSize - 1 - y, bpp)) return false;
        // Left margin
        for (var y = 0; y < FrameGeometry.QrCellSize; y++)
        for (var dx = 0; dx < FrameGeometry.QrMarginLeft; dx++)
            if (!IsWhite(frame.Pixels, leftX + dx, topY + y, bpp)) return false;
        // Right margin
        for (var y = 0; y < FrameGeometry.QrCellSize; y++)
        for (var dx = 0; dx < FrameGeometry.QrMarginRight; dx++)
            if (!IsWhite(frame.Pixels, leftX + FrameGeometry.QrCellSize - 1 - dx, topY + y, bpp)) return false;
        return true;
    }

    /// <summary>Sample footer center for text ink (dark ink on light, or light ink on dark).</summary>
    public static bool FooterHasTextInk(FrameBitmap frame, bool dark = false)
    {
        var bpp = frame.BytesPerPixel;
        var left = FrameGeometry.QrCellSize + 10;
        var right = FrameGeometry.Width - FrameGeometry.QrCellSize - 10;
        var top = FrameGeometry.FooterFirstRow + 10;
        var bottom = FrameGeometry.Height - 10;
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            if (dark)
            {
                if (!IsBlack(frame.Pixels, x, y, bpp)) return true;
            }
            else
            {
                if (!IsWhite(frame.Pixels, x, y, bpp)) return true;
            }
        }
        return false;
    }

    /// <summary>True if header free-form band uses dark background (sample left of QR).</summary>
    public static bool HeaderHasDarkBackground(FrameBitmap frame)
    {
        var bpp = frame.BytesPerPixel;
        // Sample near top-left of free-form area
        return IsBlack(frame.Pixels, 8, 8, bpp);
    }

    /// <summary>True if header free-form area has light ink (for dark chrome text).</summary>
    public static bool HeaderHasLightInk(FrameBitmap frame)
    {
        var bpp = frame.BytesPerPixel;
        var right = FrameGeometry.Width - FrameGeometry.QrCellSize - 4;
        for (var y = 4; y < FrameGeometry.HeaderHeight - 4; y++)
        for (var x = 4; x < right; x++)
            if (!IsBlack(frame.Pixels, x, y, bpp)) return true;
        return false;
    }

    private static void FillBand(byte[] rgb, int firstRow, int height, byte r, byte g, byte b)
    {
        for (var y = firstRow; y < firstRow + height; y++)
        {
            for (var x = 0; x < FrameGeometry.Width; x++)
            {
                var i = (y * FrameGeometry.Width + x) * 3;
                rgb[i] = r;
                rgb[i + 1] = g;
                rgb[i + 2] = b;
            }
        }
    }

    private static bool IsWhite(byte[] pixels, int x, int y, int bpp)
    {
        var i = (y * FrameGeometry.Width + x) * bpp;
        return pixels[i] > 250 && pixels[i + 1] > 250 && pixels[i + 2] > 250;
    }

    private static bool IsBlack(byte[] pixels, int x, int y, int bpp)
    {
        var i = (y * FrameGeometry.Width + x) * bpp;
        return pixels[i] < 5 && pixels[i + 1] < 5 && pixels[i + 2] < 5;
    }

    private static void RenderHeader(byte[] rgb, HeaderManifestSection? header, int frameIndex, string? workingDirectory, bool dark)
    {
        if (header == null)
        {
            // Band already filled by caller for dark/light
            return;
        }
        var type = header.Type ?? HeaderContentType.Text;
        switch (type)
        {
            case HeaderContentType.Text:
                DrawHeaderText(rgb, header.Text ?? "", dark);
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
        // Header images keep original colors even when dark chrome is enabled.
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

    private static void DrawHeaderText(byte[] rgb, string text, bool dark)
    {
        using var surface = SKSurface.Create(new SKImageInfo(FrameGeometry.Width, FrameGeometry.HeaderHeight));
        var canvas = surface.Canvas;
        canvas.Clear(dark ? SKColors.Black : SKColors.White);
        using var paint = new SKPaint { Color = dark ? SKColors.White : SKColors.Black, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 14);
        canvas.DrawText(text.Replace("\r\n", "\n").Replace('\n', ' '), 8, 36, SKTextAlign.Left, font, paint);
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

    private static void DrawFooterText(byte[] rgb, string line1, string sha, bool dark)
    {
        using var surface = SKSurface.Create(new SKImageInfo(FrameGeometry.Width - 2 * FrameGeometry.QrCellSize, FrameGeometry.FooterHeight));
        var canvas = surface.Canvas;
        canvas.Clear(dark ? SKColors.Black : SKColors.White);
        using var paint = new SKPaint { Color = dark ? SKColors.White : SKColors.Black, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 8);
        canvas.DrawText(line1, (FrameGeometry.Width - 2 * FrameGeometry.QrCellSize) / 2f, 24, SKTextAlign.Center, font, paint);
        canvas.DrawText(sha, (FrameGeometry.Width - 2 * FrameGeometry.QrCellSize) / 2f, 42, SKTextAlign.Center, font, paint);
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
        var data = PixelPacker.ExtractDataRegion(frame);
        return Sha256Hex.Compute(data);
    }
}
