using ImageArchive.Abstractions;
using ImageArchive.Geometry;
using ImageArchive.Internal;

namespace ImageArchive.UnitTests;

public class PixelPackerTests
{
    [Fact]
    [Trait("AC", "AC-FR-PIXEL-001-1")]
    [Trait("AC", "AC-FR-PIXEL-002-1")]
    public void Pack_and_extract_round_trip_rgb_order()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var region = PixelPacker.PackDataRegion(payload);
        Assert.Equal(FrameGeometry.FrameCapacityBytes, region.Length);
        Assert.Equal(1, region[0]);
        Assert.Equal(2, region[1]);
        Assert.Equal(3, region[2]);
        // padding zeros
        Assert.Equal(0, region[9]);

        var frameRgb = new byte[FrameGeometry.Width * FrameGeometry.Height * 3];
        frameRgb.AsSpan().Fill(255);
        PixelPacker.WriteDataRegion(frameRgb, region);
        var frame = new FrameBitmap
        {
            Width = FrameGeometry.Width,
            Height = FrameGeometry.Height,
            Format = PixelFormat.Rgb24,
            Pixels = frameRgb
        };
        var extracted = PixelPacker.ExtractDataRegion(frame);
        Assert.Equal(region, extracted);
    }

    [Fact]
    [Trait("AC", "AC-FR-PIXEL-002-2")]
    public void Final_frame_padding_is_zero_filled()
    {
        var region = PixelPacker.PackDataRegion(new byte[] { 0xAA });
        Assert.Equal(0xAA, region[0]);
        for (var i = 1; i < region.Length; i++)
            Assert.Equal(0, region[i]);
    }
}
