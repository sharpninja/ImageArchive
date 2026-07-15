using ImageArchive.Manifest;

namespace ImageArchive;

public class ImageArchiveException : Exception
{
    public ImageArchiveException(string message) : base(message) { }
    public ImageArchiveException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ManifestValidationException : ImageArchiveException
{
    public ManifestValidationResult Result { get; }
    public ManifestValidationException(ManifestValidationResult result)
        : base(result.Errors.Count > 0 ? result.Errors[0].Message : "Manifest validation failed")
    {
        Result = result;
    }
}

public sealed class IntegrityException : ImageArchiveException
{
    public int? FrameIndex { get; }
    public string? ExpectedHash { get; }
    public string? ActualHash { get; }

    public IntegrityException(string message, int? frameIndex = null, string? expected = null, string? actual = null)
        : base(message)
    {
        FrameIndex = frameIndex;
        ExpectedHash = expected;
        ActualHash = actual;
    }
}

public sealed class QrPayloadTooLongException : ImageArchiveException
{
    public int MaxLength { get; }
    public int ActualLength { get; }

    public QrPayloadTooLongException(int maxLength, int actualLength)
        : base($"QR payload length {actualLength} exceeds max {maxLength} for the configured QR module size.")
    {
        MaxLength = maxLength;
        ActualLength = actualLength;
    }
}

public sealed class ArchiveSourceException : ImageArchiveException
{
    public ArchiveSourceException(string message) : base(message) { }
    public ArchiveSourceException(string message, Exception inner) : base(message, inner) { }
}

public sealed class StreamProcessorException : ImageArchiveException
{
    public StreamProcessorException(string message) : base(message) { }
    public StreamProcessorException(string message, Exception inner) : base(message, inner) { }
}
