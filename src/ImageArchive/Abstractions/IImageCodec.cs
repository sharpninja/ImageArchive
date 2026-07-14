using ImageArchive.Geometry;

namespace ImageArchive.Abstractions;

public interface IImageEncoder
{
    ContainerFormat Format { get; }

    void Encode(
        Stream output,
        IReadOnlyList<FrameBitmap> frames,
        ArchiveMetadata metadata,
        int delayMilliseconds = FrameGeometry.AnimationDelayMilliseconds);
}

public interface IImageDecoder
{
    ContainerFormat Format { get; }
    DecodeContainerResult Decode(Stream input);
}

public sealed class DecodeContainerResult
{
    public required IReadOnlyList<FrameBitmap> Frames { get; init; }
    public required ArchiveMetadata Metadata { get; init; }
    public int? DelayMilliseconds { get; init; }
}
