namespace ImageArchive.Geometry;

/// <summary>
/// Frame geometry: square canvas of configurable width (default 1024).
/// Header/footer chrome heights and QR cell size are fixed; data region scales with width.
/// </summary>
public static class FrameGeometry
{
    public const int MinWidth = 512;
    public const int MaxWidth = 1440;
    public const int DefaultWidth = 1024;

    /// <summary>Fixed chrome band height (matches QR cell size).</summary>
    public const int HeaderHeight = 67;
    public const int FooterHeight = 67;
    public const int BytesPerPixelRgb = 3;

    public const int QrModuleSize = 65;
    public const int QrCellSize = 67;
    public const int QrMarginTop = 1;
    public const int QrMarginRight = 1;
    public const int QrMarginBottom = 1;
    public const int QrMarginLeft = 1;
    public const int AnimationDelayMilliseconds = 60_000;

    private static readonly AsyncLocal<int> ActiveWidth = new();

    /// <summary>Active frame edge length (square). Defaults to <see cref="DefaultWidth"/>.</summary>
    public static int Width
    {
        get
        {
            var w = ActiveWidth.Value;
            return w == 0 ? DefaultWidth : w;
        }
    }

    /// <summary>Always equal to <see cref="Width"/> (frames are square).</summary>
    public static int Height => Width;

    public static int DataHeight => Width - HeaderHeight - FooterHeight;
    public static int DataRegionFirstRow => HeaderHeight;
    public static int FooterFirstRow => HeaderHeight + DataHeight;
    public static int FrameCapacityBytes => Width * DataHeight * BytesPerPixelRgb;

    /// <summary>Validate and activate a frame width for the current async context.</summary>
    public static IDisposable Use(int width)
    {
        ValidateWidth(width);
        var previous = ActiveWidth.Value;
        ActiveWidth.Value = width;
        return new WidthScope(previous);
    }

    public static void ValidateWidth(int width)
    {
        if (width < MinWidth || width > MaxWidth)
            throw new ArgumentOutOfRangeException(nameof(width), width,
                $"Frame width must be between {MinWidth} and {MaxWidth} (inclusive). Height equals width (square).");
    }

    public static int ClampOrDefault(int? width) =>
        width is null or 0 ? DefaultWidth : width.Value;

    public static bool TryValidateWidth(int width, out string? error)
    {
        if (width < MinWidth || width > MaxWidth)
        {
            error = $"frameWidth must be between {MinWidth} and {MaxWidth} (got {width})";
            return false;
        }
        error = null;
        return true;
    }

    private sealed class WidthScope : IDisposable
    {
        private readonly int _previous;
        private bool _disposed;
        public WidthScope(int previous) => _previous = previous;
        public void Dispose()
        {
            if (_disposed) return;
            ActiveWidth.Value = _previous;
            _disposed = true;
        }
    }
}
