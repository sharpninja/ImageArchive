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
    public void Data_region_capacity_is_2838528()
    {
        Assert.Equal(2_838_528, FrameGeometry.FrameCapacityBytes);
        Assert.Equal(1024 * 924 * 3, FrameGeometry.FrameCapacityBytes);
    }

    [Fact]
    [Trait("AC", "AC-FR-GEOM-002-1")]
    public void Region_rows_match_RFC()
    {
        Assert.Equal(50, FrameGeometry.HeaderHeight);
        Assert.Equal(50, FrameGeometry.DataRegionFirstRow);
        Assert.Equal(924, FrameGeometry.DataHeight);
        Assert.Equal(974, FrameGeometry.FooterFirstRow);
        Assert.Equal(50, FrameGeometry.FooterHeight);
    }
}
