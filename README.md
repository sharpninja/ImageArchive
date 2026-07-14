# ImageArchive

**Embed any archive (Git repository, zip, tar, raw binary) inside a standard multi-frame PNG or WebP image.**

ImageArchive turns a data stream into a visually identifiable, self-describing animated image.  
Every frame contains:

- A free-form 50 px header (text, logo, or per-frame images)
- A 50 px footer with dual QR codes + frame index + per-frame SHA-256
- Pure RGB data region that holds the payload

The format is fully specified in [`docs/ImageArchive-RFC.md`](docs/ImageArchive-RFC.md).

## Features

- Fixed 1024×1024 frames
- 50 px header + 50 px footer (exact layout defined in the RFC)
- Three QR codes per frame (user-defined top-right, frame SHA bottom-left, source URL bottom-right)
- All metadata stored in PNG/WebP text chunks (`jsonManifest`, `jsonSchema`, `mimeType`, etc.)
- Pluggable image encoders (`IImageEncoder`)
- C# library that exposes `Stream` for both reading and writing
- Full integration test that clones → tarballs → encodes → validates QR codes & metadata → extracts → compares

## Quick Start

```bash
# Encode
dotnet run --project src/ImageArchive.Cli -- encode --manifest examples/example-manifest.json

# Decode
dotnet run --project src/ImageArchive.Cli -- decode --input archive.png --output ./extracted
```

## Building

```bash
dotnet build
dotnet test
```

## License

GPL-3.0-only

## Repository

https://github.com/sharpninja/ImageArchive
