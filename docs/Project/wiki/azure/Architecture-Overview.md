# Architecture overview

ImageArchive is a .NET multi-target library and CLI that embeds an archive byte stream in multi-frame **APNG** or **animated WebP** images per RFC 1.0.0.

## Layers

1. **Facade** (`ImageArchiveEncoder` / `ImageArchiveDecoder`): validate manifest, load/process archive, pack frames, write/read container, verify integrity.
2. **Geometry and packing** (`FrameGeometry`, internal `PixelPacker`): square frames; default **1024×1024**, range **512–1440** (`frameWidth` / `--width` / `ImageArchiveEncodeOptions.FrameWidth`). Header/footer chrome **67 px** each; data height = `width − 134`; capacity = `width × (width − 134) × 3` bytes (default **2,734,080**). Active width is `AsyncLocal` via `FrameGeometry.Use`.
3. **Rendering** (internal `FrameRenderer`): header free-form content (text, image, or folder), footer text, QR cells (67×67 with 65×65 modules and 1 px margins). Header **images** scale into free-form width only (`width − 67`); the right QR cell is never painted by free-form content. Optional **dark** chrome inverts header/footer bands and text ink; QR modules stay black-on-white.
4. **Codecs** (`ImageArchive.Codecs.Skia`): SkiaSharp raster path; APNG mux/demux; animated WebP RIFF mux/demux. Canvas dimensions follow active `FrameGeometry.Width`.
5. **Manifest** (`JsonSchemaManifestValidator`, `ManifestJson`, `BlankManifest`): draft 2020-12 schema including `streamSha256`, `dark`, `frameWidth`; blank starter templates for CLI `init`.
6. **Sources** (`DefaultArchiveSourceLoader`): raw/zip/tar files; git as compressed tar of `.git` + worktree (excludes `bin`/`obj`/`.vs`/`node_modules`).

## Integrity

- Per-frame SHA-256 of the data region (zero padding included), stored as footer left QR, footer Line2, and `frameSha256.{i}` text metadata.
- Whole-stream SHA-256 of concatenated padded frame data in `jsonManifest.streamSha256`.
- Decode is fail-closed when metadata or QR hashes disagree with recomputed values. Decoder scopes geometry to the first frame width (validated 512–1440).

## CLI

Package `ImageArchive.Cli` ships as a .NET tool with command name **`imga`**. Commands:

| Command | Purpose |
|---------|---------|
| `imga init [--output path] [--force]` | Write a blank schema-oriented manifest (alias: `manifest`) |
| `imga encode --manifest path [--dark] [--width n]` | Encode per manifest; CLI flags override `dark` / `frameWidth` |
| `imga decode --input image --output path` | Decode archive bytes |

Exit codes: 0 success, 1 validation/usage, 2 integrity, 3 I/O or archive source, 4 unexpected.

Optional env: `IMAGEARCHIVE_TOOL_COMMIT_URL` for the footer right QR.

## Packaging

| Package | NuGet README | Notes |
|---------|--------------|-------|
| `ImageArchive` | `src/ImageArchive/README.md` | Library API; ships sample images under `images/` |
| `ImageArchive.Cli` | `src/ImageArchive.Cli/README.md` | PackAsTool; strips SkiaSharp `.pdb` from publish to stay under nuget.org size limit |

Repo root `README.md` is project-oriented (build, tests, Nuke). Sample assets default to **512×512** animated WebP (`docs/images/origin-head.webp`) with a **512×67** PNG banner.

## Runtime stack (locked pins)

| Area | Package | Version notes |
|------|---------|---------------|
| Raster / containers | SkiaSharp | **3.119.4** (not 4.x while ZXing Skia binding targets 3.119.x APIs) |
| QR encode / decode | QRCoder, ZXing.Net, ZXing.Net.Bindings.SkiaSharp | 1.8.0 / 0.16.11 / 0.16.22 |
| Schema | JsonSchema.Net | 9.2.2 |
| JSON | System.Text.Json | 10.0.10 |
| CLI helper | System.CommandLine | 2.0.10 (argv currently hand-parsed in `Program.cs`) |
| Tests | xunit.v3, runner.visualstudio, Microsoft.NET.Test.Sdk | 3.2.2 / 3.1.5 / 18.8.1 |
| Build | Nuke.Common | 10.1.0; build project TFM **net10.0** (not listed in `ImageArchive.slnx`) |
| Versioning | GitVersion.MsBuild / GitVersion.Tool | 6.8.2 |

## Solution layout

```
ImageArchive.slnx                 # product projects only (no _build)
src/ImageArchive/                 # library + package README
src/ImageArchive.Cli/             # .NET tool (imga) + package README
tests/ImageArchive.UnitTests/
tests/ImageArchive.IntegrationTests/
build/                            # Nuke host (_build.csproj); run via build.ps1 / build.sh
schema/imagearchive-schema.json
docs/images/                      # sample banner (PNG) + origin-head animated WebP
docs/ai-agent-discovery.md        # machine-readable metadata for agents
scripts/New-OriginHeadImageArchive.ps1
```

## Nuke host bootstrap

`build.ps1` rebuilds the Nuke host only when build sources have the Windows Archive attribute set (or the binary is missing), then clears Archive flags and runs `build\bin\Debug\_build.exe`. `build.sh` uses source mtime vs host binary. Product Compile never rebuilds `_build` (not in the solution).
