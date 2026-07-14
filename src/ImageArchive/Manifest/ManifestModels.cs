using System.Text.Json.Serialization;
using ImageArchive.Abstractions;

namespace ImageArchive.Manifest;

public sealed class ImageArchiveManifest
{
    public string Version { get; set; } = "1.0.0";
    public EncoderManifestSection Encoder { get; set; } = new();
    public ArchiveManifestSection Archive { get; set; } = new();
    public OutputManifestSection Output { get; set; } = new();
    public string? StreamSha256 { get; set; }
    public HeaderManifestSection? Header { get; set; }
    public List<FrameManifestSection> Frames { get; set; } = new() { new() };
    public string? Preprocessor { get; set; }
    public string? Postprocessor { get; set; }
}

public sealed class EncoderManifestSection
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class ArchiveManifestSection
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ArchiveType Type { get; set; } = ArchiveType.Raw;
    public string MimeType { get; set; } = "application/octet-stream";
    public string Source { get; set; } = "";
    public string? SourceUrl { get; set; }
    public string? CommitHash { get; set; }
    public string? OriginalFileName { get; set; }
}

public sealed class OutputManifestSection
{
    public string Path { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContainerFormat Format { get; set; } = ContainerFormat.Png;
}

public sealed class HeaderManifestSection
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HeaderContentType? Type { get; set; }
    public string? Text { get; set; }
    public string? ImagePath { get; set; }
    public string? FolderPath { get; set; }
    public QrCodeManifestSection? QrCode { get; set; }
}

public sealed class QrCodeManifestSection
{
    public string? Content { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class FrameManifestSection
{
    public HeaderManifestSection? Header { get; set; }
}

public sealed class ManifestValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ManifestValidationError> Errors { get; init; } = Array.Empty<ManifestValidationError>();
}

public sealed class ManifestValidationError
{
    public string Path { get; init; } = "";
    public string Message { get; init; } = "";
}
