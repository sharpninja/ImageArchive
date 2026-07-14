# Functional Requirements (MCP Server)

## FR-ANIM-001 Frame delay

RFC §11 | medium. 60000 ms delay; decoder ignores timing.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] APNG/WebP animation delay written as 60000 ms per frame.
- [ ] Decoder extracts payload without depending on delay values.

## FR-ARCH-001 Archive type ingestion

RFC §1 §2 | high. git = compressed tar of .git + worktree; zip/tar/raw files.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] type=git with directory source produces a compressed tarball of .git directory plus working tree (not git archive tree-only).
- [ ] type=zip|tar|raw reads file source as the byte stream.
- [ ] mimeType from manifest is written to metadata mimeType.
- [ ] For git type, payload is compressed tar of .git + worktree suitable for extract-and-compare against a clone.

## FR-CLI-001 CLI encode/decode

RFC §15 | high. CLI encode --manifest; decode --input --output.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] encode --manifest <path> writes output defined by manifest output.path.
- [ ] decode --input <image> --output <dir-or-file> extracts archive bytes/files.
- [ ] Non-zero exit codes on validation/integrity/IO failures; zero on success.
- [ ] CLI project lives at src/ImageArchive.Cli (when implemented).

## FR-CONT-001 Supported containers

RFC §12 | critical. APNG and animated WebP via default SkiaSharp codec.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Encode to output.format=png produces a valid multi-frame PNG (APNG semantics for multi-frame).
- [ ] Encode to output.format=webp produces a valid animated WebP.
- [ ] Output file content-type / detection matches chosen container.

## FR-DEC-001 Decode end-to-end

RFC goals | critical. Decode ImageArchive to archive bytes.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Round-trip: encode then decode yields byte-identical original archive stream (absent pre/post processors).
- [ ] Decode fails closed on per-frame integrity failure.
- [ ] Decode exposes/reads required metadata fields.

## FR-E2E-001 Full clone-tar-encode-validate-extract integration

Flagship xUnit v3 E2E. Clone HEAD, tar.gz .git+worktree, encode, validate QR+metadata, extract, compare to clone.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Test clones HEAD of this ImageArchive repo into a temp directory (isolated clone).
- [ ] Test creates a compressed tarball of that clone .git + working tree.
- [ ] Test encodes the tarball into an ImageArchive image (APNG at minimum) using a valid manifest.
- [ ] Test validates every frame left QR decodes to hex SHA-256 of that frame data-region bytes; footer Line2 matches; header QR matches manifest when enabled; right QR matches tool commit URL policy.
- [ ] Test validates required metadata text chunks match encode-time fields and embedded jsonManifest/jsonSchema.
- [ ] Test extracts payload tarball and compares extracted tree to cloned folder (recursive hashes per TR-E2E-CMP-001).
- [ ] Test project uses xUnit v3 and multi-targets net8.0;net9.0;net10.0 where practical.

## FR-ENC-001 Encode end-to-end

RFC goals | critical. Encode archive+manifest to ImageArchive image.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Given valid manifest + source, When encode completes, Then output path exists and is non-empty.
- [ ] Encoded file satisfies GEOM, PIXEL, HDR, FTR, META, INTG, ANIM, CONT ACs applicable to the chosen format.
- [ ] Multi-frame count is ceil(streamLength / 2838528) with defined final-frame padding (TR-PIXEL-PAD-001).

## FR-EXT-001 Pluggable image codecs

RFC §13 | high. IImageEncoder/IImageDecoder; default SkiaSharp.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Core encode/decode depends on interfaces, not concrete PNG/WebP types.
- [ ] Registering a mock encoder/decoder is sufficient for unit tests of core packing/stream logic.

## FR-FTR-001 Footer layout

RFC §6 | critical. White footer; left QR; center text; right QR.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Footer background is pure white before drawing elements.
- [ ] Left element is 50x50 QR with same margin scheme as header QR.
- [ ] Center text block: black, system sans-serif, 8 pt; Line1 Frame N of M; Line2 hex SHA-256 of this frame raw data bytes.
- [ ] Right element is 50x50 QR, right-aligned; content is canonical URL of the commit of the ImageArchive tool that produced the file.

## FR-FTR-002 Footer left QR content binding

RFC §6 §8 | critical. Left QR = frame data SHA-256 hex.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Left QR decoded text equals the hex SHA-256 of the exact bytes written to that frame data region.
- [ ] Footer Line2 text equals the same hex digest as the left QR.

## FR-GEOM-001 Fixed frame dimensions

RFC §3 | critical. Every encoded frame must be exactly 1024x1024 pixels.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Given any encoded ImageArchive frame, When dimensions are read, Then width is 1024 and height is 1024.
- [ ] Given an encode request that would produce a non-1024x1024 canvas, When encode runs, Then the encoder rejects or normalizes strictly to 1024x1024 (no silent other sizes).

## FR-GEOM-002 Mandatory region layout

RFC §3 | critical. Header 0-49, Data 50-973, Footer 974-1023.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Header occupies exactly rows 0-49 (50 px).
- [ ] Data occupies exactly rows 50-973 (924 px).
- [ ] Footer occupies exactly rows 974-1023 (50 px).
- [ ] Data region capacity is exactly 1024 x 924 x 3 = 2,838,528 bytes per frame.

## FR-HDR-001 Header background and free-form area

RFC §5 | high. White header; free-form text/image/folder; 1024x50 full-bleed image; folder round-robin.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Header pixels default to RGB(255,255,255) before free-form paint.
- [ ] Free-form content may be text, single image, or numbered image sequence (one per frame, cycling if needed).
- [ ] Free-form is rendered before QR composite.
- [ ] Given a 1024x50 image header on a frame, When the frame is rendered, Then the free-form/header bitmap fills the full 1024x50 header band before QR composite (right 50x50 may be overwritten by QR).
- [ ] Given a folder of numbered images as header source, When multi-frame encode runs, Then frames round-robin through the ordered image set (frame i uses image at index i % count).

## FR-HDR-002 Header QR reservation and composite

RFC §5 | critical. Rightmost 50x50 QR 47x47 + margins.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] QR module area is 47x47 with margins: 2 px top and right, 1 px bottom and left (total 50x50).
- [ ] Margin colour is white.
- [ ] QR is right-aligned in the header and drawn after free-form content (overwrites free-form under the 50x50).

## FR-HDR-003 Header QR content

RFC §5 | high. User-defined QR content; max length per TR-HDR-QR-002.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Encoder writes user-supplied QR content from manifest header.qrCode.content when enabled.
- [ ] When header.qrCode.enabled is false, no header QR payload is required.
- [ ] Empty content is allowed for non-git archives.
- [ ] Encoder rejects or truncates per documented max payload length for the chosen QR library/ECC at 47x47.

## FR-INTG-001 Per-frame integrity encode

RFC §8 | critical. SHA-256 of data region in footer text and left QR.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] For every frame, SHA-256 input is exactly the raw data-region bytes (not header/footer).
- [ ] Hex digest is lowercase a-f0-9, 64 chars; same string used in text and QR.

## FR-INTG-002 Per-frame integrity decode reject

RFC §8 | critical. Decoder rejects on hash mismatch.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Given a valid archive with one data-region byte flipped, When decode validates, Then decode fails with integrity error identifying the frame index.
- [ ] Given all frames match, When decode validates, Then validation succeeds.

## FR-INTG-003 Whole-stream integrity in manifest

RFC §9 | high. streamSha256 of concatenated data-region bytes in jsonManifest.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Manifest includes top-level streamSha256 (64 hex) of concatenated data-region bytes (padding included per TR).
- [ ] Decoder can recompute whole-stream hash and compare to manifest streamSha256.

## FR-LIB-001 C# library Stream API

RFC §15 | high. Stream APIs under GPL-3.0; multi-TFM net8/9/10.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Public encode API accepts input Stream + manifest model and produces output Stream or file.
- [ ] Public decode API accepts image Stream and produces archive Stream + metadata model.
- [ ] Library project lives at src/ImageArchive (when implemented).

## FR-MANF-001 Manifest-driven encode

RFC §10 | critical. JSON manifest conforms to schema/imagearchive-schema.json.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Valid example manifest shape is accepted.
- [ ] Manifest missing required keys (version, encoder, archive, output, frames) is rejected with validation errors.
- [ ] version must be const 1.0.0.
- [ ] frames minItems is 1.
- [ ] encoder.sha256 matches ^[a-fA-F0-9]{64}$.
- [ ] archive.type enum git|zip|tar|raw; output.format enum png|webp.
- [ ] additionalProperties false at root is enforced.

## FR-MANF-002 Header configuration in manifest

RFC §5 schema header | high. text|image|folder; per-frame override.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Header type text uses text field for free-form rendering.
- [ ] Header type image uses imagePath.
- [ ] Header type folder uses folderPath for numbered image sequence / cycling.
- [ ] Per-frame frames[i].header overrides default header when present.

## FR-META-001 Required text-chunk fields

RFC §7 | critical. Required camelCase metadata fields in container text chunks.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Required fields always present: encoderName, encoderVersion, encoderSha256, mimeType, archiveType, jsonSchema, jsonManifest.
- [ ] Field names are camelCase exactly as RFC table.
- [ ] archiveType is one of git, zip, tar, raw.

## FR-META-002 Optional text-chunk fields

RFC §7 | medium. Optional sourceUrl, commitHash, originalFileName; free-form chunks allowed.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Optional fields omitted when not supplied (no empty-required failures).
- [ ] Encoder may emit additional free-form text chunks without failing decode of required fields.

## FR-META-003 Metadata source of truth

RFC §7 §10 | high. jsonManifest exact; jsonSchema embedded.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Decoded jsonManifest round-trips to the manifest document used at encode time (semantic equality).
- [ ] Decoded jsonSchema is a valid JSON Schema document for ImageArchive manifest RFC 1.0.0.

## FR-PIXEL-001 RGB-only payload channels

RFC §4 | critical. Only RGB channels carry payload; alpha fully opaque if present.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Payload bytes map exclusively to R, G, B in that channel order within each pixel.
- [ ] When the container has an alpha channel, every pixel alpha in the frame is 255.
- [ ] Non-payload visual regions (header/footer) do not contribute to the concatenated archive byte stream.

## FR-PIXEL-002 Scan order and frame concatenation

RFC §4 | critical. L->R, T->B; frames concat 0,1,...
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Within a frame data region, byte 0 is top-left pixel R, then G, B, then next pixel L->R, then next row top->bottom.
- [ ] Stream offset after frame i accounts for frame capacity and final partial frame padding policy (TR-PIXEL-PAD-001).
- [ ] Decoder reconstructs the same continuous byte stream order as encoder wrote.

## FR-PIXEL-003 Core vs container packing responsibility

RFC §4 | high. Core supplies Stream; IImageEncoder packs pixels. Default codec SkiaSharp.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Core encode API accepts/produces Stream of archive bytes independent of PNG/WebP specifics.
- [ ] IImageEncoder implementation performs pixel packing and container write.

## FR-PROC-001 Optional stream processors

Schema preprocessor/postprocessor | medium.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] When preprocessor is null/absent, source bytes encode unchanged.
- [ ] When postprocessor is null/absent, decode emits extracted bytes unchanged.
- [ ] When set, stream is piped through the external command; non-zero exit fails the operation.

## FR-SEC-001 Crypto and encryption scope

RFC §14 | high. No format encryption; SHA-256 only for integrity.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Format pipeline does not encrypt/decrypt payload bytes itself.
- [ ] Only SHA-256 is used for format integrity hashes.
- [ ] Manifest preprocessor/postprocessor strings are invoked as external commands when set.

