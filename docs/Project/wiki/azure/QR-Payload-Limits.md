# QR payload limits (65×65 modules)

ImageArchive places QR codes in a **67×67** pixel cell with **1 px** white margin on every side, leaving a **65×65** module area (RFC §5–6).

## Library choice

| Role | Package (pinned) |
|------|------------------|
| Encode | QRCoder **1.8.0** (ECC level M) |
| Decode | ZXing.Net **0.16.11** + ZXing.Net.Bindings.SkiaSharp **0.16.22** (SkiaSharp **3.119.4** bridge) |

Native matrices from QRCoder are **centered** in the 65×65 module grid when smaller (no scale-up). Matrices larger than 65 modules are rejected.

## Max payload length

**`DefaultQrCodeService.MaxPayloadLength = 200`** characters for general (URL / text) payloads.

| Payload kind | Limit | Notes |
|--------------|------:|-------|
| General text / URL | **200** | Under ECC-M version 10 byte capacity (~213); fits in 65 modules |
| SHA-256 hex only | **64** | Footer left QR; special-cased in `EncodeModules` |

Full GitHub tree URLs with a 40-char commit SHA are about **88** characters and fit without truncation. A full tree URL plus ~100 extra characters (~188) also fits under the 200-char cap and version 10.

### Enforcement

- User-defined header QR content longer than `MaxPayloadLength` throws `QrPayloadTooLongException` (AC-FR-HDR-003-4).
- Tool commit URL (right footer QR) is truncated to `MaxPayloadLength` when necessary.
- If QRCoder produces a module matrix larger than 65, encode fails with the same exception type.

## Geometry checklist

| Constant | Value |
|----------|------:|
| Header / footer height | 67 |
| QR cell | 67×67 |
| QR modules | 65×65 |
| Margins | 1 px all sides |
| Data height | 890 |
| Frame capacity | 2,734,080 bytes |
