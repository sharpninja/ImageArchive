# ImageArchive.Cli

Global **.NET tool** (`imga`) for encoding and decoding [ImageArchive](https://www.nuget.org/packages/ImageArchive) containers: multi-frame **APNG** or **animated WebP** images that carry archives with visual chrome, QR codes, and SHA-256 integrity.

**Tool command:** `imga`  
**Targets:** `net8.0`, `net9.0`, `net10.0` (tool host picks a matching TFM)

## Install

```bash
dotnet tool install -g ImageArchive.Cli
```

Update:

```bash
dotnet tool update -g ImageArchive.Cli
```

## Example output

Header banner (512×67):

![ImageArchive header banner](images/readme-header-banner.png)

Sample multi-frame archive (512×512 animated WebP, dark chrome; open in a WebP-capable viewer to step frames):

![ImageArchive sample](images/origin-head.webp)

## Commands

### `imga init`

Write a blank, schema-valid manifest for hand-editing.

```bash
imga init
imga init --output path/to/manifest.json --force
```

Alias: `imga manifest`

### `imga encode`

Encode from a manifest file.

```bash
imga encode --manifest path/to/manifest.json
imga encode --manifest path/to/manifest.json --dark --width 1024
```

| Flag | Meaning |
|------|---------|
| `--manifest <path>` | Required. Path to JSON manifest |
| `--dark` | Invert header/footer chrome (QR stays black-on-white). Forces `dark: true` in embedded manifest |
| `--width <n>` | Square frame edge **512–1440** (default 1024). Overrides manifest `frameWidth` |

Output path and format (`png` / `webp`) come from the manifest `output` section.

Optional environment variable:

| Variable | Purpose |
|----------|---------|
| `IMAGEARCHIVE_TOOL_COMMIT_URL` | Footer right QR payload (tool/commit URL) |

### `imga decode`

```bash
imga decode --input archive.png --output extracted.bin
```

| Flag | Meaning |
|------|---------|
| `--input <image>` | Required. APNG or animated WebP ImageArchive |
| `--output <path>` | Required. Where to write the recovered archive bytes |

## Exit codes

| Code | Meaning |
|-----:|---------|
| 0 | Success |
| 1 | Validation / usage |
| 2 | Integrity failure |
| 3 | I/O or archive source |
| 4 | Unexpected internal error |

## Manifest tips

Edit at least:

- `archive.source` (and `type` / `mimeType`)
- `output.path` / `output.format`
- `header` text or image path
- optional `dark`, `frameWidth` (512–1440)

Capacity per frame: `width × (width − 134) × 3` bytes (default width 1024 → **2,734,080** bytes).

Example starter: see the [repository example manifest](https://github.com/sharpninja/ImageArchive/blob/main/examples/example-manifest.json).

## Library API

For in-process encode/decode without the tool, use the **ImageArchive** library package:

```bash
dotnet add package ImageArchive
```

## Docs and license

- Format: [ImageArchive RFC](https://github.com/sharpninja/ImageArchive/blob/main/docs/ImageArchive-RFC.md)
- Source: https://github.com/sharpninja/ImageArchive
- License: **GPL-3.0-only**
