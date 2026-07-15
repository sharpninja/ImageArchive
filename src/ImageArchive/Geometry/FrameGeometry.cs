namespace ImageArchive.Geometry;

/// <summary>RFC fixed frame geometry constants.</summary>
public static class FrameGeometry
{
    public const int Width = 1024;
    public const int Height = 1024;
    public const int HeaderHeight = 67;
    public const int DataHeight = 890; // 1024 - 67 - 67
    public const int FooterHeight = 67;
    public const int DataRegionFirstRow = HeaderHeight;
    public const int FooterFirstRow = HeaderHeight + DataHeight; // 957
    public const int BytesPerPixelRgb = 3;
    /// <summary>1024 × 890 × 3 = 2,734,080 bytes per frame.</summary>
    public const int FrameCapacityBytes = Width * DataHeight * BytesPerPixelRgb;
    /// <summary>QR module matrix size (pixels at 1:1 module mapping).</summary>
    public const int QrModuleSize = 65;
    /// <summary>QR cell including 1 px white margin on every side (65 + 1 + 1).</summary>
    public const int QrCellSize = 67;
    public const int QrMarginTop = 1;
    public const int QrMarginRight = 1;
    public const int QrMarginBottom = 1;
    public const int QrMarginLeft = 1;
    public const int AnimationDelayMilliseconds = 60_000;
}
