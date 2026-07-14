namespace ImageArchive.Abstractions;

/// <summary>RFC text-chunk metadata (camelCase keys).</summary>
public sealed class ArchiveMetadata
{
    public string EncoderName { get; set; } = "";
    public string EncoderVersion { get; set; } = "";
    public string EncoderSha256 { get; set; } = "";
    public string MimeType { get; set; } = "";
    public ArchiveType ArchiveType { get; set; } = ArchiveType.Raw;
    public string JsonSchema { get; set; } = "";
    public string JsonManifest { get; set; } = "";
    public string? SourceUrl { get; set; }
    public string? CommitHash { get; set; }
    public string? OriginalFileName { get; set; }
    public IDictionary<string, string> AdditionalTextChunks { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
