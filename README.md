# ImageArchive

**Embed any archive (Git repository, zip, tar, or raw bytes) inside a multi-frame APNG or animated WebP image** with a visual header and footer, QR codes, and SHA-256 integrity.

The container format is defined by RFC 1.0.0: [`docs/ImageArchive-RFC.md`](docs/ImageArchive-RFC.md).

## Features

- Fixed **1024×1024** frames (50 px header, 924 px data, 50 px footer)
- Data region capacity **2,838,528** bytes per frame (RGB, zero-padded final frame)
- Header: free-form text, image, or numbered folder round-robin; top-right QR
- Footer: left QR (frame data SHA-256), `Frame N of M` + SHA text, right QR (tool commit URL)
- Required camelCase text metadata (`encoderName`, `jsonManifest`, `jsonSchema`, …) plus `streamSha256`
- Archive types: **raw** (default), git (compressed tar of `.git` + worktree), zip, tar
- Default codec path: **SkiaSharp** raster + real **APNG** / **animated WebP** containers
- Multi-target: `net8.0;net9.0;net10.0`
- Tests: **xUnit v3** (unit + integration, including flagship E2E)

## Solution layout

```
ImageArchive.slnx
src/ImageArchive/                 # library
src/ImageArchive.Cli/             # encode / decode CLI
tests/ImageArchive.UnitTests/
tests/ImageArchive.IntegrationTests/
schema/imagearchive-schema.json
examples/example-manifest.json
```

## Build and test

Requires .NET SDK 8, 9, and 10.

```bash
dotnet build ImageArchive.slnx
dotnet test ImageArchive.slnx
```

Planning package check (docs/schema receipt):

```bash
pwsh -File docs/verify-planning-package.ps1
```

## CLI

```bash
dotnet run --project src/ImageArchive.Cli -- encode --manifest path/to/manifest.json
dotnet run --project src/ImageArchive.Cli -- decode --input archive.png --output extracted.bin
```

Manifest `output.path` and `output.format` (`png` or `webp`) control encode output. Optional env `IMAGEARCHIVE_TOOL_COMMIT_URL` sets the footer right QR.

| Exit code | Meaning |
|----------:|---------|
| 0 | Success |
| 1 | Validation / usage |
| 2 | Integrity failure |
| 3 | I/O or archive source |
| 4 | Unexpected internal error |

## Library (quick start)

```csharp
using ImageArchive;
using ImageArchive.Manifest;

var manifest = ManifestJson.Deserialize(File.ReadAllText("manifest.json"));
using var archive = File.OpenRead(manifest.Archive.Source);
using var output = File.Create(manifest.Output.Path);

new ImageArchiveEncoder().Encode(manifest, archive, output, new ImageArchiveEncodeOptions
{
    WorkingDirectory = Environment.CurrentDirectory,
    ToolCommitUrl = "https://github.com/sharpninja/ImageArchive"
});

using var image = File.OpenRead(manifest.Output.Path);
var result = new ImageArchiveDecoder().Decode(image);
// result.ArchiveStream holds the recovered payload
```

Public entry points include `IImageArchiveEncoder` / `IImageArchiveDecoder`, `IImageEncoder` / `IImageDecoder` (Skia defaults), `IManifestValidator`, and `FrameGeometry` constants.

## Documentation

| Doc | Purpose |
|-----|---------|
| [`docs/ImageArchive-RFC.md`](docs/ImageArchive-RFC.md) | Format specification (canonical) |
| [`schema/imagearchive-schema.json`](schema/imagearchive-schema.json) | Manifest JSON Schema (`streamSha256`) |
| [`docs/qr-payload-limits.md`](docs/qr-payload-limits.md) | QR 47×47 payload limits |
| [`docs/plans/ImageArchive-Implementation-Plan.md`](docs/plans/ImageArchive-Implementation-Plan.md) | Implementation plan / public API |
| [`docs/receipts-requirements-rfc-1.0.0.md`](docs/receipts-requirements-rfc-1.0.0.md) | FR/TR/TEST AC coverage receipt |
| [`docs/Project/`](docs/Project/) | Exported requirements documents |
| [`docs/wiki.yaml`](docs/wiki.yaml) | Wiki publish manifest |

## License

GPL-3.0-only

## Repository

https://github.com/sharpninja/ImageArchive
