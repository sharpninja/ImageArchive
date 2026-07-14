# Structural verification of ImageArchive planning package (RFC 1.0.0 requirements + plan).
# Exit 0 only when acceptance criteria for the planning goal hold.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $root "docs/ImageArchive-RFC.md"))) {
  $root = Get-Location
}

$failures = [System.Collections.Generic.List[string]]::new()
function Ok($m) { Write-Output "OK  $m" }
function Fail($m) { $script:failures.Add($m); Write-Output "FAIL $m" }

# 1) Schema streamSha256
$schemaPath = Join-Path $root "schema/imagearchive-schema.json"
if (-not (Test-Path $schemaPath)) { Fail "missing $schemaPath" }
else {
  $schema = Get-Content $schemaPath -Raw | ConvertFrom-Json
  $p = $schema.properties.streamSha256
  if (-not $p) { Fail "schema missing properties.streamSha256" }
  elseif ($p.pattern -ne '^[a-fA-F0-9]{64}$') { Fail "streamSha256 pattern unexpected: $($p.pattern)" }
  else { Ok "schema streamSha256 pattern=$($p.pattern)" }
}

# 2) Receipt 100% coverage + matrix shape
$receiptPath = Join-Path $root "docs/receipts-requirements-rfc-1.0.0.md"
if (-not (Test-Path $receiptPath)) { Fail "missing receipt" }
else {
  $r = Get-Content $receiptPath -Raw
  if ($r -notmatch 'PASS:\s*100%\s*AC coverage\s*\(93/93\)') { Fail "receipt missing PASS 93/93" }
  else { Ok "receipt PASS 93/93" }
  $bad = 0
  foreach ($row in (Get-Content $receiptPath | Where-Object { $_ -match '^\| AC-FR-' })) {
    $parts = ($row -split '\|') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
    if ($parts.Count -lt 3) { $bad++; continue }
    if ($parts[1] -notmatch '^FR-[A-Z0-9]+-\d{3}$') { $bad++ }
    if ($parts[2] -notmatch 'TEST-') { $bad++ }
  }
  if ($bad -gt 0) { Fail "receipt matrix bad rows=$bad" }
  else { Ok "receipt matrix rows well-formed" }
}

# 3) Implementation plan locks
$planPath = Join-Path $root "docs/plans/ImageArchive-Implementation-Plan.md"
if (-not (Test-Path $planPath)) { Fail "missing implementation plan" }
else {
  $p = Get-Content $planPath -Raw
  foreach ($need in @('SkiaSharp','net8.0','net9.0','net10.0','xUnit v3','.git')) {
    if ($p -notmatch [regex]::Escape($need)) { Fail "impl plan missing $need" }
    else { Ok "impl plan has $need" }
  }
  foreach ($phase in 0..10) {
    # Accept **P0**, ### P0, or | **P0** |
    if ($p -notmatch "\*\*P$phase\*\*" -and $p -notmatch "###\s*P$phase\b" -and $p -notmatch "(?m)^### P$phase") {
      Fail "impl plan missing phase P$phase"
    }
  }
  if ($failures | Where-Object { $_ -match 'phase P' }) { }
  else { Ok "impl plan phases P0-P10 present" }
  if ($p -notmatch 'clone' -or $p -notmatch 'extract' -or $p -notmatch 'compare') {
    Fail "impl plan E2E missing clone/extract/compare"
  } else { Ok "impl plan E2E keywords present" }
}

# 4) Exports + README
$exports = @(
  "docs/Project/Functional-Requirements.md",
  "docs/Project/Technical-Requirements.md",
  "docs/Project/Testing-Requirements.md",
  "docs/Project/Requirements-Matrix.md",
  "docs/Project/TR-per-FR-Mapping.md"
)
foreach ($rel in $exports) {
  $full = Join-Path $root $rel
  if (-not (Test-Path $full)) { Fail "missing $rel" }
  elseif ((Get-Item $full).Length -lt 100) { Fail "too small $rel" }
  else { Ok "export $rel bytes=$((Get-Item $full).Length)" }
}
$readme = Get-Content (Join-Path $root "README.md") -Raw
if ($readme -notmatch 'receipts-requirements-rfc-1.0.0') { Fail "README missing receipt link" }
else { Ok "README links receipt" }
if ($readme -notmatch 'ImageArchive-Implementation-Plan') { Fail "README missing impl plan link" }
else { Ok "README links implementation plan" }

# 5) SkiaSharp lock in technical export
$tech = Get-Content (Join-Path $root "docs/Project/Technical-Requirements.md") -Raw
if ($tech -notmatch 'TR-CONT-SKIA-001' -or $tech -notmatch 'SkiaSharp') {
  Fail "technical requirements missing SkiaSharp lock"
} else { Ok "TR-CONT-SKIA-001 / SkiaSharp in technical export" }

Write-Output ""
if ($failures.Count -eq 0) {
  Write-Output "PLANNING_PACKAGE_VERIFY=PASS"
  exit 0
} else {
  Write-Output "PLANNING_PACKAGE_VERIFY=FAIL count=$($failures.Count)"
  $failures | ForEach-Object { Write-Output " - $_" }
  exit 1
}
