# Receipt: ImageArchive RFC 1.0.0 Requirements Coverage

Generated: 2026-07-14T19:33:22Z
Workspace: F:\GitHub\ImageArchive
Source: docs/ImageArchive-RFC.md + schema/imagearchive-schema.json
Process: Byrd Development Process v4 (TDD, 100% AC coverage)
Default image codec: **SkiaSharp** (TR-CONT-SKIA-001)

## Counts

| Kind | Count |
|------|------:|
| FR | 29 |
| TR | 35 |
| TEST | 32 |
| Mappings | 29 |
| Acceptance Criteria | 93 |
| AC covered by TEST Condition | 93 |
| AC uncovered | 0 |

## Schema normalization

- Added top-level `streamSha256` to `schema/imagearchive-schema.json` (RFC §9).

## Locked decisions

- Image codec default: SkiaSharp
- TFMs: net8.0;net9.0;net10.0
- Tests: xUnit v3
- Git payload: compressed tar of .git + worktree

## AC to TEST coverage matrix

| AC ID | FR | TEST IDs |
|-------|----|----------|
| AC-FR-ANIM-001-1 | FR-ANIM-001 | TEST-ANIM-001 |
| AC-FR-ANIM-001-2 | FR-ANIM-001 | TEST-ANIM-001 |
| AC-FR-ARCH-001-1 | FR-ARCH-001 | TEST-ARCH-001 |
| AC-FR-ARCH-001-2 | FR-ARCH-001 | TEST-ARCH-001 |
| AC-FR-ARCH-001-3 | FR-ARCH-001 | TEST-ARCH-001 |
| AC-FR-ARCH-001-4 | FR-ARCH-001 | TEST-ARCH-001 |
| AC-FR-CLI-001-1 | FR-CLI-001 | TEST-CLI-001 |
| AC-FR-CLI-001-2 | FR-CLI-001 | TEST-CLI-001 |
| AC-FR-CLI-001-3 | FR-CLI-001 | TEST-CLI-001 |
| AC-FR-CLI-001-4 | FR-CLI-001 | TEST-CLI-001 |
| AC-FR-CONT-001-1 | FR-CONT-001 | TEST-CONT-001 |
| AC-FR-CONT-001-2 | FR-CONT-001 | TEST-CONT-001 |
| AC-FR-CONT-001-3 | FR-CONT-001 | TEST-CONT-001 |
| AC-FR-DEC-001-1 | FR-DEC-001 | TEST-DEC-001 |
| AC-FR-DEC-001-2 | FR-DEC-001 | TEST-DEC-001 |
| AC-FR-DEC-001-3 | FR-DEC-001 | TEST-DEC-001 |
| AC-FR-E2E-001-1 | FR-E2E-001 | TEST-E2E-001 |
| AC-FR-E2E-001-2 | FR-E2E-001 | TEST-E2E-001 |
| AC-FR-E2E-001-3 | FR-E2E-001 | TEST-E2E-001 |
| AC-FR-E2E-001-4 | FR-E2E-001 | TEST-E2E-001 |
| AC-FR-E2E-001-5 | FR-E2E-001 | TEST-E2E-001 |
| AC-FR-E2E-001-6 | FR-E2E-001 | TEST-E2E-001 |
| AC-FR-E2E-001-7 | FR-E2E-001 | TEST-E2E-001 |
| AC-FR-ENC-001-1 | FR-ENC-001 | TEST-ENC-001 |
| AC-FR-ENC-001-2 | FR-ENC-001 | TEST-ENC-001 |
| AC-FR-ENC-001-3 | FR-ENC-001 | TEST-ENC-001 |
| AC-FR-EXT-001-1 | FR-EXT-001 | TEST-EXT-001 |
| AC-FR-EXT-001-2 | FR-EXT-001 | TEST-EXT-001 |
| AC-FR-FTR-001-1 | FR-FTR-001 | TEST-FTR-001 |
| AC-FR-FTR-001-2 | FR-FTR-001 | TEST-FTR-001 |
| AC-FR-FTR-001-3 | FR-FTR-001 | TEST-FTR-001 |
| AC-FR-FTR-001-4 | FR-FTR-001 | TEST-FTR-001 |
| AC-FR-FTR-002-1 | FR-FTR-002 | TEST-FTR-002 |
| AC-FR-FTR-002-2 | FR-FTR-002 | TEST-FTR-002 |
| AC-FR-GEOM-001-1 | FR-GEOM-001 | TEST-GEOM-001 |
| AC-FR-GEOM-001-2 | FR-GEOM-001 | TEST-GEOM-001 |
| AC-FR-GEOM-002-1 | FR-GEOM-002 | TEST-GEOM-002 |
| AC-FR-GEOM-002-2 | FR-GEOM-002 | TEST-GEOM-002 |
| AC-FR-GEOM-002-3 | FR-GEOM-002 | TEST-GEOM-002 |
| AC-FR-GEOM-002-4 | FR-GEOM-002 | TEST-GEOM-002 |
| AC-FR-HDR-001-1 | FR-HDR-001 | TEST-HDR-001 |
| AC-FR-HDR-001-2 | FR-HDR-001 | TEST-HDR-001 |
| AC-FR-HDR-001-3 | FR-HDR-001 | TEST-HDR-001 |
| AC-FR-HDR-001-4 | FR-HDR-001 | TEST-HDR-004 |
| AC-FR-HDR-001-5 | FR-HDR-001 | TEST-HDR-005 |
| AC-FR-HDR-002-1 | FR-HDR-002 | TEST-HDR-002 |
| AC-FR-HDR-002-2 | FR-HDR-002 | TEST-HDR-002 |
| AC-FR-HDR-002-3 | FR-HDR-002 | TEST-HDR-002 |
| AC-FR-HDR-003-1 | FR-HDR-003 | TEST-HDR-003 |
| AC-FR-HDR-003-2 | FR-HDR-003 | TEST-HDR-003 |
| AC-FR-HDR-003-3 | FR-HDR-003 | TEST-HDR-003 |
| AC-FR-HDR-003-4 | FR-HDR-003 | TEST-HDR-003 |
| AC-FR-INTG-001-1 | FR-INTG-001 | TEST-INTG-001 |
| AC-FR-INTG-001-2 | FR-INTG-001 | TEST-INTG-001 |
| AC-FR-INTG-002-1 | FR-INTG-002 | TEST-INTG-002 |
| AC-FR-INTG-002-2 | FR-INTG-002 | TEST-INTG-002 |
| AC-FR-INTG-003-1 | FR-INTG-003 | TEST-INTG-003 |
| AC-FR-INTG-003-2 | FR-INTG-003 | TEST-INTG-003 |
| AC-FR-LIB-001-1 | FR-LIB-001 | TEST-LIB-001 |
| AC-FR-LIB-001-2 | FR-LIB-001 | TEST-LIB-001 |
| AC-FR-LIB-001-3 | FR-LIB-001 | TEST-LIB-001 |
| AC-FR-MANF-001-1 | FR-MANF-001 | TEST-MANF-001 |
| AC-FR-MANF-001-2 | FR-MANF-001 | TEST-MANF-001 |
| AC-FR-MANF-001-3 | FR-MANF-001 | TEST-MANF-001 |
| AC-FR-MANF-001-4 | FR-MANF-001 | TEST-MANF-001 |
| AC-FR-MANF-001-5 | FR-MANF-001 | TEST-MANF-001 |
| AC-FR-MANF-001-6 | FR-MANF-001 | TEST-MANF-001 |
| AC-FR-MANF-001-7 | FR-MANF-001 | TEST-MANF-001 |
| AC-FR-MANF-002-1 | FR-MANF-002 | TEST-MANF-002 |
| AC-FR-MANF-002-2 | FR-MANF-002 | TEST-MANF-002 |
| AC-FR-MANF-002-3 | FR-MANF-002 | TEST-MANF-002 |
| AC-FR-MANF-002-4 | FR-MANF-002 | TEST-MANF-002 |
| AC-FR-META-001-1 | FR-META-001 | TEST-META-001 |
| AC-FR-META-001-2 | FR-META-001 | TEST-META-001 |
| AC-FR-META-001-3 | FR-META-001 | TEST-META-001 |
| AC-FR-META-002-1 | FR-META-002 | TEST-META-002 |
| AC-FR-META-002-2 | FR-META-002 | TEST-META-002 |
| AC-FR-META-003-1 | FR-META-003 | TEST-META-003 |
| AC-FR-META-003-2 | FR-META-003 | TEST-META-003 |
| AC-FR-PIXEL-001-1 | FR-PIXEL-001 | TEST-PIXEL-001 |
| AC-FR-PIXEL-001-2 | FR-PIXEL-001 | TEST-PIXEL-001 |
| AC-FR-PIXEL-001-3 | FR-PIXEL-001 | TEST-PIXEL-001 |
| AC-FR-PIXEL-002-1 | FR-PIXEL-002 | TEST-PIXEL-002 |
| AC-FR-PIXEL-002-2 | FR-PIXEL-002 | TEST-PIXEL-002 |
| AC-FR-PIXEL-002-3 | FR-PIXEL-002 | TEST-PIXEL-002 |
| AC-FR-PIXEL-003-1 | FR-PIXEL-003 | TEST-PIXEL-003 |
| AC-FR-PIXEL-003-2 | FR-PIXEL-003 | TEST-PIXEL-003 |
| AC-FR-PROC-001-1 | FR-PROC-001 | TEST-PROC-001 |
| AC-FR-PROC-001-2 | FR-PROC-001 | TEST-PROC-001 |
| AC-FR-PROC-001-3 | FR-PROC-001 | TEST-PROC-001 |
| AC-FR-SEC-001-1 | FR-SEC-001 | TEST-SEC-001 |
| AC-FR-SEC-001-2 | FR-SEC-001 | TEST-SEC-001 |
| AC-FR-SEC-001-3 | FR-SEC-001 | TEST-SEC-001 |

## FR inventory

- **FR-ANIM-001**: Frame delay (priority=medium, AC=2)
- **FR-ARCH-001**: Archive type ingestion (priority=high, AC=4)
- **FR-CLI-001**: CLI encode/decode (priority=high, AC=4)
- **FR-CONT-001**: Supported containers (priority=critical, AC=3)
- **FR-DEC-001**: Decode end-to-end (priority=critical, AC=3)
- **FR-E2E-001**: Full clone-tar-encode-validate-extract integration (priority=critical, AC=7)
- **FR-ENC-001**: Encode end-to-end (priority=critical, AC=3)
- **FR-EXT-001**: Pluggable image codecs (priority=high, AC=2)
- **FR-FTR-001**: Footer layout (priority=critical, AC=4)
- **FR-FTR-002**: Footer left QR content binding (priority=critical, AC=2)
- **FR-GEOM-001**: Fixed frame dimensions (priority=critical, AC=2)
- **FR-GEOM-002**: Mandatory region layout (priority=critical, AC=4)
- **FR-HDR-001**: Header background and free-form area (priority=high, AC=5)
- **FR-HDR-002**: Header QR reservation and composite (priority=critical, AC=3)
- **FR-HDR-003**: Header QR content (priority=high, AC=4)
- **FR-INTG-001**: Per-frame integrity encode (priority=critical, AC=2)
- **FR-INTG-002**: Per-frame integrity decode reject (priority=critical, AC=2)
- **FR-INTG-003**: Whole-stream integrity in manifest (priority=high, AC=2)
- **FR-LIB-001**: C# library Stream API (priority=high, AC=3)
- **FR-MANF-001**: Manifest-driven encode (priority=critical, AC=7)
- **FR-MANF-002**: Header configuration in manifest (priority=high, AC=4)
- **FR-META-001**: Required text-chunk fields (priority=critical, AC=3)
- **FR-META-002**: Optional text-chunk fields (priority=medium, AC=2)
- **FR-META-003**: Metadata source of truth (priority=high, AC=2)
- **FR-PIXEL-001**: RGB-only payload channels (priority=critical, AC=3)
- **FR-PIXEL-002**: Scan order and frame concatenation (priority=critical, AC=3)
- **FR-PIXEL-003**: Core vs container packing responsibility (priority=high, AC=2)
- **FR-PROC-001**: Optional stream processors (priority=medium, AC=3)
- **FR-SEC-001**: Crypto and encryption scope (priority=high, AC=3)

## Verification

- MCP requirements store (fr/tr/test/mapping)
- Generated docs: docs/Project/
- Implementation plan: docs/plans/ImageArchive-Implementation-Plan.md
- Iteration phases: PHASE-001 .. PHASE-011 (P0-P10)
- **PASS: 100% AC coverage (93/93)**
