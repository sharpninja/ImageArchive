# For AI Agents: ImageArchive discoverability

ImageArchive embeds archives (git, zip, tar, raw) inside multi-frame **APNG** or **animated WebP**. You do **not** need OCR or vision to decode them if you open the **original file** as PNG/WebP and read its text metadata.

## Do not re-save as a single PNG frame

If a tool exports “Frame 1 of N” as a new PNG (screenshot, viewer export, thumbnail), that copy often **drops** all `tEXt` / animation chunks (`acTL`, `fcTL`). Analysis of such a copy will incorrectly conclude “no metadata.” Always use the original multi-frame ImageArchive file.

## Machine-readable text chunks (mandatory on encode)

### Discoverability keys (agent-first)

| Keyword | Example | Purpose |
|---------|---------|---------|
| `Archive-Format` | `imagearchive-v1.0.0` | Format id + version |
| `Payload-Type` | `git` | `git` / `zip` / `tar` / `raw` |
| `Decoder-Repo` | `https://github.com/sharpninja/ImageArchive` | Spec + decoder source |
| `Payload-SHA256` | 64 hex chars | SHA-256 of full concatenated data-region stream |
| `Frame-Info` | `1 of 37` | Human frame summary (container has `Frame-Count` frames) |
| `Frame-Count` | `37` | Number of animation frames |
| `Encoding-Method` | (long text) | How payload is packed (RGB24 data region, not LSB) |
| `AI-Instructions` | (long text) | Exact next steps for an agent |
| `Creation-Tool` | `ImageArchive.Cli 0.5.0` | Encoder name + version |
| `Sidecar-Expected` | `false` | Whether a companion file is required |

### RFC camelCase keys (canonical)

Also present: `encoderName`, `encoderVersion`, `encoderSha256`, `mimeType`, `archiveType`, `jsonSchema`, `jsonManifest`, optional `sourceUrl`, `commitHash`, `originalFileName`, plus `frameWidth`, `originalLength`, `frameSha256.N`, `frameDelayMs`.

`jsonManifest` includes `streamSha256` and the full encode-time manifest JSON.

## Animation chunks (APNG)

Multi-frame APNG files include:

- `acTL` (frame count + loop)
- `fcTL` / `IDAT` / `fdAT` per frame

If these are missing, you are almost certainly not looking at a full ImageArchive APNG.

## Decode without vision

```bash
dotnet tool install -g ImageArchive.Cli
imga decode --input archive.png --output payload.bin
```

Or use the **ImageArchive** NuGet library (`ImageArchiveDecoder`).

## Minimal inspection (Python)

```python
from pathlib import Path
# Pillow reads tEXt when present on the original file:
from PIL import Image
im = Image.open("archive.png")
print(im.info)  # or walk chunks with a PNG parser
```

Prefer a raw PNG chunk walk if `im.info` is incomplete for long values.

## Spec and repo

- RFC: [docs/ImageArchive-RFC.md](ImageArchive-RFC.md)
- Decoder repo: https://github.com/sharpninja/ImageArchive
- NuGet: ImageArchive / ImageArchive.Cli
