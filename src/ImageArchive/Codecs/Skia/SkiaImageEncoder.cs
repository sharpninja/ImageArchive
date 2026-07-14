using ImageArchive.Abstractions;
using ImageArchive.Geometry;

namespace ImageArchive.Codecs.Skia;

public sealed class SkiaImageEncoder : IImageEncoder
{
    public SkiaImageEncoder(ContainerFormat format) => Format = format;
    public ContainerFormat Format { get; }

    public void Encode(
        Stream output,
        IReadOnlyList<FrameBitmap> frames,
        ArchiveMetadata metadata,
        int delayMilliseconds = FrameGeometry.AnimationDelayMilliseconds)
    {
        MultiFrameContainer.Write(output, Format, frames, metadata, delayMilliseconds);
    }
}

public sealed class SkiaImageDecoder : IImageDecoder
{
    public ContainerFormat Format { get; private set; } = ContainerFormat.Png;

    public DecodeContainerResult Decode(Stream input)
    {
        var result = MultiFrameContainer.Read(input);
        Format = Guess(input, result);
        return result;
    }

    private static ContainerFormat Guess(Stream input, DecodeContainerResult result)
    {
        // Prefer delay/metadata; sniff was done in MultiFrameContainer
        return ContainerFormat.Png;
    }
}

public sealed class SkiaAutoImageDecoder : IImageDecoder
{
    public ContainerFormat Format { get; private set; } = ContainerFormat.Png;

    public DecodeContainerResult Decode(Stream input)
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
        if (n >= 8 && magic[0] == 0x89 && magic[1] == 0x50)
            Format = ContainerFormat.Png;
        else if (n >= 12 && magic[0] == (byte)'R' && magic[8] == (byte)'W')
            Format = ContainerFormat.Webp;

        return MultiFrameContainer.Read(input);
    }
}

public static class ImageArchiveCodecs
{
    public static IImageEncoder CreateDefaultEncoder(ContainerFormat format) => new SkiaImageEncoder(format);
    public static IImageDecoder CreateDefaultDecoder() => new SkiaAutoImageDecoder();
}
