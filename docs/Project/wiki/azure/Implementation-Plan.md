**Status: Implemented (P0–P10 in tree).** ArchiveType.Raw = 0. Default codec SkiaSharp.

# Plan: ImageArchive Implementation (Byrd v4 TDD, P0–P10)

## Context

Requirements planning is complete and verified:

- MCP: 29 FR, 35 TR, 32 TEST, 29 mappings, **93/93 AC coverage**
- Receipt: [`docs/receipts-requirements-rfc-1.0.0.md`](docs/receipts-requirements-rfc-1.0.0.md)
- High-level Round B notes: [`docs/plans/ImageArchive-Implementation-Plan.md`](docs/plans/ImageArchive-Implementation-Plan.md)
- Schema includes `streamSha256`; RFC is final at [`docs/ImageArchive-RFC.md`](docs/ImageArchive-RFC.md)

**Working tree has no `src/`, `tests/`, or `.sln`** (implementation was deleted deliberately). SDKs **8.0, 9.0, and 10.0** are installed on this machine.

This plan is the **executable implementation plan**: scaffolding through flagship E2E under Byrd Dev Process v4 (Fowler Red → Green → Refactor, mocks-first, full-suite green gates).

On approval, execution writes/updates [`docs/plans/ImageArchive-Implementation-Plan.md`](docs/plans/ImageArchive-Implementation-Plan.md) to match this document (source of truth for implementers) and begins **P0 only if the user asks to implement**; otherwise the approved plan file is the deliverable of this planning turn.

## Goal

Ship a GPL-3.0 C# reference library + CLI that:

1. Encodes arbitrary archives into multi-frame APNG / animated WebP per RFC 1.0.0
2. Decodes with per-frame and whole-stream integrity (`streamSha256`)
3. Satisfies every FR AC via automated tests (unit + integration)
4. Multi-targets `net8.0;net9.0;net10.0` with **xUnit v3**
5. Uses **SkiaSharp** as the default image codec

## Locked decisions (do not re-litigate)

| Topic | Decision |
|-------|----------|
| Image codec | **SkiaSharp** default (`SkiaImageEncoder` / `SkiaImageDecoder`); core uses `IImageEncoder` / `IImageDecoder` |
| TFMs | `net8.0;net9.0;net10.0` on library, CLI, unit tests, integration tests |
| Test framework | **xUnit v3** (`xunit.v3`); separate unit vs integration projects |
| Git archives | Compressed tar (`.tar.gz`) of **`.git` + working tree**; not `git archive` tree-export |
| Final frame padding | Zero-fill remainder of data region; padding included in per-frame SHA-256 |
| SHA-256 hex | Lowercase `a-f0-9`, 64 chars |
| Frame delay | Exactly **60000 ms** when container is animated |
| Stream integrity | Manifest field `streamSha256` (schema already normalized) |
| Process | Byrd v4 TDD: AC tests → mocks green → real code → full suite green before phase exit |

## Solution architecture

### Projects

```
ImageArchive.sln
src/ImageArchive/                      # class library (multi-TFM)
src/ImageArchive.Cli/                  # CLI (multi-TFM)
tests/ImageArchive.UnitTests/          # xUnit v3 (multi-TFM)
tests/ImageArchive.IntegrationTests/   # xUnit v3 (multi-TFM); TEST-E2E-001 lives here
```

### Visibility rules

| Visibility | Rule |
|------------|------|
| **Public** | Stable surface for library consumers, CLI, and tests that assert FR-LIB/FR-EXT ACs. Lives in root namespaces under `ImageArchive`. |
| **Public (codec plugin)** | Types implementers need for custom containers: `IImageEncoder`, `IImageDecoder`, frame pixel contract, metadata bag. |
| **Internal** | Renderers, pixel packers, Skia helpers, git tar details. Unit-testable via `InternalsVisibleTo` for unit test assembly only. |
| **Not public** | CLI `Program` internals (except documented exit codes via process contract). |

Namespaces:

```
ImageArchive                      # facade: encoder/decoder entry points
ImageArchive.Abstractions         # interfaces for codecs, QR, archive sources
ImageArchive.Geometry             # public constants
ImageArchive.Manifest             # public models + validation API
ImageArchive.Metadata             # text-chunk metadata model
ImageArchive.Integrity            # public hash helpers + exceptions
ImageArchive.Codecs.Skia          # default SkiaSharp codecs (public concrete types)
ImageArchive.IO                   # optional public StreamProcessor
ImageArchive.Internal.*           # internal implementation (not in API docs)
```

---

## Public API surface (complete)

All signatures are **contractual** for implementation. Sync APIs are required; async overloads (`*Async` with `CancellationToken`) are **optional** in P0–P8 and recommended by P9 for I/O-heavy paths. If both exist, sync may wrap async or vice versa, but public sync methods must remain.

### 1. Geometry (`ImageArchive.Geometry`)

```csharp
public static class FrameGeometry
{
    public const int Width = 1024;
    public const int Height = 1024;
    public const int HeaderHeight = 50;
    public const int DataHeight = 924;      // rows 50..973
    public const int FooterHeight = 50;     // rows 974..1023
    public const int DataRegionFirstRow = 50;
    public const int FooterFirstRow = 974;
    public const int BytesPerPixelRgb = 3;
    public const int FrameCapacityBytes = Width * DataHeight * BytesPerPixelRgb; // 2_838_528
    public const int QrModuleSize = 47;
    public const int QrCellSize = 50;       // module + margins
    public const int QrMarginTop = 2;
    public const int QrMarginRight = 2;
    public const int QrMarginBottom = 1;
    public const int QrMarginLeft = 1;
    public const int AnimationDelayMilliseconds = 60_000;
}
```

### 2. Codec plug-in contracts (`ImageArchive.Abstractions`)

```csharp
/// <summary>Pixel buffer for one 1024×1024 frame. Row-major; RGB or RGBA.</summary>
public sealed class FrameBitmap
{
    public int Width { get; init; }           // must be 1024
    public int Height { get; init; }          // must be 1024
    public PixelFormat Format { get; init; }  // Rgb24 or Rgba32
    public byte[] Pixels { get; init; }       // length = W*H*bytesPerPixel
}

public enum PixelFormat
{
    Rgb24 = 0,   // 3 bytes/pixel R,G,B
    Rgba32 = 1   // 4 bytes/pixel R,G,B,A; A must be 255 when used as ImageArchive frame
}

public enum ContainerFormat
{
    Png = 0,     // APNG when multi-frame
    Webp = 1     // animated WebP when multi-frame
}

/// <summary>RFC text-chunk metadata (camelCase keys).</summary>
public sealed class ArchiveMetadata
{
    // Required
    public string EncoderName { get; set; }
    public string EncoderVersion { get; set; }
    public string EncoderSha256 { get; set; }      // 64 hex
    public string MimeType { get; set; }
    public ArchiveType ArchiveType { get; set; }
    public string JsonSchema { get; set; }         // embedded schema document
    public string JsonManifest { get; set; }       // exact manifest JSON used / produced

    // Optional
    public string? SourceUrl { get; set; }
    public string? CommitHash { get; set; }
    public string? OriginalFileName { get; set; }

    // Free-form additional chunks (key -> value); never used for required keys
    public IDictionary<string, string> AdditionalTextChunks { get; }
}

public enum ArchiveType
{
    Git = 0,
    Zip = 1,
    Tar = 2,
    Raw = 3
}

public interface IImageEncoder
{
    ContainerFormat Format { get; }

    /// <summary>
    /// Write multi-frame ImageArchive container. Frames already fully rendered
    /// (header + data + footer). DelayMs is 60000 for animated containers.
    /// </summary>
    void Encode(
        Stream output,
        IReadOnlyList<FrameBitmap> frames,
        ArchiveMetadata metadata,
        int delayMilliseconds = FrameGeometry.AnimationDelayMilliseconds);
}

public interface IImageDecoder
{
    ContainerFormat Format { get; }

    /// <summary>
    /// Read container. Returns frames in order and metadata from text chunks.
    /// Timing is ignored by callers for payload extraction.
    /// </summary>
    DecodeContainerResult Decode(Stream input);
}

public sealed class DecodeContainerResult
{
    public IReadOnlyList<FrameBitmap> Frames { get; init; }
    public ArchiveMetadata Metadata { get; init; }
    public int? DelayMilliseconds { get; init; }  // may be present; decoder pipeline ignores for bytes
}
```

### 3. QR service (`ImageArchive.Abstractions`)

```csharp
public interface IQrCodeService
{
    /// <summary>Render QR payload into a 47×47 module matrix (boolean or 0/1), no margins.</summary>
    bool[,] EncodeModules(string payload);

    /// <summary>Decode payload from a 50×50 (or 47×47) pixel RGB/RGBA crop of a QR cell.</summary>
    string DecodeFromPixels(ReadOnlySpan<byte> pixels, int width, int height, PixelFormat format);

    /// <summary>Maximum payload character length reliable at 47×47 for the chosen ECC (documented in docs/qr-payload-limits.md).</summary>
    int MaxPayloadLength { get; }
}
```

### 4. Archive source loading (`ImageArchive.Abstractions`)

```csharp
public interface IArchiveSourceLoader
{
    /// <summary>
    /// Produce the continuous archive byte stream for encoding from a manifest archive section.
    /// git => compressed tar of .git + worktree; zip/tar/raw => file bytes.
    /// </summary>
    Stream OpenRead(ArchiveManifestSection archive, string? workingDirectory = null);
}

// Concrete public default:
public sealed class DefaultArchiveSourceLoader : IArchiveSourceLoader { /* ... */ }
```

### 5. Stream processors (`ImageArchive.IO` / abstractions)

```csharp
public interface IStreamProcessor
{
    /// <summary>If command is null/empty, return input unchanged (no copy required if seekable policy allows). Non-zero process exit => throw.</summary>
    Stream Apply(Stream input, string? externalCommand);
}

public sealed class ExternalCommandStreamProcessor : IStreamProcessor { /* ... */ }
```

### 6. Manifest models + validation (`ImageArchive.Manifest`)

Public POCOs mirror `schema/imagearchive-schema.json` (System.Text.Json names camelCase):

```csharp
public sealed class ImageArchiveManifest
{
    public string Version { get; set; }              // "1.0.0"
    public EncoderManifestSection Encoder { get; set; }
    public ArchiveManifestSection Archive { get; set; }
    public OutputManifestSection Output { get; set; }
    public string? StreamSha256 { get; set; }        // written by encoder; optional on input
    public HeaderManifestSection? Header { get; set; }
    public IList<FrameManifestSection> Frames { get; set; }
    public string? Preprocessor { get; set; }
    public string? Postprocessor { get; set; }
}

public sealed class EncoderManifestSection
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string Sha256 { get; set; }
}

public sealed class ArchiveManifestSection
{
    public ArchiveType Type { get; set; }
    public string MimeType { get; set; }
    public string Source { get; set; }
    public string? SourceUrl { get; set; }
    public string? CommitHash { get; set; }
    public string? OriginalFileName { get; set; }
}

public sealed class OutputManifestSection
{
    public string Path { get; set; }
    public ContainerFormat Format { get; set; }  // maps schema "png"|"webp"
}

public sealed class HeaderManifestSection
{
    public HeaderContentType? Type { get; set; }  // text|image|folder
    public string? Text { get; set; }
    public string? ImagePath { get; set; }
    public string? FolderPath { get; set; }
    public QrCodeManifestSection? QrCode { get; set; }
}

public enum HeaderContentType { Text, Image, Folder }

public sealed class QrCodeManifestSection
{
    public string? Content { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class FrameManifestSection
{
    public HeaderManifestSection? Header { get; set; }
}

public interface IManifestValidator
{
    ManifestValidationResult Validate(ImageArchiveManifest manifest);
    ManifestValidationResult ValidateJson(string json);
    ImageArchiveManifest Parse(string json);  // throws or returns invalid via result — prefer Result type
}

public sealed class ManifestValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ManifestValidationError> Errors { get; init; }
}

public sealed class ManifestValidationError
{
    public string Path { get; init; }       // JSON pointer or property path
    public string Message { get; init; }
}

// Public default implementation:
public sealed class JsonSchemaManifestValidator : IManifestValidator { /* loads schema/imagearchive-schema.json */ }

public static class ManifestJson
{
    public static ImageArchiveManifest Deserialize(string json);
    public static string Serialize(ImageArchiveManifest manifest); // deterministic enough for embed; document options
}
```

### 7. Integrity (`ImageArchive.Integrity`)

```csharp
public static class Sha256Hex
{
    public static string Compute(ReadOnlySpan<byte> data);  // lowercase 64 hex
    public static string Compute(Stream stream);
}

public sealed class IntegrityException : Exception
{
    public int? FrameIndex { get; }           // 0-based when per-frame
    public string? ExpectedHash { get; }
    public string? ActualHash { get; }
}
```

### 8. Encode / decode facades (`ImageArchive`)

Primary consumer API (FR-LIB-001 Stream contracts):

```csharp
public sealed class ImageArchiveEncodeOptions
{
    public IImageEncoder? Encoder { get; init; }           // default: Skia for Output.Format
    public IQrCodeService? QrCodeService { get; init; }
    public IArchiveSourceLoader? SourceLoader { get; init; }
    public IStreamProcessor? StreamProcessor { get; init; }
    public IManifestValidator? ManifestValidator { get; init; }
    public string? ToolCommitUrl { get; init; }            // footer right QR content
    public string? WorkingDirectory { get; init; }         // resolve relative source paths
}

public sealed class ImageArchiveDecodeOptions
{
    public IImageDecoder? Decoder { get; init; }           // default: sniff or Skia for known formats
    public IQrCodeService? QrCodeService { get; init; }    // optional verification helpers
    public IStreamProcessor? StreamProcessor { get; init; }
    public bool VerifyFrameIntegrity { get; init; } = true;
    public bool VerifyStreamSha256 { get; init; } = true;
}

public interface IImageArchiveEncoder
{
    /// <summary>
    /// Validate manifest, load/process archive stream, pack frames, write container + metadata.
    /// Sets manifest.StreamSha256 on the embedded jsonManifest.
    /// </summary>
    EncodeResult Encode(ImageArchiveManifest manifest, Stream output, ImageArchiveEncodeOptions? options = null);

    /// <summary>Encode using archive bytes already in memory/stream (ignores archive.Source file open; still uses type/mime for metadata).</summary>
    EncodeResult Encode(ImageArchiveManifest manifest, Stream archiveInput, Stream output, ImageArchiveEncodeOptions? options = null);
}

public sealed class EncodeResult
{
    public int FrameCount { get; init; }
    public string StreamSha256 { get; init; }
    public ImageArchiveManifest EmbeddedManifest { get; init; }  // with streamSha256 set
    public ArchiveMetadata Metadata { get; init; }
}

public interface IImageArchiveDecoder
{
    DecodeResult Decode(Stream imageInput, ImageArchiveDecodeOptions? options = null);
}

public sealed class DecodeResult
{
    public Stream ArchiveStream { get; init; }             // caller owns dispose
    public ArchiveMetadata Metadata { get; init; }
    public ImageArchiveManifest? Manifest { get; init; } // parsed from jsonManifest when valid JSON
    public int FrameCount { get; init; }
    public string StreamSha256 { get; init; }            // computed
}

// Public defaults:
public sealed class ImageArchiveEncoder : IImageArchiveEncoder { }
public sealed class ImageArchiveDecoder : IImageArchiveDecoder { }
```

### 9. Default codecs (`ImageArchive.Codecs.Skia`)

```csharp
public sealed class SkiaImageEncoder : IImageEncoder
{
    public SkiaImageEncoder(ContainerFormat format);
    public ContainerFormat Format { get; }
    public void Encode(Stream output, IReadOnlyList<FrameBitmap> frames, ArchiveMetadata metadata, int delayMilliseconds = FrameGeometry.AnimationDelayMilliseconds);
}

public sealed class SkiaImageDecoder : IImageDecoder
{
    public ContainerFormat Format { get; }  // may be set after sniff, or dual-mode decoder:
}

/// <summary>Sniffs PNG vs WebP and decodes via SkiaSharp-backed implementation.</summary>
public sealed class SkiaAutoImageDecoder : IImageDecoder
{
    public ContainerFormat Format { get; private set; }
    public DecodeContainerResult Decode(Stream input);
}
```

### 10. Factory helpers (public convenience)

```csharp
public static class ImageArchiveCodecs
{
    public static IImageEncoder CreateDefaultEncoder(ContainerFormat format); // returns SkiaImageEncoder
    public static IImageDecoder CreateDefaultDecoder();                      // returns SkiaAutoImageDecoder
}
```

### 11. Exceptions (public)

| Type | When |
|------|------|
| `ManifestValidationException` | Invalid manifest (wraps `ManifestValidationResult`) |
| `IntegrityException` | Frame or stream hash mismatch |
| `ImageArchiveException` | Base for library failures |
| `QrPayloadTooLongException` | Header/footer QR exceeds max (AC-FR-HDR-003-4) |
| `ArchiveSourceException` | Source path missing / git tar failure |
| `StreamProcessorException` | External command non-zero / timeout |

```csharp
public class ImageArchiveException : Exception { }
public sealed class ManifestValidationException : ImageArchiveException
{
    public ManifestValidationResult Result { get; }
}
// IntegrityException as above
public sealed class QrPayloadTooLongException : ImageArchiveException
{
    public int MaxLength { get; }
    public int ActualLength { get; }
}
```

### 12. CLI surface (`src/ImageArchive.Cli`) — process API

Not a .NET public API, but **user-facing contract**:

```
ImageArchive encode --manifest <path>
ImageArchive decode --input <image> --output <path>

Exit codes:
  0  success
  1  validation / usage error (manifest, args)
  2  integrity failure
  3  I/O or external process failure
  4  unexpected internal error
```

- `encode` reads manifest JSON; writes `output.path` (creates directories as needed).
- `decode` writes archive bytes to `--output` file (or extracts tar/zip into directory if output is directory — **lock in P8**: default = file stream dump; directory extract optional if format is tar/zip).
- Stderr: human-readable error; stdout quiet on success unless `--verbose` (optional P8).

### 13. Explicitly **internal** (not public API)

| Type | Reason |
|------|--------|
| `PixelPacker` / `PixelUnpacker` | Implementation detail of frame assembly |
| `HeaderRenderer` / `FooterRenderer` | Frame composition |
| `FrameAssembler` | Combines header/data/footer rows |
| `GitWorktreeTarBuilder` | git archive details |
| Skia pixel conversion helpers | Codec private |
| QR margin compositor | Internal to renderers |

Unit tests access these via `InternalsVisibleTo("ImageArchive.UnitTests")`.

### 14. Assembly attributes / versioning

- Assembly name: `ImageArchive`
- `InternalsVisibleTo`: `ImageArchive.UnitTests`
- Public API documented in XML doc comments (`GenerateDocumentationFile=true` from P0)
- SemVer starts at `1.0.0` aligned with RFC version for first release

### 15. API → FR mapping (traceability)

| Public surface | Primary FR |
|----------------|------------|
| `FrameGeometry` | FR-GEOM-001/002 |
| `IImageEncoder` / `IImageDecoder` | FR-EXT-001, FR-PIXEL-003, FR-CONT-001 |
| `SkiaImageEncoder` / `SkiaImageDecoder` | FR-CONT-001, TR-CONT-SKIA-001 |
| `IImageArchiveEncoder` / `IImageArchiveDecoder` | FR-ENC-001, FR-DEC-001, FR-LIB-001 |
| `ImageArchiveManifest` + `IManifestValidator` | FR-MANF-001/002 |
| `ArchiveMetadata` | FR-META-001/002/003 |
| `Sha256Hex` / `IntegrityException` | FR-INTG-001/002/003 |
| `IArchiveSourceLoader` | FR-ARCH-001 |
| `IStreamProcessor` | FR-PROC-001, FR-SEC-001 |
| `IQrCodeService` | FR-HDR-002/003, FR-FTR-001/002 |
| CLI encode/decode | FR-CLI-001 |

### Package choices (initial pins; bump only if multi-TFM fails)

| Concern | Package | Notes |
|---------|---------|--------|
| Codec | `SkiaSharp` (+ platform native assets as needed) | Default; verify APNG multi-frame write/read early in P5 |
| QR | Prefer **QRCoder** for encode; **ZXing.Net** if decode reliability needs it | Spike in P2; publish `docs/qr-payload-limits.md` for 47×47 |
| Schema | `JsonSchema.Net` | Draft 2020-12 |
| CLI | `System.CommandLine` | encode/decode |
| Mocks | `NSubstitute` | Unit tests |
| Tests | `xunit.v3`, `Microsoft.NET.Test.Sdk` | Traits: `AC-FR-…` |

### Test conventions

- Method or `[Trait("AC", "AC-FR-…")]` for every AC covered (TR-TDD-COV-001)
- Unit tests never require real PNG/WebP until P5+; use fakes implementing encoder/decoder interfaces
- Integration tests use real SkiaSharp
- Flagship E2E: long-running category; shallow clone; exclude `bin/`, `obj/`

---

## Phases (execution order)

Each phase ends only when: **all new AC tests green + entire suite green on net8/9/10**.

### P0 — Scaffold + public API stubs (FR-LIB-001 partial, FR-EXT-001 shape)

**Do**

1. Create solution + four projects multi-targeting `net8.0;net9.0;net10.0`
2. Wire project references; packages: xunit.v3, NSubstitute; SkiaSharp may wait until P5
3. Add **all public interfaces, enums, DTOs, and exception types** from the Public API section as compilable stubs (`NotImplementedException` / empty bodies only where needed for non-interface types)
4. Add `FrameGeometry` constants with real values (no stub)
5. `InternalsVisibleTo` unit tests; `GenerateDocumentationFile=true`
6. Placeholder smoke tests that compile against public types
7. Update README build/test commands
8. Optional: `Directory.Build.props` for shared TFMs

**Exit:** `dotnet build` and `dotnet test` succeed for all three TFMs; public API surface compiles; XML docs generate without error.

**AC focus:** AC-FR-LIB-001-3; FR-EXT-001 interface shape exists.

---

### P1 — Geometry + pixel packing (FR-GEOM-*, FR-PIXEL-*)

**Tests first (unit, mocks)**

- TEST-GEOM-001/002: dimensions, region rows, capacity constant
- TEST-PIXEL-001/002/003: RGB order, alpha policy on RGBA buffers, scan order, concat, stream vs encoder split

**Implement**

- `FrameGeometry`, `PixelPacker`/`PixelUnpacker`
- Fake encoder that records packed frames without writing PNG

**Exit:** P1 ACs green with mocks; capacity exactly `2838528`.

---

### P2 — Header / footer / QR (FR-HDR-*, FR-FTR-*)

**Tests first**

- TEST-HDR-001..005 including:
  - **TEST-HDR-004:** exact **1024×50** header image on ≥1 frame
  - **TEST-HDR-005:** numbered folder round-robin `i % n`
- TEST-FTR-001/002: layout + left QR binds to frame hash

**Research spike (same phase)**

- Measure max QR payload at 47×47 modules; write [`docs/qr-payload-limits.md`](docs/qr-payload-limits.md)
- Enforce AC-FR-HDR-003-4 (reject/truncate per documented max)

**Implement**

- Header/footer renderers (white bg → free-form → QR)
- QR margins: T=2,R=2,B=1,L=1 on 50×50
- Footer text: `Frame {N} of {M}` (1-based) + hex SHA line

**Exit:** Header image + folder RR tests green; QR limits documented.

---

### P3 — Integrity + metadata model (FR-INTG-*, FR-META-*)

**Tests first**

- TEST-INTG-001..003: per-frame hash input = data region only; decode fail-closed with frame index; `streamSha256` round-trip compare
- TEST-META-001..003: required camelCase fields; optional omission; jsonManifest/jsonSchema embed model (in-memory before real chunks)

**Implement**

- `IntegrityService` (SHA-256 lowercase hex)
- Metadata DTO + serializers for chunk key/value model

**Exit:** Integrity unit suite green.

---

### P4 — Manifest validation (FR-MANF-*)

**Tests first**

- TEST-MANF-001: schema matrix (required keys, version const, enums, sha pattern, minItems frames, additionalProperties false)
- TEST-MANF-002: header types text/image/folder + per-frame override

**Implement**

- Load `schema/imagearchive-schema.json` from embedded resource or known path
- `ManifestValidator` using JsonSchema.Net
- Manifest models mirror schema (including optional input `streamSha256`, encoder-written output)

**Exit:** Validation matrix green.

---

### P5 — Real PNG/APNG via SkiaSharp (FR-CONT partial, FR-ENC/DEC partial)

**Tests first (integration)**

- Encode small known stream → multi-frame PNG; decode back; byte identity
- Metadata written to PNG text chunks (tEXt/iTXt)
- Alpha fully opaque if RGBA

**Implement**

- `SkiaImageEncoder` / `SkiaImageDecoder`
- Wire encoder pipeline: validate → load stream → split frames → render → pack → Skia write
- Wire decoder: open → extract data regions → verify hashes → concat → verify `streamSha256`

**Risk:** SkiaSharp APNG multi-frame support may need custom acTL/fcTL handling or a thin helper. Spike early; if blocked, document fallback (still Skia for raster, custom APNG mux). Do not switch default codec without plan change.

**Exit:** TEST-ENC-001 / TEST-DEC-001 / TEST-CONT-001 (png path) green.

---

### P6 — Animated WebP + delay (FR-ANIM-001, WebP path)

**Tests**

- `output.format=webp` produces animated WebP
- Frame delay **60000 ms**
- Decoder ignores delay

**Exit:** WebP path green; delay asserted.

---

### P7 — Archive types (FR-ARCH-001)

**Tests**

- `type=git`: directory → `.tar.gz` of `.git` + worktree; deterministic entry order; exclude `bin/`, `obj/`
- `type=zip|tar|raw`: file stream
- `mimeType` → metadata

**Implement**

- `GitWorktreeTarArchive` using `System.Formats.Tar` + GZip
- File loaders for other types

**Exit:** TEST-ARCH-001 green.

---

### P8 — CLI (FR-CLI-001)

**Tests (integration)**

- `encode --manifest path` writes `output.path`
- `decode --input --output` extracts
- Non-zero exit on validation/integrity/IO failure

**Implement**

- `src/ImageArchive.Cli` with System.CommandLine

**Exit:** TEST-CLI-001 green.

---

### P9 — Flagship E2E (FR-E2E-001)

**TEST-E2E-001 (xUnit v3 integration)**

1. Shallow `git clone` of this repo HEAD to temp dir  
2. Compressed tar of clone (`.git` + worktree)  
3. Encode to APNG via library (SkiaSharp)  
4. Validate every frame left QR = data-region SHA-256 hex; footer Line2 match; header/right QR policy  
5. Validate required metadata + `jsonManifest.streamSha256`  
6. Extract payload tar; recursive hash-compare to clone (TR-E2E-CMP-001)  
7. Traits `AC-FR-E2E-001-*`; multi-TFM green  

**Exit:** E2E green on net8/9/10.

---

### P10 — Processors + security (FR-PROC-001, FR-SEC-001)

**Tests**

- Null preprocessor/postprocessor pass-through  
- External command pipe; non-zero fails  
- No format-level encryption; only SHA-256 for integrity  

**Exit:** TEST-PROC-001 / TEST-SEC-001 green; full suite green.

---

## Execution approach (after plan approval)

1. **Expand** in-repo plan doc to this level of detail (replace Round B “planning only” status with “approved for implementation”).
2. **Implement P0** first (scaffold) when user says implement (or as first execution step if approval includes “start coding”).
3. **One phase per PR-sized increment** preferred; never merge a phase with red tests.
4. **Do not resurrect** deleted pre-delete implementation blindly; rebuild against AC tests.
5. Keep MCP mappings as traceability; when ACs refine, update MCP + receipt before changing tests.

## Critical files to create (P0–P2)

| Path | When |
|------|------|
| `ImageArchive.sln` | P0 |
| `src/ImageArchive/ImageArchive.csproj` | P0 |
| `src/ImageArchive.Cli/ImageArchive.Cli.csproj` | P0 |
| `src/ImageArchive/Abstractions/*.cs` | P0 (public interfaces) |
| `src/ImageArchive/Manifest/*.cs` | P0 (public models) |
| `src/ImageArchive/Geometry/FrameGeometry.cs` | P0 (constants live) |
| `src/ImageArchive/ImageArchiveEncoder.cs` / `Decoder.cs` | P0 stubs → filled P1–P5 |
| `tests/ImageArchive.UnitTests/*` | P0+ |
| `tests/ImageArchive.IntegrationTests/*` | P0, body from P5/P9 |
| `src/ImageArchive/Pixels/PixelPacker.cs` | P1 |
| `src/ImageArchive/Rendering/*` | P2 |
| `src/ImageArchive/Codecs/Skia/*` | P5 |
| `docs/qr-payload-limits.md` | P2 |

## Verification plan (implementation complete when)

| Check | How |
|-------|-----|
| Multi-TFM build | `dotnet build -f net8.0|net9.0|net10.0` (or single multi-target build) |
| Full tests | `dotnet test` all green unit + integration |
| AC linkage | Grep tests for `AC-FR-` traits; optional policy test TEST-TDD-001 |
| E2E | TEST-E2E-001 passes |
| Planning package still valid | `pwsh -File docs/verify-planning-package.ps1` |
| Receipt still 93/93 unless ACs intentionally changed | regenerate if MCP requirements change |

## Non-goals (this plan)

- Encryption inside the format  
- Non-Skia default codecs (extensions ok via interfaces)  
- Changing RFC geometry/capacity  
- Wiki publish / Azure DevOps sync unless separately requested  

## Risks

| Risk | Mitigation |
|------|------------|
| SkiaSharp APNG multi-frame gaps | Early P5 spike; custom APNG mux around Skia bitmaps if needed |
| QR capacity at 47×47 too small for long URLs | Document max length; fail encode with clear error (AC-FR-HDR-003-4) |
| E2E clone size/time | Shallow clone; exclude bin/obj; long timeout |
| net10-only API accidental use | Build all three TFMs in CI every phase |
| Footer text rendering fidelity | Accept system sans-serif baseline; snapshot or measure bounds in unit tests |

## Task checklist (orchestrator)

- [ ] Promote this plan into `docs/plans/ImageArchive-Implementation-Plan.md` (include full Public API section)
- [ ] P0 scaffold multi-TFM + xUnit v3 + public API stubs
- [ ] P1 geometry + pixels (TDD)
- [ ] P2 header/footer/QR + `docs/qr-payload-limits.md`
- [ ] P3 integrity + metadata
- [ ] P4 manifest validation
- [ ] P5 SkiaSharp PNG/APNG
- [ ] P6 WebP + 60000 ms delay
- [ ] P7 git tar + archive types
- [ ] P8 CLI
- [ ] P9 flagship E2E
- [ ] P10 processors + security
- [ ] Final: full suite green all TFMs + planning package verify

## Deliverable of *this* planning request

On user approval via plan mode:

1. Write the expanded plan to `docs/plans/ImageArchive-Implementation-Plan.md`
2. Do **not** start P0 coding until the user explicitly says implement (unless they approve “implement immediately” in the same breath)

If the user wants planning-only: step 1 only.  
If the user wants execute: steps 1 then P0….

