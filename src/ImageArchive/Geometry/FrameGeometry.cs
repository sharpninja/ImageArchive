namespace ImageArchive.Geometry;

/// <summary>RFC 1.0.0 fixed frame geometry constants.</summary>
public static class FrameGeometry
{
    public const int Width = 1024;
    public const int Height = 1024;
    public const int HeaderHeight = 50;
    public const int DataHeight = 924;
    public const int FooterHeight = 50;
    public const int DataRegionFirstRow = 50;
    public const int FooterFirstRow = 974;
    public const int BytesPerPixelRgb = 3;
    public const int FrameCapacityBytes = Width * DataHeight * BytesPerPixelRgb; // 2_838_528
    public const int QrModuleSize = 47;
    public const int QrCellSize = 50;
    public const int QrMarginTop = 2;
    public const int QrMarginRight = 2;
    public const int QrMarginBottom = 1;
    public const int QrMarginLeft = 1;
    public const int AnimationDelayMilliseconds = 60_000;
}
