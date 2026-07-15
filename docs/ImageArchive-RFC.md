# ImageArchive Format Specification

**RFC Version**: 1.0.0
**Status**: Final
**Date**: 2026-07-14
**License**: GNU GPL v3.0

## 1. Abstract

ImageArchive is a container format that embeds an arbitrary binary archive (Git repository + working tree, zip, tarball, or any other stream of bytes) inside a standard lossless animated image (APNG or animated WebP).

Every frame is a **square** image that is fully human-readable and machine-verifiable. The **default** edge length is **1024** pixels; encoders **must** allow **512–1440** inclusive (manifest `frameWidth` and/or encode option / CLI `--width`). The payload is stored as raw RGB pixel data between a fixed 67-pixel visual header and a fixed 67-pixel visual footer.

## 2. Design Goals

- Remain a valid, viewable image in any modern browser or image viewer
- Make every single frame self-describing
- Carry complete provenance and integrity information
- Support both Git repositories (with history + working tree) and ordinary archives
- Be completely open and free software (GPL-3.0)

## 3. Frame Geometry (Mandatory)

Every frame **must** be square:

```
Width:  W pixels   (W ∈ [512, 1440], default 1024)
Height: W pixels
```

Chrome heights are fixed; the data region scales with `W`:

| Region   | Rows (0-based)        | Height      | Purpose                          |
|----------|-----------------------|-------------|----------------------------------|
| Header   | 0 – 66                | 67 px       | Free-form visual content + QR    |
| Data     | 67 – (W − 68)         | W − 134 px  | Raw RGB payload                  |
| Footer   | (W − 67) – (W − 1)    | 67 px       | Frame number, SHA, two QR codes  |

**Default layout (W = 1024)**

| Region   | Rows          | Height |
|----------|---------------|--------|
| Header   | 0 – 66        | 67     |
| Data     | 67 – 956      | 890    |
| Footer   | 957 – 1023    | 67     |

**Bytes of data capacity per frame**
`W × (W − 134) × 3`

Default: `1024 × 890 × 3 = 2,734,080 bytes` (~2.61 MiB)

## 4. Pixel Encoding of Payload

- Only the **RGB** channels are used for data (3 bytes per pixel).
- Alpha channel (if present) must be fully opaque (255).
- Within the data region the bytes are written left-to-right, top-to-bottom.
- Frames are concatenated in sequential order (frame 0, frame 1, …) to form one continuous byte stream.
- The image encoder is responsible for packing the stream into the pixel data; the core library only supplies the continuous byte stream.

## 5. Visual Header (rows 0–66)

- Background: pure white (`#FFFFFF`)
- The rightmost 67×67 pixels are reserved for a QR code (65×65 modules + 1 px margin on every side)
- The remaining area (left of the QR) is completely free-form.
  It may contain:
  - Text
  - A single static image
  - A sequence of numbered images (one per frame, cycling if necessary)
- The free-form content is rendered first; the QR code is then composited on top, right-aligned.

### Header QR Code (top-right)

- Module area: 65×65 pixels (1 module = 1 pixel)
- Margins: 1 px on all sides (top, right, bottom, left) → total cell 67×67
- Margin colour: white
- Content: **user-defined**
  Intended use: repository root URL at the archived commit (e.g. `https://github.com/owner/repo/tree/<sha>`).
  For non-Git archives the encoder may put any URL or leave it empty.

## 6. Visual Footer (rows W−67 … W−1; default 957–1023)

- Background: pure white (or inverted when dark chrome is enabled; QR cells remain white)
- Layout (left → right):

  1. **Left QR code** (67×67 with same margins as above)
     Content: hexadecimal SHA-256 of the **raw data bytes of this frame only**

  2. **Centered text block** (black, system sans-serif, 8 pt, standard leading)
     Line 1: `Frame N of M`
     Line 2: the same SHA-256 (hex)

  3. **Right QR code** (67×67, right-aligned with identical margins)
     Content: repository-root-at-commit URL for the **ImageArchive tool** revision that produced this file

## 7. Metadata (Text Chunks)

All structured metadata lives exclusively in the image format’s text chunks (tEXt / iTXt for PNG, EXIF or XMP for WebP).

Field names are **camelCase**:

| Field            | Description                                      | Required |
|------------------|--------------------------------------------------|----------|
| `encoderName`    | Name of the encoding tool                        | Yes      |
| `encoderVersion` | Version of the encoding tool                     | Yes      |
| `encoderSha256`  | SHA-256 of the encoding tool binary              | Yes      |
| `mimeType`       | MIME type of the original archive content        | Yes      |
| `sourceUrl`      | Canonical URL of the source repository (if any)  | No       |
| `commitHash`     | Commit hash of the source repository             | No       |
| `archiveType`    | One of: `git`, `zip`, `tar`, `raw`               | Yes      |
| `originalFileName`| Suggested filename for the extracted archive    | No       |
| `jsonSchema`     | The complete JSON Schema that describes this format (embedded) | Yes |
| `jsonManifest`   | The exact JSON manifest that was used to build this file | Yes |

### Manifest field `dark` (optional)

Boolean, default `false`. When `true`, header and footer chrome use inverted colors (black background, light text). QR codes remain standard black modules on white cells so phone scanners can read them. The data-region payload is unchanged. The CLI flag `--dark` forces dark chrome regardless of the manifest value.

Additional free-form text chunks are permitted.

### Discoverability text chunks (recommended for agents)

Encoders **should** also emit the following keys (PNG `tEXt` / equivalent WebP metadata) so AI agents can decode without vision or OCR. See `docs/ai-agent-discovery.md`.

| Keyword | Description |
|---------|-------------|
| `Archive-Format` | Format id, e.g. `imagearchive-v1.0.0` |
| `Payload-Type` | Same as `archiveType` (`git` / `zip` / `tar` / `raw`) |
| `Decoder-Repo` | URL of the decoder/spec repository |
| `Payload-SHA256` | Lowercase hex of the concatenated data-region stream (same as `streamSha256`) |
| `Frame-Info` | e.g. `1 of N` |
| `Frame-Count` | Decimal frame count |
| `Encoding-Method` | Short accurate description of RGB24 data-region packing |
| `AI-Instructions` | One or two sentences with next steps for an automated agent |
| `Creation-Tool` | Encoder product name and version |
| `Sidecar-Expected` | `true` or `false` |

**Note:** Exporting a single animation frame as a new PNG often **strips** text and APNG control chunks. Agents must open the original multi-frame container.

## 8. Per-Frame Integrity

For every frame the encoder **must**:

1. Collect the exact raw bytes that will be written into that frame’s data region.
2. Compute SHA-256 of those bytes.
3. Embed the hexadecimal digest both as text in the footer and as the payload of the left QR code.

The decoder **must** recompute the SHA-256 of each frame’s data region and reject the file if any frame fails the check.

## 9. Overall Integrity

After concatenating all frames into a single continuous stream, the decoder should compute a final SHA-256 of the entire stream.
This value is recorded in the `jsonManifest` (and therefore also in the text chunk `jsonManifest`).

## 10. JSON Manifest

Encoding is driven by a single JSON document that conforms to the schema:

```
schema/imagearchive-schema.json
```

The complete manifest that was used to produce a given ImageArchive is always embedded as the text chunk `jsonManifest`.

## 11. Animation Timing

When the container format supports animation (APNG / animated WebP):

- Frame delay = **60 000 ms** (exactly one frame per minute)
- This delay is purely visual; the decoder ignores timing and treats the frames as a pure byte stream.

## 12. Supported Container Formats

- APNG (image/png)
- Animated WebP (image/webp)

The MIME type of the ImageArchive file itself remains that of the chosen container.

## 13. Extensibility

New image formats can be added by implementing the `IImageEncoder` / `IImageDecoder` interfaces.
No changes to the core data layout or metadata model are required.

## 14. Security Considerations

- No encryption is performed by the format itself.
  Users may pipe the input stream through any preprocessor (e.g. `gpg --encrypt`) and the output stream through a matching post-processor.
- The only cryptographic primitives used by the format are SHA-256 hashes for integrity.

## 15. Reference Implementation

A complete open-source reference implementation written in C# is provided in this repository:

- Library: `src/ImageArchive`
- CLI: `src/ImageArchive.Cli`

Both are licensed under GPL-3.0.

---

*This document is the canonical definition of ImageArchive version 1.0.0.*
