# ImageArchive Format Specification

**RFC Version**: 1.0.0  
**Status**: Final  
**Date**: 2026-07-14  
**License**: GNU GPL v3.0

## 1. Abstract

ImageArchive is a container format that embeds an arbitrary binary archive (Git repository + working tree, zip, tarball, or any other stream of bytes) inside a standard lossless animated image (APNG or animated WebP).  

Every frame is a fixed-size 1024×1024 image that is fully human-readable and machine-verifiable. The payload is stored as raw RGB pixel data between a fixed 50-pixel visual header and a fixed 50-pixel visual footer.

## 2. Design Goals

- Remain a valid, viewable image in any modern browser or image viewer
- Make every single frame self-describing
- Carry complete provenance and integrity information
- Support both Git repositories (with history + working tree) and ordinary archives
- Be completely open and free software (GPL-3.0)

## 3. Frame Geometry (Mandatory)

Every frame **must** be exactly:

```
Width:  1024 pixels
Height: 1024 pixels
```

Layout of each frame (top → bottom):

| Region   | Rows          | Height | Purpose                          |
|----------|---------------|--------|----------------------------------|
| Header   | 0 – 49        | 50 px  | Free-form visual content + QR    |
| Data     | 50 – 973      | 924 px | Raw RGB payload                  |
| Footer   | 974 – 1023    | 50 px  | Frame number, SHA, two QR codes  |

**Bytes of data capacity per frame**  
`1024 × 924 × 3 = 2,838,528 bytes` (~2.71 MiB)

## 4. Pixel Encoding of Payload

- Only the **RGB** channels are used for data (3 bytes per pixel).
- Alpha channel (if present) must be fully opaque (255).
- Within the data region the bytes are written left-to-right, top-to-bottom.
- Frames are concatenated in sequential order (frame 0, frame 1, …) to form one continuous byte stream.
- The image encoder is responsible for packing the stream into the pixel data; the core library only supplies the continuous byte stream.

## 5. Visual Header (rows 0–49)

- Background: pure white (`#FFFFFF`)
- The rightmost 50×50 pixels are reserved for a QR code (47×47 code + margins)
- The remaining area (left of the QR) is completely free-form.  
  It may contain:
  - Text
  - A single static image
  - A sequence of numbered images (one per frame, cycling if necessary)
- The free-form content is rendered first; the QR code is then composited on top, right-aligned.

### Header QR Code (top-right)

- Size: 47×47 pixels
- Margins: 2 px top & right, 1 px bottom & left → total 50×50
- Margin colour: white
- Content: **user-defined**  
  Intended use: canonical commit URL of the Git repository contained in the payload.  
  For non-Git archives the encoder may put any URL or leave it empty.

## 6. Visual Footer (rows 974–1023)

- Background: pure white
- Layout (left → right):

  1. **Left QR code** (50×50 with same margins as above)  
     Content: hexadecimal SHA-256 of the **raw data bytes of this frame only**

  2. **Centered text block** (black, system sans-serif, 8 pt, standard leading)  
     Line 1: `Frame N of M`  
     Line 2: the same SHA-256 (hex)

  3. **Right QR code** (50×50, right-aligned with identical margins)  
     Content: canonical URL of the **commit of the ImageArchive tool** that produced this file

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

Additional free-form text chunks are permitted.

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
