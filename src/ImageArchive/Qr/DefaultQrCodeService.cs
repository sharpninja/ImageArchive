using ImageArchive.Abstractions;
using ImageArchive.Geometry;
using QRCoder;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace ImageArchive.Qr;

/// <summary>QR encode (QRCoder) + decode (ZXing). Max payload for 65×65 documented in docs/qr-payload-limits.md.</summary>
public sealed class DefaultQrCodeService : IQrCodeService
{
    // ECC M; version ≤10 (~57 modules ISO, ~65 with library quiet zone) fits QrModuleSize=65.
    // Byte-mode capacity at version 10 ECC-M is ~213; keep a safety margin under that.
    public int MaxPayloadLength => 200;

    public bool[,] EncodeModules(string payload)
    {
        payload ??= "";
        var max = IsHexSha256(payload) ? 64 : MaxPayloadLength;
        if (payload.Length > max)
            throw new QrPayloadTooLongException(max, payload.Length);

        using var gen = new QRCodeGenerator();
        // ECC M; prefer native matrix centered in 65×65 (no scale-up distortion)
        var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var modules = data.ModuleMatrix;
        var n = modules.Count;
        if (n > FrameGeometry.QrModuleSize)
            throw new QrPayloadTooLongException(MaxPayloadLength, payload.Length);

        var result = new bool[FrameGeometry.QrModuleSize, FrameGeometry.QrModuleSize];
        var off = (FrameGeometry.QrModuleSize - n) / 2; // center; outer cells stay false (white)
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
            result[y + off, x + off] = modules[y][x];
        return result;
    }

    public string DecodeFromPixels(ReadOnlySpan<byte> pixels, int width, int height, PixelFormat format)
    {
        var bpp = format == PixelFormat.Rgba32 ? 4 : 3;
        using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var dest = bmp.GetPixelSpan();
        for (var i = 0; i < width * height; i++)
        {
            var si = i * bpp;
            var di = i * 4;
            dest[di] = pixels[si];
            dest[di + 1] = pixels[si + 1];
            dest[di + 2] = pixels[si + 2];
            dest[di + 3] = 255;
        }

        var reader = new BarcodeReader
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                TryHarder = true,
                TryInverted = true, // dark chrome: white modules on black cell
                PureBarcode = false,
                CharacterSet = "UTF-8"
            }
        };
        var result = reader.Decode(bmp);
        if (result != null) return result.Text ?? "";
        // Retry upscaled 4x for small QR cells
        using var big = bmp.Resize(new SKImageInfo(width * 4, height * 4), SKSamplingOptions.Default);
        if (big == null) return "";
        return reader.Decode(big)?.Text ?? "";
    }

    // invert: dark chrome uses black cell + white modules (still scannable with TryInverted).
    public static void CompositeQrOnRgb(byte[] frameRgb, bool[,] modules, int cellLeft, int cellTop, bool invert = false)
    {
        byte bg = invert ? (byte)0 : (byte)255;
        byte fg = invert ? (byte)255 : (byte)0;
        for (var y = 0; y < FrameGeometry.QrCellSize; y++)
        {
            for (var x = 0; x < FrameGeometry.QrCellSize; x++)
            {
                SetRgb(frameRgb, cellLeft + x, cellTop + y, bg, bg, bg);
            }
        }
        for (var my = 0; my < FrameGeometry.QrModuleSize; my++)
        {
            for (var mx = 0; mx < FrameGeometry.QrModuleSize; mx++)
            {
                if (!modules[my, mx]) continue;
                var px = cellLeft + FrameGeometry.QrMarginLeft + mx;
                var py = cellTop + FrameGeometry.QrMarginTop + my;
                SetRgb(frameRgb, px, py, fg, fg, fg);
            }
        }
    }

    private static void SetRgb(byte[] frame, int x, int y, byte r, byte g, byte b)
    {
        if ((uint)x >= FrameGeometry.Width || (uint)y >= FrameGeometry.Height) return;
        var i = (y * FrameGeometry.Width + x) * 3;
        frame[i] = r;
        frame[i + 1] = g;
        frame[i + 2] = b;
    }

    private static bool IsHexSha256(string s) =>
        s.Length == 64 && s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
}
