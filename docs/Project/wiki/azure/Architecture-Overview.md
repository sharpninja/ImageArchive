# Architecture overview

ImageArchive is a .NET multi-target library and CLI that embeds an archive byte stream in multi-frame **APNG** or **animated WebP** images per RFC 1.0.0.

## Layers

1. **Facade** (`ImageArchiveEncoder` / `ImageArchiveDecoder`): validate manifest, load/process archive, pack frames, write/read container, verify integrity.
2. **Geometry and packing** (`FrameGeometry`, internal `PixelPacker`): fixed 1024×1024 layout; RGB data region 2,838,528 bytes.
3. **Rendering** (internal `FrameRenderer`): header free-form content, footer text, QR cells (50×50 with margins T=2,R=2,B=1,L=1).
4. **Codecs** (`ImageArchive.Codecs.Skia`): SkiaSharp raster path; APNG mux/demux; animated WebP RIFF mux/demux.
5. **Manifest** (`JsonSchemaManifestValidator`, `ManifestJson`, `BlankManifest`): draft 2020-12 schema including `streamSha256`; blank starter templates for CLI `init`.
6. **Sources** (`DefaultArchiveSourceLoader`): raw/zip/tar files; git as compressed tar of `.git` + worktree (excludes `bin`/`obj`).

## Integrity

- Per-frame SHA-256 of the data region (zero padding included), stored as footer left QR, footer Line2, and `frameSha256.{i}` text metadata.
- Whole-stream SHA-256 of concatenated padded frame data in `jsonManifest.streamSha256`.
- Decode is fail-closed when metadata or QR hashes disagree with recomputed values.

## CLI

Package `ImageArchive.Cli` ships as a .NET tool with command name **`imga`**. Commands:

| Command | Purpose |
|---------|---------|
| `imga init [--output path] [--force]` | Write a blank schema-oriented manifest (alias: `manifest`) |
| `imga encode --manifest path` | Encode per manifest |
| `imga decode --input image --output path` | Decode archive bytes |

Exit codes: 0 success, 1 validation/usage, 2 integrity, 3 I/O or archive source, 4 unexpected.

## Runtime stack (locked pins)

| Area | Package | Version notes |
|------|---------|---------------|
| Raster / containers | SkiaSharp | **3.119.4** (not 4.x while ZXing Skia binding targets 3.119.x APIs) |
| QR encode / decode | QRCoder, ZXing.Net, ZXing.Net.Bindings.SkiaSharp | 1.8.0 / 0.16.11 / 0.16.22 |
| Schema | JsonSchema.Net | 9.2.2 |
| JSON | System.Text.Json | 10.0.10 |
| CLI helper | System.CommandLine | 2.0.10 (argv currently hand-parsed in `Program.cs`) |
| Tests | xunit.v3, runner.visualstudio, Microsoft.NET.Test.Sdk | 3.2.2 / 3.1.5 / 18.8.1 |
| Build | Nuke.Common | 10.1.0; build project TFM **net10.0** |

## Solution layout

```
ImageArchive.slnx
src/ImageArchive/
src/ImageArchive.Cli/
tests/ImageArchive.UnitTests/
tests/ImageArchive.IntegrationTests/
build/                          # Nuke (_build.csproj net10.0)
schema/imagearchive-schema.json
```
