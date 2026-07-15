using ImageArchive.Abstractions;
using ImageArchive.Geometry;

namespace ImageArchive.Internal;

internal static class PixelPacker
{
    /// <summary>
    /// Packs archive bytes into the data region of an RGB24 frame (rows HeaderHeight .. FooterFirstRow-1).
    /// Remaining capacity is zero-filled (included in integrity hash).
    /// </summary>
    public static byte[] PackDataRegion(ReadOnlySpan<byte> payloadSlice)
    {
        var data = new byte[FrameGeometry.FrameCapacityBytes];
        var n = Math.Min(payloadSlice.Length, data.Length);
        payloadSlice[..n].CopyTo(data);
        // remainder already zero
        return data;
    }

    public static byte[] ExtractDataRegion(FrameBitmap frame)
    {
        if (frame.Width != frame.Height)
            throw new ImageArchiveException($"Frame must be square (got {frame.Width}x{frame.Height}).");
        FrameGeometry.ValidateWidth(frame.Width);

        using (FrameGeometry.Use(frame.Width))
        {
            var bpp = frame.BytesPerPixel;
            var data = new byte[FrameGeometry.FrameCapacityBytes];
            var src = frame.Pixels;
            var di = 0;
            for (var y = FrameGeometry.DataRegionFirstRow; y < FrameGeometry.FooterFirstRow; y++)
            {
                var rowStart = y * FrameGeometry.Width * bpp;
                for (var x = 0; x < FrameGeometry.Width; x++)
                {
                    var pi = rowStart + x * bpp;
                    data[di++] = src[pi];     // R
                    data[di++] = src[pi + 1]; // G
                    data[di++] = src[pi + 2]; // B
                }
            }
            return data;
        }
    }

    public static void WriteDataRegion(byte[] fullFrameRgb, ReadOnlySpan<byte> dataRegionRgb)
    {
        if (fullFrameRgb.Length != FrameGeometry.Width * FrameGeometry.Height * 3)
            throw new ArgumentException("Full frame RGB buffer size mismatch.");
        if (dataRegionRgb.Length != FrameGeometry.FrameCapacityBytes)
            throw new ArgumentException("Data region size mismatch.");

        var offset = FrameGeometry.DataRegionFirstRow * FrameGeometry.Width * 3;
        dataRegionRgb.CopyTo(fullFrameRgb.AsSpan(offset, FrameGeometry.FrameCapacityBytes));
    }
}
