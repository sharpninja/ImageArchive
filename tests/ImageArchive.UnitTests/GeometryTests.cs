using ImageArchive.Geometry;

namespace ImageArchive.UnitTests;

public class GeometryTests
{
    [Fact]
    [Trait("AC", "AC-FR-GEOM-001-1")]
    public void Default_frame_is_1024_square()
    {
        Assert.Equal(1024, FrameGeometry.DefaultWidth);
        Assert.Equal(1024, FrameGeometry.Width);
        Assert.Equal(1024, FrameGeometry.Height);
        Assert.Equal(FrameGeometry.Width, FrameGeometry.Height);
    }

    [Fact]
    [Trait("AC", "AC-FR-GEOM-002-4")]
    public void Data_region_capacity_scales_with_width()
    {
        Assert.Equal(2_734_080, FrameGeometry.FrameCapacityBytes);
        Assert.Equal(1024 * 890 * 3, FrameGeometry.FrameCapacityBytes);
        using (FrameGeometry.Use(512))
        {
            Assert.Equal(512, FrameGeometry.Width);
            Assert.Equal(512 - 67 - 67, FrameGeometry.DataHeight);
            Assert.Equal(512 * (512 - 134) * 3, FrameGeometry.FrameCapacityBytes);
        }
        using (FrameGeometry.Use(1440))
        {
            Assert.Equal(1440 * (1440 - 134) * 3, FrameGeometry.FrameCapacityBytes);
        }
        Assert.Equal(1024, FrameGeometry.Width); // restored
    }

    [Fact]
    [Trait("AC", "AC-FR-GEOM-002-1")]
    public void Region_rows_match_geometry()
    {
        Assert.Equal(67, FrameGeometry.HeaderHeight);
        Assert.Equal(67, FrameGeometry.DataRegionFirstRow);
        Assert.Equal(890, FrameGeometry.DataHeight);
        Assert.Equal(957, FrameGeometry.FooterFirstRow);
        Assert.Equal(67, FrameGeometry.FooterHeight);
        Assert.Equal(1024, FrameGeometry.HeaderHeight + FrameGeometry.DataHeight + FrameGeometry.FooterHeight);
    }

    [Fact]
    public void Width_out_of_range_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameGeometry.ValidateWidth(511));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameGeometry.ValidateWidth(1441));
    }

    [Fact]
    [Trait("AC", "AC-FR-HDR-002-1")]
    public void Qr_cell_is_67_with_65_modules_and_1px_margins()
    {
        Assert.Equal(65, FrameGeometry.QrModuleSize);
        Assert.Equal(67, FrameGeometry.QrCellSize);
        Assert.Equal(1, FrameGeometry.QrMarginTop);
        Assert.Equal(1, FrameGeometry.QrMarginRight);
        Assert.Equal(1, FrameGeometry.QrMarginBottom);
        Assert.Equal(1, FrameGeometry.QrMarginLeft);
        Assert.Equal(
            FrameGeometry.QrModuleSize + FrameGeometry.QrMarginLeft + FrameGeometry.QrMarginRight,
            FrameGeometry.QrCellSize);
    }
}
