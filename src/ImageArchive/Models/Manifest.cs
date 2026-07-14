using System.Text.Json.Serialization;

namespace ImageArchive.Models;

public sealed class Manifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("encoder")]
    public EncoderInfo Encoder { get; set; } = new();

    [JsonPropertyName("archive")]
    public ArchiveInfo Archive { get; set; } = new();

    [JsonPropertyName("output")]
    public OutputInfo Output { get; set; } = new();

    [JsonPropertyName("header")]
    public HeaderInfo? Header { get; set; }

    [JsonPropertyName("frames")]
    public List<FrameInfo> Frames { get; set; } = new() { new() };

    [JsonPropertyName("preprocessor")]
    public string? Preprocessor { get; set; }

    [JsonPropertyName("postprocessor")]
    public string? Postprocessor { get; set; }
}

public sealed class EncoderInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "ImageArchive.Cli";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
}

public sealed class ArchiveInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "raw";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/octet-stream";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("commitHash")]
    public string? CommitHash { get; set; }

    [JsonPropertyName("originalFileName")]
    public string? OriginalFileName { get; set; }
}

public sealed class OutputInfo
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "archive.apng";

    [JsonPropertyName("format")]
    public string Format { get; set; } = "png"; // "png" or "webp"
}

public sealed class HeaderInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text"; // text | image | folder

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("folderPath")]
    public string? FolderPath { get; set; }

    [JsonPropertyName("qrCode")]
    public QrCodeInfo? QrCode { get; set; }
}

public sealed class QrCodeInfo
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public sealed class FrameInfo
{
    [JsonPropertyName("header")]
    public HeaderInfo? Header { get; set; }
}
