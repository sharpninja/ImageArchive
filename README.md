# ImageArchive

**Embed an entire archive (Git repository, zip, tarball, or any binary data) inside a standard lossless animated image (APNG or animated WebP).**

Every frame is a self-contained, human-readable 1024×1024 image that includes:

- A free-form visual header (text, logo, or image)
- An optional user-defined QR code (intended for the canonical commit URL)
- A fixed footer with:
  - Left QR code → SHA-256 of that frame’s data chunk
  - Centered text → `Frame X of Y` + the frame SHA
  - Right QR code → Canonical commit of the **ImageArchive** tool itself

The actual payload is stored as pure RGB pixel data between the 50-pixel header and 50-pixel footer.

## Features

- **Fixed geometry**: Every frame is exactly 1024×1024
- **Data capacity**: 2,838,528 bytes (~2.71 MiB) of raw data per frame
- **Unlimited frames** (limited only by the container format)
- **Pluggable image encoders** (PNG / WebP today, more later)
- **JSON manifest** driven encoding
- **Full metadata** embedded as text chunks (`jsonSchema`, `jsonManifest`, `mimeType`, encoder info, etc.)
- **C# library** exposing clean `Stream` interfaces for reading and writing
- **CLI** that can encode/decode using a single JSON manifest file
- Licensed under **GPL-3.0**

## Quick Start

```bash
# Encode from a JSON manifest
dotnet run --project src/ImageArchive.Cli -- encode --manifest my-archive.json --output archive.apng

# Decode
dotnet run --project src/ImageArchive.Cli -- decode --input archive.apng --output ./extracted
```

## Specification

The complete, versioned specification lives in:

- [docs/ImageArchive-RFC.md](docs/ImageArchive-RFC.md)

The JSON Schema that describes a complete build is:

- [schema/imagearchive-schema.json](schema/imagearchive-schema.json)

## Project Structure

```
src/
├── ImageArchive/               # Core library (Streams, models, pluggable encoders)
└── ImageArchive.Cli/           # Command-line tool
schema/
└── imagearchive-schema.json
examples/
└── example-manifest.json
docs/
└── ImageArchive-RFC.md
```

## License

GNU General Public License v3.0 – see [LICENSE](LICENSE).
