# Git-Payload-APNG Metadata & Discoverability Guidelines (ImageArchive)

This document adapts external feedback on **self-describing** git-in-image archives to the **ImageArchive** format (RFC 1.0.0).

**Source feedback:** agent analysis that found no `tEXt` / `acTL` on an example file.  
**Finding on current samples:** a full ImageArchive APNG contains **`acTL`/`fcTL`/`fdAT` plus many `tEXt` chunks**; animated WebP samples (e.g. `docs/images/origin-head.webp`) embed the same logical metadata in WebP text/meta. Absence of those usually means the file was **re-exported as a single-frame image** (screenshot/viewer export), not that the encoder omitted metadata.

Canonical agent guide: [ai-agent-discovery.md](ai-agent-discovery.md).

## Redundancy (required by design)

ImageArchive places the same integrity and provenance data in multiple places:

1. **PNG `tEXt` (or WebP metadata)** — machine-readable without vision  
2. **Visible chrome** — header/footer text for humans and OCR  
3. **QR codes** — footer left (frame SHA), footer right / header (tool or repo URL)

## Encoding method (correct description)

Payload is **not** LSB steganography. It is **full RGB24 packing** in the data region between 67 px header and 67 px footer:

`capacity = width × (width − 134) × 3` bytes per frame.

## Discoverability keywords

Encoders write the keyword set documented in [ai-agent-discovery.md](ai-agent-discovery.md), implemented by `DiscoverabilityTextChunks` in the library.

## AI-Instructions (current text)

See `DiscoverabilityTextChunks.AiInstructionsValue` in source; it points agents to `Decoder-Repo`, NuGet, and `imga decode`.

## Sidecar

`Sidecar-Expected=false` by default: the container is self-contained. A sidecar JSON is optional for tooling convenience, not required for decode.
