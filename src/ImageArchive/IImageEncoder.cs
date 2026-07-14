using ImageArchive.Models;

namespace ImageArchive;

/// <summary>
/// Pluggable image format encoder/decoder.
/// </summary>
public interface IImageEncoder
{
    string FormatName { get; }          // "png" or "webp"
    string MimeType { get; }

    /// <summary>
    /// Encode a continuous data stream into a multi-frame image.
    /// The implementation is responsible for packing RGB bytes into the data region of each 1024x1024 frame.
    /// </summary>
    Task EncodeAsync(
        Stream dataStream,
        Manifest manifest,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decode a multi-frame image back into a continuous data stream.
    /// Also extracts all text metadata and verifies per-frame SHA-256.
    /// </summary>
    Task<DecodeResult> DecodeAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}

public sealed class DecodeResult
{
    public required Stream DataStream { get; init; }
    public required Manifest Manifest { get; init; }
    public required IReadOnlyList<string> FrameSha256s { get; init; }
}
