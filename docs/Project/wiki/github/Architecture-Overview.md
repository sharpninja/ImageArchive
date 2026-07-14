# Architecture overview

ImageArchive is a .NET multi-target library and CLI that embeds an archive byte stream in multi-frame **APNG** or **animated WebP** images per RFC 1.0.0.

## Layers

1. **Facade** (`ImageArchiveEncoder` / `ImageArchiveDecoder`): validate manifest, load/process archive, pack frames, write/read container, verify integrity.
2. **Geometry and packing** (`FrameGeometry`, internal `PixelPacker`): fixed 1024×1024 layout; RGB data region 2,838,528 bytes.
3. **Rendering** (internal `FrameRenderer`): header free-form content, footer text, QR cells (50×50 with margins T=2,R=2,B=1,L=1).
4. **Codecs** (`ImageArchive.Codecs.Skia`): SkiaSharp raster path; APNG mux/demux; animated WebP RIFF mux/demux.
5. **Manifest** (`JsonSchemaManifestValidator`, `ManifestJson`): draft 2020-12 schema including `streamSha256`.
6. **Sources** (`DefaultArchiveSourceLoader`): raw/zip/tar files; git as compressed tar of `.git` + worktree (excludes `bin`/`obj`).

## Integrity

- Per-frame SHA-256 of the data region (zero padding included), stored as footer left QR, footer Line2, and `frameSha256.{i}` text metadata.
- Whole-stream SHA-256 of concatenated padded frame data in `jsonManifest.streamSha256`.
- Decode is fail-closed when metadata or QR hashes disagree with recomputed values.

## CLI

`ImageArchive.Cli` exposes `encode --manifest` and `decode --input --output` with documented exit codes (0-4).
