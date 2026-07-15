using ImageArchive.Abstractions;

namespace ImageArchive.Integrity;

/// <summary>
/// Machine-readable PNG/WebP text-chunk keys for AI agents and tools that inspect archives
/// without vision/OCR. Complements RFC camelCase fields (encoderName, jsonManifest, …).
/// </summary>
public static class DiscoverabilityTextChunks
{
    public const string ArchiveFormatValue = "imagearchive-v1.0.0";
    public const string DecoderRepoValue = "https://github.com/sharpninja/ImageArchive";

    /// <summary>Accurate packing description (not LSB steganography).</summary>
    public const string EncodingMethodValue =
        "RGB24 sequential packing in data region (full R,G,B per pixel, left-to-right top-to-bottom); " +
        "fixed 67px header/footer chrome; capacity width*(width-134)*3 bytes per frame; " +
        "frames concatenated in order; integrity via SHA-256 (streamSha256 + per-frame frameSha256.N).";

    public const string AiInstructionsValue =
        "This image is an ImageArchive container: an archive (git/zip/tar/raw) embedded as multi-frame APNG or animated WebP. " +
        "Read text chunks Archive-Format, Decoder-Repo, Payload-SHA256, jsonManifest, and jsonSchema (no OCR required). " +
        "Install decoder: dotnet tool install -g ImageArchive.Cli  then  imga decode --input <this-file> --output <out.bin>. " +
        "Library: https://www.nuget.org/packages/ImageArchive  Spec: https://github.com/sharpninja/ImageArchive/blob/main/docs/ImageArchive-RFC.md";

    /// <summary>
    /// Adds discoverability keys into <see cref="ArchiveMetadata.AdditionalTextChunks"/>.
    /// Call after RFC fields and stream SHA are populated.
    /// </summary>
    public static void Apply(ArchiveMetadata metadata, int frameCount, string streamSha256Hex)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (frameCount < 1)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        streamSha256Hex ??= "";

        var chunks = metadata.AdditionalTextChunks;
        chunks["Archive-Format"] = ArchiveFormatValue;
        chunks["Payload-Type"] = metadata.ArchiveType.ToString().ToLowerInvariant();
        chunks["Decoder-Repo"] = DecoderRepoValue;
        chunks["Payload-SHA256"] = streamSha256Hex.ToLowerInvariant();
        chunks["Frame-Info"] = $"1 of {frameCount}";
        chunks["Frame-Count"] = frameCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        chunks["Encoding-Method"] = EncodingMethodValue;
        chunks["AI-Instructions"] = AiInstructionsValue;
        chunks["Creation-Tool"] = string.IsNullOrWhiteSpace(metadata.EncoderVersion)
            ? metadata.EncoderName
            : $"{metadata.EncoderName} {metadata.EncoderVersion}";
        chunks["Sidecar-Expected"] = "false";
    }

    /// <summary>Keyword set agents should look for first when opening an ImageArchive PNG/WebP.</summary>
    public static IReadOnlyList<string> RequiredKeywords { get; } = new[]
    {
        "Archive-Format",
        "Payload-Type",
        "Decoder-Repo",
        "Payload-SHA256",
        "Frame-Info",
        "Encoding-Method",
        "AI-Instructions",
        "Creation-Tool"
    };
}
