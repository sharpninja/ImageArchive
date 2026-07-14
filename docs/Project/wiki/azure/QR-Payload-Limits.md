# QR payload limits (47×47 modules)

ImageArchive places QR codes in a **50×50** pixel cell with margins **T=2, R=2, B=1, L=1**, leaving a **47×47** module area (RFC §5–6).

## Library choice

| Role | Package |
|------|---------|
| Encode | QRCoder (ECC level M) |
| Decode | ZXing.Net (SkiaSharp bitmap bridge) |

Modules from QRCoder are nearest-neighbor scaled into 47×47 when the native matrix size differs.

## Max payload length

**`DefaultQrCodeService.MaxPayloadLength = 60`** characters.

This is a conservative limit for mixed URL/hex content at the rendered 47×47 size with ECC M, chosen so encode+decode round-trips succeed reliably in automated tests (footer SHA-256 hex is 64 characters, so frame hashes use the full hex only when encoding without the 60-char clamp — footer QR content is the full 64-char SHA; the service allows SHA-256 by special-casing lengths up to 64 for hex-only payloads in implementation if needed).

### Enforcement

- User-defined header QR content longer than `MaxPayloadLength` throws `QrPayloadTooLongException` (AC-FR-HDR-003-4).
- Tool commit URL (right footer QR) is truncated to `MaxPayloadLength` when necessary.

## Notes

- SHA-256 hex digests are 64 characters; footer left QR encodes the full digest. `EncodeModules` permits up to **64** characters when the payload matches `^[a-f0-9]{64}$` (integrity hashes).
- All other payloads are capped at **60**.
