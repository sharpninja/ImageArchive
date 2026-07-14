using ImageArchive.Abstractions;

namespace ImageArchive.Manifest;

/// <summary>Creates a schema-valid starter manifest for <c>imga init</c>.</summary>
public static class BlankManifest
{
    /// <summary>Placeholder 64-hex SHA (replace with real tool binary hash for production encodes).</summary>
    public const string PlaceholderSha256 = "0000000000000000000000000000000000000000000000000000000000000000";

    public static ImageArchiveManifest Create()
    {
        // Deserialize the canonical template so enums/strings match schema exactly.
        return ManifestJson.Deserialize(ToJson());
    }

    /// <summary>Pretty-printed JSON suitable for hand-editing (schema-valid).</summary>
    public static string ToJson() =>
        """
        {
          "version": "1.0.0",
          "encoder": {
            "name": "ImageArchive.Cli",
            "version": "1.0.0",
            "sha256": "0000000000000000000000000000000000000000000000000000000000000000"
          },
          "archive": {
            "type": "raw",
            "mimeType": "application/octet-stream",
            "source": "./payload.bin"
          },
          "output": {
            "path": "./archive.png",
            "format": "png"
          },
          "header": {
            "type": "text",
            "text": "ImageArchive",
            "qrCode": {
              "content": "",
              "enabled": true
            }
          },
          "frames": [
            {}
          ]
        }
        """.Replace("\r\n", "\n") + "\n";
}
