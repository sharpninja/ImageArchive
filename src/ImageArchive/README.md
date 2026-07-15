# ImageArchive

.NET library that **embeds archives (Git, zip, tar, or raw bytes) inside multi-frame APNG or animated WebP images**, with header/footer chrome, QR codes, and SHA-256 integrity (RFC 1.0.0).

**Targets:** `net8.0`, `net9.0`, `net10.0`

## Install

```bash
dotnet add package ImageArchive
```

## Example frame

Header banner (512×67):

![ImageArchive header banner](images/readme-header-banner.png)

Sample multi-frame archive (512×512, dark chrome + banner; open as APNG to step frames):

![ImageArchive sample](images/origin-head.png)

## Quick start

```csharp
using ImageArchive;
using ImageArchive.Manifest;

var manifest = ManifestJson.Deserialize(File.ReadAllText("manifest.json"));
using var archive = File.OpenRead(manifest.Archive.Source);
using var output = File.Create(manifest.Output.Path);

new ImageArchiveEncoder().Encode(manifest, archive, output, new ImageArchiveEncodeOptions
{
    WorkingDirectory = Environment.CurrentDirectory,
    ToolCommitUrl = "https://github.com/sharpninja/ImageArchive",
    // Dark = true,          // optional chrome invert (QR stays black-on-white)
    // FrameWidth = 1024,    // optional 512–1440; overrides manifest.frameWidth
});

using var image = File.OpenRead(manifest.Output.Path);
var result = new ImageArchiveDecoder().Decode(image);
// result.ArchiveStream  - recovered payload
// result.Manifest       - embedded jsonManifest
```

## Geometry

| Setting | Value |
|---------|--------|
| Frame shape | Square |
| Default size | 1024×1024 |
| Range | 512–1440 (`frameWidth` / `ImageArchiveEncodeOptions.FrameWidth`) |
| Header / footer | 67 px each |
| Data height | `width − 134` |
| Capacity / frame | `width × (width − 134) × 3` bytes (default **2,734,080**) |
| QR cell | 67×67 (65 modules + 1 px margin) |

## Manifest (library usage)

Pass an `ImageArchiveManifest` (from JSON or constructed in code). Important fields:

- `archive.type` / `archive.source` / `archive.mimeType`
- `output.path` / `output.format` (`png` or `webp`)
- `header` (text, image, or folder) + optional header QR
- `dark`, `frameWidth` (optional)
- Encoder fills `streamSha256` and frame integrity metadata

JSON Schema ships embedded in the package and is validated by `JsonSchemaManifestValidator`.

## Public surface

| Type | Role |
|------|------|
| `ImageArchiveEncoder` / `IImageArchiveEncoder` | Encode payload + chrome into container |
| `ImageArchiveDecoder` / `IImageArchiveDecoder` | Decode container → stream + manifest |
| `ImageArchiveEncodeOptions` | Working dir, tool URL, dark, frame width, injectables |
| `FrameGeometry` | Width, capacity, chrome constants |
| `ManifestJson` / `ImageArchiveManifest` | Serialize/deserialize manifests |
| `IImageEncoder` / `IImageDecoder` | Skia APNG / animated WebP defaults |

## CLI companion

Command-line tool (separate package):

```bash
dotnet tool install -g ImageArchive.Cli
imga encode --manifest path/to/manifest.json
```

## Docs and license

- Format: [ImageArchive RFC](https://github.com/sharpninja/ImageArchive/blob/main/docs/ImageArchive-RFC.md)
- Source: https://github.com/sharpninja/ImageArchive
- License: **GPL-3.0-only**
