using ImageArchive.Geometry;

namespace ImageArchive.UnitTests;

public class GeometryTests
{
    [Fact]
    [Trait("AC", "AC-FR-GEOM-001-1")]
    public void Frame_is_1024x1024()
    {
        Assert.Equal(1024, FrameGeometry.Width);
        Assert.Equal(1024, FrameGeometry.Height);
    }

    [Fact]
    [Trait("AC", "AC-FR-GEOM-002-4")]
    public void Data_region_capacity_is_2734080()
    {
        Assert.Equal(2_734_080, FrameGeometry.FrameCapacityBytes);
        Assert.Equal(1024 * 890 * 3, FrameGeometry.FrameCapacityBytes);
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
