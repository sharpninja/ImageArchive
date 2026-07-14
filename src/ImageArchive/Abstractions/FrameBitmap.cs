namespace ImageArchive.Abstractions;

/// <summary>Pixel buffer for one 1024×1024 frame. Row-major RGB or RGBA.</summary>
public sealed class FrameBitmap
{
    public int Width { get; init; }
    public int Height { get; init; }
    public PixelFormat Format { get; init; }
    public required byte[] Pixels { get; init; }

    public int BytesPerPixel => Format == PixelFormat.Rgba32 ? 4 : 3;
}
