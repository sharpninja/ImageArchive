# Technical Requirements (MCP Server)

## TR-ANIM-DLY-001

**Delay encoding** — 60000 ms in container-native delay units.
**Covered by:** FR: FR-ANIM-001; TEST: TEST-ANIM-001
**Status:** pending
Scope: layer-1+

## TR-ARCH-GIT-001

**Git bytes equals .git plus worktree** — For archive.type=git, build compressed tar of source directory including .git and working tree. Deterministic entry order; document exclusions (bin/, obj/). Not git archive tree-export.
**Covered by:** FR: FR-ARCH-001, FR-E2E-001; TEST: TEST-ARCH-001, TEST-E2E-001
**Status:** pending
Scope: layer-1+

## TR-CLI-ARGS-001

**CLI parsing** — Subcommands encode/decode; clear stderr messages; exit codes documented.
**Covered by:** FR: FR-CLI-001; TEST: TEST-CLI-001
**Status:** pending
Scope: layer-1+

## TR-CONT-PNG-001

**APNG writer** — Multi-frame PNG with animation control via SkiaSharp default path; single-frame still valid PNG.
**Covered by:** FR: FR-CONT-001; TEST: TEST-CONT-001
**Status:** pending
Scope: layer-1+

## TR-CONT-SKIA-001

**Default image codec SkiaSharp** — Default encode/decode implementation uses SkiaSharp for raster frames and container write/read (PNG/APNG and WebP as supported). Other codecs may implement IImageEncoder/IImageDecoder; SkiaSharp is the reference default.
**Covered by:** FR: FR-CONT-001, FR-DEC-001, FR-E2E-001, FR-ENC-001, FR-EXT-001; TEST: TEST-CONT-001, TEST-DEC-001, TEST-E2E-001, TEST-ENC-001, TEST-EXT-001
**Status:** pending
Scope: layer-1+

## TR-CONT-WEBP-001

**Animated WebP writer** — Multi-frame animated WebP via SkiaSharp default path where supported.
**Covered by:** FR: FR-CONT-001; TEST: TEST-CONT-001
**Status:** pending
Scope: layer-1+

## TR-DEC-PIPE-001

**Decode pipeline stages** — Open container (SkiaSharp) -> read metadata -> for each frame extract data region -> verify SHA -> concatenate -> verify streamSha256 -> optional postprocessor -> emit.
**Covered by:** FR: FR-DEC-001; TEST: TEST-DEC-001
**Status:** pending
Scope: layer-1+

## TR-E2E-CMP-001

**Clone vs extract compare** — Recursive compare of file paths + content hashes; exclude only paths documented in TR-ARCH-GIT-001; fail on extra/missing/mismatched file.
**Covered by:** FR: FR-E2E-001; TEST: TEST-E2E-001
**Status:** pending
Scope: layer-1+

## TR-ENC-PIPE-001

**Encode pipeline stages** — Validate manifest -> load source stream -> optional preprocessor -> split into frame payloads -> render header/footer -> integrity -> pack container (SkiaSharp) -> write metadata.
**Covered by:** FR: FR-ARCH-001, FR-ENC-001, FR-PIXEL-003; TEST: TEST-ARCH-001, TEST-ENC-001, TEST-PIXEL-003
**Status:** pending
Scope: layer-1+

## TR-EXT-IFACE-001

**Codec interfaces** — IImageEncoder / IImageDecoder in library; core has no static dependency on concrete codecs beyond default registration of SkiaSharp implementations.
**Covered by:** FR: FR-EXT-001, FR-PIXEL-003; TEST: TEST-EXT-001, TEST-PIXEL-003
**Status:** pending
Scope: layer-1+

## TR-FTR-QR-001

**Footer QR binding** — Left=frame data SHA-256 hex; Right=tool commit URL from build/manifest encoder identity.
**Covered by:** FR: FR-FTR-001, FR-FTR-002; TEST: TEST-FTR-001, TEST-FTR-002
**Status:** pending
Scope: layer-1+

## TR-FTR-TXT-001

**Footer typography** — Black, system sans-serif, 8pt, centered between QRs; Line1 exactly Frame {N} of {M} 1-based.
**Covered by:** FR: FR-FTR-001; TEST: TEST-FTR-001
**Status:** pending
Scope: layer-1+

## TR-GEOM-CAP-001

**Capacity formula** — FrameCapacityBytes = 1024 * 924 * 3 = 2838528; single source of truth.
**Covered by:** FR: FR-GEOM-002; TEST: TEST-GEOM-002
**Status:** pending
Scope: layer-1+

## TR-GEOM-SIZE-001

**Frame size constants** — Constants Width=1024, Height=1024, HeaderH=50, DataH=924, FooterH=50; assert in tests.
**Covered by:** FR: FR-GEOM-001, FR-GEOM-002; TEST: TEST-GEOM-001, TEST-GEOM-002
**Status:** pending
Scope: layer-1+

## TR-HDR-FLD-001

**Numbered folder round-robin** — Order images by numeric filename sort; frame i uses images[i % n]; cover in TEST-HDR-005.
**Covered by:** FR: FR-HDR-001; TEST: TEST-HDR-001, TEST-HDR-004, TEST-HDR-005
**Status:** pending
Scope: layer-1+

## TR-HDR-IMG-001

**Header image sizing** — Support full 1024x50 header image; document reject vs fit for other sizes; at least one test uses exact 1024x50.
**Covered by:** FR: FR-HDR-001; TEST: TEST-HDR-001, TEST-HDR-004, TEST-HDR-005
**Status:** pending
Scope: layer-1+

## TR-HDR-QR-001

**QR geometry** — 47x47 modules; margins T=2,R=2,B=1,L=1; white quiet zone.
**Covered by:** FR: FR-HDR-002, FR-HDR-003; TEST: TEST-HDR-002, TEST-HDR-003
**Status:** pending
Scope: layer-1+

## TR-HDR-QR-002

**QR library and max length** — Research .NET QR libraries (QRCoder, ZXing.Net, etc); select encode+decode for tests; document ECC and max payload chars at 47x47 in docs/qr-payload-limits.md.
**Covered by:** FR: FR-HDR-003; TEST: TEST-HDR-003
**Status:** pending
Scope: layer-1+

## TR-HDR-REN-001

**Free-form render order** — Paint white -> free-form -> QR composite.
**Covered by:** FR: FR-HDR-001, FR-HDR-002, FR-MANF-002; TEST: TEST-HDR-001, TEST-HDR-004, TEST-HDR-005, TEST-HDR-002, TEST-MANF-002
**Status:** pending
Scope: layer-1+

## TR-INTG-FAIL-001

**Integrity errors** — Typed error includes frame index and expected vs actual hash.
**Covered by:** FR: FR-DEC-001, FR-INTG-002; TEST: TEST-DEC-001, TEST-INTG-002
**Status:** pending
Scope: layer-1+

## TR-INTG-SHA-001

**SHA-256 hex form** — Hex encoding lowercase a-f0-9, 64 chars.
**Covered by:** FR: FR-FTR-002, FR-INTG-001, FR-INTG-002, FR-INTG-003; TEST: TEST-FTR-002, TEST-INTG-001, TEST-INTG-002, TEST-INTG-003
**Status:** pending
Scope: layer-1+

## TR-LIB-NET-001

**Multi-target TFMs** — Library, CLI, and tests multi-target net8.0;net9.0;net10.0. CI builds/tests all three. Requires SDK 8, 9, and 10.
**Covered by:** FR: FR-CLI-001, FR-E2E-001, FR-LIB-001; TEST: TEST-CLI-001, TEST-E2E-001, TEST-LIB-001
**Status:** pending
Scope: layer-1+

## TR-LIB-STR-001

**Stream contracts** — Sync and/or async Stream APIs; avoid full buffering of multi-GiB archives when avoidable.
**Covered by:** FR: FR-LIB-001; TEST: TEST-LIB-001
**Status:** pending
Scope: layer-1+

## TR-LIB-XUNIT-001

**xUnit v3** — All test projects use xUnit v3 packages; unit and integration projects separate.
**Covered by:** FR: FR-E2E-001, FR-LIB-001; TEST: TEST-E2E-001, TEST-LIB-001
**Status:** pending
Scope: layer-1+

## TR-MANF-HASH-001

**Normalize schema for stream hash** — Schema top-level streamSha256 (64 hex) for RFC §9. Encoder writes into embedded jsonManifest.
**Covered by:** FR: FR-INTG-003, FR-MANF-001; TEST: TEST-INTG-003, TEST-MANF-001
**Status:** pending
Scope: layer-1+

## TR-MANF-SCH-001

**Schema validation** — Validate manifest with draft 2020-12 schema before encode.
**Covered by:** FR: FR-MANF-001, FR-MANF-002, FR-META-003; TEST: TEST-MANF-001, TEST-MANF-002, TEST-META-003
**Status:** pending
Scope: layer-1+

## TR-META-PNG-001

**PNG text chunks** — Use tEXt or iTXt; keys exactly camelCase RFC names.
**Covered by:** FR: FR-META-001, FR-META-002, FR-META-003; TEST: TEST-META-001, TEST-META-002, TEST-META-003
**Status:** pending
Scope: layer-1+

## TR-META-WEBP-001

**WebP metadata** — Map same logical fields into EXIF/XMP with documented key mapping table.
**Covered by:** FR: FR-META-001, FR-META-002; TEST: TEST-META-001, TEST-META-002
**Status:** pending
Scope: layer-1+

## TR-PIXEL-ALPH-001

**Alpha policy** — Prefer RGB24 where container allows; else RGBA with A=255 for all pixels. SkiaSharp default codec.
**Covered by:** FR: FR-PIXEL-001; TEST: TEST-PIXEL-001
**Status:** pending
Scope: layer-1+

## TR-PIXEL-ORD-001

**Channel order** — Per pixel R,G,B sequential byte stream order.
**Covered by:** FR: FR-PIXEL-001, FR-PIXEL-002; TEST: TEST-PIXEL-001, TEST-PIXEL-002
**Status:** pending
Scope: layer-1+

## TR-PIXEL-PAD-001

**Final frame padding** — Final partial frame: remaining data-region bytes after payload zero-filled; hash includes padding bytes.
**Covered by:** FR: FR-ENC-001, FR-PIXEL-002; TEST: TEST-ENC-001, TEST-PIXEL-002
**Status:** pending
Scope: layer-1+

## TR-PROC-EXEC-001

**Process execution** — External commands via process start with stdin/stdout piping; timeout and non-zero exit handling.
**Covered by:** FR: FR-PROC-001, FR-SEC-001; TEST: TEST-PROC-001, TEST-SEC-001
**Status:** pending
Scope: layer-1+

## TR-SEC-HASH-001

**Hash algorithm** — System.Security.Cryptography SHA-256 only for format integrity.
**Covered by:** FR: FR-SEC-001; TEST: TEST-SEC-001
**Status:** pending
Scope: layer-1+

## TR-TDD-COV-001

**AC test linkage** — Every unit/integration test method attributes or names reference AC-FR- id for coverage audit.
**Status:** pending
Scope: layer-1+

## TR-TDD-MOCK-001

**Mocks-first** — Unit tests for core use mocked IImageEncoder/IImageDecoder; integration tests use real SkiaSharp codecs.
**Covered by:** FR: FR-EXT-001; TEST: TEST-EXT-001
**Status:** pending
Scope: layer-1+

