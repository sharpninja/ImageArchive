using ImageArchive.Abstractions;
using ImageArchive.Geometry;

namespace ImageArchive.Codecs.Skia;

/// <summary>
/// Dispatches to real APNG (PNG signature) or animated WebP (RIFF/WEBP).
/// All frame rasters pass through SkiaSharp (TR-CONT-SKIA-001).
/// </summary>
internal static class MultiFrameContainer
{
    public static void Write(Stream output, ContainerFormat format, IReadOnlyList<FrameBitmap> frames, ArchiveMetadata metadata, int delayMs)
    {
        if (format == ContainerFormat.Webp)
            WebpAnimContainer.Write(output, frames, metadata, delayMs);
        else
            ApngContainer.Write(output, frames, metadata, delayMs);
    }

    public static DecodeContainerResult Read(Stream input)
    {
        if (!input.CanSeek)
        {
            var ms = new MemoryStream();
            input.CopyTo(ms);
            ms.Position = 0;
            input = ms;
        }

        var pos = input.Position;
        Span<byte> magic = stackalloc byte[12];
        var n = input.Read(magic);
        input.Position = pos;
        if (n >= 8 && magic[0] == 0x89 && magic[1] == 0x50 && magic[2] == 0x4E && magic[3] == 0x47)
            return ApngContainer.Read(input);
        if (n >= 12 && magic[0] == (byte)'R' && magic[1] == (byte)'I' && magic[2] == (byte)'F' && magic[3] == (byte)'F'
            && magic[8] == (byte)'W' && magic[9] == (byte)'E' && magic[10] == (byte)'B' && magic[11] == (byte)'P')
            return WebpAnimContainer.Read(input);
        throw new ImageArchiveException("Unsupported container: expected PNG/APNG or RIFF/WEBP.");
    }
}
