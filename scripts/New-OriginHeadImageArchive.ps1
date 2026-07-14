<#
.SYNOPSIS
  Encode an ImageArchive of origin HEAD, decode it, extract, and diff against the clone.

.DESCRIPTION
  Resolves origin/HEAD (falling back to origin/main or origin/master), fetches origin
  unless -SkipFetch is set, clones that tip into a work directory, writes a git-type
  manifest (outside the clone so it is not packed), runs ImageArchive.Cli encode,
  decodes the image to a tar.gz, extracts into a separate folder, then recursively
  compares the clone to the extract using the same exclusions as the encoder
  (bin, obj, .vs, node_modules).

.PARAMETER RepoRoot
  Git repository root. Default: parent of this scripts/ directory.

.PARAMETER Output
  Output image path. Default: <RepoRoot>/artifacts/origin-head-<shortSha>.<png|webp>

.PARAMETER ExtractDir
  Folder for the decoded archive tree. Default: <stage>/extracted

.PARAMETER Format
  Container format: png (APNG) or webp. Default: png.

.PARAMETER SkipFetch
  Do not run "git fetch origin" before resolving HEAD.

.PARAMETER SkipVerify
  Encode only; skip decode, extract, and directory compare.

.PARAMETER KeepWorkDir
  Leave the stage directory (clone, extract, tar, manifest) on disk.

.PARAMETER WorkDir
  Explicit stage directory. Default: a unique folder under the system temp path.

.PARAMETER Configuration
  dotnet configuration for ImageArchive.Cli (Debug/Release). Default: Release.

.PARAMETER Framework
  Target framework for running the multi-TFM CLI. Default: net10.0.

.EXAMPLE
  pwsh -File scripts/New-OriginHeadImageArchive.ps1

.EXAMPLE
  pwsh -File scripts/New-OriginHeadImageArchive.ps1 -Output .\artifacts\tip.png -KeepWorkDir
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$Output = "",
    [string]$ExtractDir = "",
    [ValidateSet("png", "webp")]
    [string]$Format = "png",
    [switch]$SkipFetch,
    [switch]$SkipVerify,
    [switch]$KeepWorkDir,
    [string]$WorkDir = "",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("net8.0", "net9.0", "net10.0")]
    [string]$Framework = "net10.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Must match DefaultArchiveSourceLoader.ExcludeDirNames
$script:PackExcludeDirNames = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@("bin", "obj", ".vs", "node_modules"),
    [StringComparer]::OrdinalIgnoreCase)

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found on PATH: $Name"
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$WorkingDirectory = $null
    )
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        if ($WorkingDirectory) {
            $out = & git -C $WorkingDirectory @Arguments 2>&1
        }
        else {
            $out = & git @Arguments 2>&1
        }
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $prev
    }
    $text = ($out | ForEach-Object { "$_" }) -join "`n"
    if ($code -ne 0) {
        throw "git $($Arguments -join ' ') failed (exit $code): $text"
    }
    return $text.Trim()
}

function Resolve-OriginHeadSha {
    param([string]$Root)
    foreach ($ref in @("origin/HEAD", "origin/main", "origin/master")) {
        $prev = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $sha = & git -C $Root rev-parse --verify "$ref^{commit}" 2>$null
            if ($LASTEXITCODE -eq 0 -and $sha) {
                return @{ Sha = $sha.Trim(); Ref = $ref }
            }
        }
        finally {
            $ErrorActionPreference = $prev
        }
    }
    throw "Could not resolve origin/HEAD, origin/main, or origin/master under $Root. Fetch origin first."
}

function Get-OriginDefaultBranch {
    param([string]$Root)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $sym = & git -C $Root symbolic-ref refs/remotes/origin/HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and $sym) {
            $name = ($sym.Trim() -replace '^refs/remotes/origin/', '')
            if ($name) { return $name }
        }
    }
    finally {
        $ErrorActionPreference = $prev
    }
    return $null
}

function Get-FileSha256Hex([string]$Path) {
    $hash = Get-FileHash -Algorithm SHA256 -Path $Path
    return $hash.Hash.ToLowerInvariant()
}

function Test-PathExcluded {
    param(
        [string]$Root,
        [string]$FullPath
    )
    $rel = [System.IO.Path]::GetRelativePath($Root, $FullPath)
    $parts = $rel -split '[\\/]'
    foreach ($p in $parts) {
        if ($script:PackExcludeDirNames.Contains($p)) { return $true }
    }
    return $false
}

function Get-RelativeFileList {
    param([string]$Root, [switch]$ApplyPackExcludes)
    if (-not (Test-Path $Root)) {
        return @()
    }
    $list = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath $Root -File -Recurse -Force | ForEach-Object {
        if ($ApplyPackExcludes -and (Test-PathExcluded -Root $Root -FullPath $_.FullName)) {
            return
        }
        $rel = [System.IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
        if ($rel) { $list.Add($rel) }
    }
    $arr = $list.ToArray()
    [Array]::Sort($arr, [StringComparer]::Ordinal)
    return $arr
}

function Expand-TarGz {
    param(
        [Parameter(Mandatory)][string]$TarGzPath,
        [Parameter(Mandatory)][string]$Destination
    )
    if (-not (Test-Path $TarGzPath)) {
        throw "Tar.gz not found: $TarGzPath"
    }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    # Prefer Windows/libarchive tar when available
    if (Get-Command tar -ErrorAction SilentlyContinue) {
        $prev = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            & tar -xzf $TarGzPath -C $Destination 2>&1 | ForEach-Object { Write-Host $_ }
            if ($LASTEXITCODE -ne 0) {
                throw "tar extract failed (exit $LASTEXITCODE)"
            }
        }
        finally {
            $ErrorActionPreference = $prev
        }
        return
    }

    # Fallback: .NET TarReader via temporary csharp (no extra deps on modern SDK)
    $csx = Join-Path ([System.IO.Path]::GetTempPath()) ("ia-untar-" + [Guid]::NewGuid().ToString("n") + ".cs")
    $code = @"
using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
var tarGz = args[0];
var dest = args[1];
Directory.CreateDirectory(dest);
using var input = File.OpenRead(tarGz);
using var gz = new GZipStream(input, CompressionMode.Decompress);
using var tar = new TarReader(gz);
TarEntry? entry;
while ((entry = tar.GetNextEntry()) != null)
{
    if (entry.EntryType is TarEntryType.Directory) continue;
    var path = Path.Combine(dest, entry.Name.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var fs = File.Create(path);
    entry.DataStream?.CopyTo(fs);
}
"@
    Set-Content -Path $csx -Value $code -Encoding UTF8
    try {
        $projDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ia-untar-proj-" + [Guid]::NewGuid().ToString("n"))
        New-Item -ItemType Directory -Path $projDir | Out-Null
        Set-Content -Path (Join-Path $projDir "untar.csproj") -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
"@
        Copy-Item $csx (Join-Path $projDir "Program.cs")
        & dotnet run --project (Join-Path $projDir "untar.csproj") -c Release -- $TarGzPath $Destination
        if ($LASTEXITCODE -ne 0) { throw "dotnet untar failed (exit $LASTEXITCODE)" }
        Remove-Item -LiteralPath $projDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    finally {
        Remove-Item -LiteralPath $csx -Force -ErrorAction SilentlyContinue
    }
}

function Compare-ArchiveTrees {
    param(
        [Parameter(Mandatory)][string]$ExpectedRoot,
        [Parameter(Mandatory)][string]$ActualRoot
    )
    $expected = Get-RelativeFileList -Root $ExpectedRoot -ApplyPackExcludes
    $actual = Get-RelativeFileList -Root $ActualRoot

    $missing = @($expected | Where-Object { $_ -notin $actual })
    $extra = @($actual | Where-Object { $_ -notin $expected })
    $diffs = [System.Collections.Generic.List[string]]::new()

    foreach ($rel in $expected) {
        if ($rel -notin $actual) { continue }
        $ePath = Join-Path $ExpectedRoot ($rel -replace '/', [IO.Path]::DirectorySeparatorChar)
        $aPath = Join-Path $ActualRoot ($rel -replace '/', [IO.Path]::DirectorySeparatorChar)
        $eHash = (Get-FileHash -Algorithm SHA256 -Path $ePath).Hash
        $aHash = (Get-FileHash -Algorithm SHA256 -Path $aPath).Hash
        if ($eHash -ne $aHash) {
            $diffs.Add($rel)
        }
    }

    return [pscustomobject]@{
        ExpectedCount = $expected.Count
        ActualCount   = $actual.Count
        Missing       = $missing
        Extra         = $extra
        ContentDiffs  = @($diffs)
        Ok            = ($missing.Count -eq 0 -and $extra.Count -eq 0 -and $diffs.Count -eq 0)
    }
}

# --- bootstrap ---
Require-Command git
Require-Command dotnet

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}
$RepoRoot = (Resolve-Path $RepoRoot).Path
if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
    throw "Not a git repository: $RepoRoot"
}

$cliProject = Join-Path $RepoRoot "src\ImageArchive.Cli\ImageArchive.Cli.csproj"
if (-not (Test-Path $cliProject)) {
    throw "CLI project not found: $cliProject"
}

Write-Host "Repo: $RepoRoot"

if (-not $SkipFetch) {
    Write-Host "Fetching origin..."
    [void](Invoke-Git -WorkingDirectory $RepoRoot -Arguments @("fetch", "origin", "--prune"))
}

$head = Resolve-OriginHeadSha -Root $RepoRoot
$sha = $head.Sha
$shortSha = $sha.Substring(0, [Math]::Min(12, $sha.Length))
$originUrl = Invoke-Git -WorkingDirectory $RepoRoot -Arguments @("remote", "get-url", "origin")
$branch = Get-OriginDefaultBranch -Root $RepoRoot

Write-Host "origin HEAD: $sha (via $($head.Ref))"
Write-Host "origin URL:  $originUrl"
if ($branch) { Write-Host "default branch: $branch" }

$createdStage = $false
if (-not $WorkDir) {
    $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ia-origin-head-" + $shortSha + "-" + [Guid]::NewGuid().ToString("n").Substring(0, 8))
    $createdStage = $true
}
if (Test-Path $WorkDir) {
    if ((Get-ChildItem -Force $WorkDir | Measure-Object).Count -gt 0) {
        throw "WorkDir is not empty: $WorkDir"
    }
}
else {
    New-Item -ItemType Directory -Path $WorkDir | Out-Null
    $createdStage = $true
}

$stageFull = (Resolve-Path $WorkDir).Path
$cloneDir = Join-Path $stageFull "clone"
$decodedTarGz = Join-Path $stageFull "decoded.tar.gz"
if (-not $ExtractDir) {
    $ExtractDir = Join-Path $stageFull "extracted"
}
else {
    $ExtractDir = [System.IO.Path]::GetFullPath($ExtractDir)
}

New-Item -ItemType Directory -Force -Path $cloneDir | Out-Null
Write-Host "Stage:   $stageFull"
Write-Host "Clone:   $cloneDir"
Write-Host "Extract: $ExtractDir"

try {
    # Prefer a shallow clone of the default branch when it matches origin/HEAD.
    $cloned = $false
    if ($branch) {
        $prev = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            & git clone --depth 1 --branch $branch --single-branch -- $originUrl $cloneDir 2>&1 | ForEach-Object { Write-Host $_ }
            if ($LASTEXITCODE -eq 0) {
                $tip = (& git -C $cloneDir rev-parse HEAD).Trim()
                if ($tip -eq $sha) {
                    $cloned = $true
                }
                else {
                    Write-Host "Shallow tip $tip != $sha; deepening checkout..."
                    [void](Invoke-Git -WorkingDirectory $cloneDir -Arguments @("fetch", "--depth", "1", "origin", $sha))
                    [void](Invoke-Git -WorkingDirectory $cloneDir -Arguments @("checkout", "--force", $sha))
                    $cloned = $true
                }
            }
        }
        finally {
            $ErrorActionPreference = $prev
        }
    }

    if (-not $cloned) {
        if ((Get-ChildItem -Force $cloneDir | Measure-Object).Count -gt 0) {
            Get-ChildItem -Force $cloneDir | Remove-Item -Recurse -Force
        }
        Write-Host "Cloning full remote (no matching single-branch tip)..."
        [void](Invoke-Git -Arguments @("clone", "--", $originUrl, $cloneDir))
        [void](Invoke-Git -WorkingDirectory $cloneDir -Arguments @("checkout", "--force", $sha))
    }

    $actual = Invoke-Git -WorkingDirectory $cloneDir -Arguments @("rev-parse", "HEAD")
    if ($actual -ne $sha) {
        throw "Clone HEAD $actual does not match origin HEAD $sha"
    }

    # Output image path
    if (-not $Output) {
        $artifacts = Join-Path $RepoRoot "artifacts"
        New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
        $ext = if ($Format -eq "webp") { "webp" } else { "png" }
        $Output = Join-Path $artifacts ("origin-head-" + $shortSha + "." + $ext)
    }
    else {
        $outParent = Split-Path -Parent $Output
        if ($outParent -and -not (Test-Path $outParent)) {
            New-Item -ItemType Directory -Force -Path $outParent | Out-Null
        }
    }
    $Output = [System.IO.Path]::GetFullPath($Output)

    $encoderSha = "0000000000000000000000000000000000000000000000000000000000000000"
    Write-Host "Building ImageArchive.Cli ($Configuration)..."
    & dotnet build $cliProject -c $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }

    $dllCandidates = @(
        Join-Path $RepoRoot "src\ImageArchive.Cli\bin\$Configuration\$Framework\ImageArchive.Cli.dll"
        Join-Path $RepoRoot "src\ImageArchive.Cli\bin\$Configuration\net10.0\ImageArchive.Cli.dll"
        Join-Path $RepoRoot "src\ImageArchive.Cli\bin\$Configuration\net9.0\ImageArchive.Cli.dll"
        Join-Path $RepoRoot "src\ImageArchive.Cli\bin\$Configuration\net8.0\ImageArchive.Cli.dll"
    )
    foreach ($dll in $dllCandidates) {
        if (Test-Path $dll) {
            $encoderSha = Get-FileSha256Hex $dll
            Write-Host "Encoder sha256 from: $dll"
            break
        }
    }

    $repoName = Split-Path -Leaf $RepoRoot
    $commitUrl = ""
    if ($originUrl -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+)') {
        $commitUrl = "https://github.com/$($Matches.owner)/$($Matches.repo)/commit/$sha"
    }
    elseif ($originUrl -match 'dev\.azure\.com') {
        $commitUrl = $originUrl.TrimEnd('/') + "?version=GC$sha"
    }
    else {
        $commitUrl = $originUrl
    }

    $headerQr = $commitUrl
    if ($headerQr.Length -gt 60) {
        $headerQr = $headerQr.Substring(0, 60)
    }

    # Manifest lives outside clone so it is not packed into the git archive payload.
    $manifestPath = Join-Path $stageFull "origin-head.manifest.json"
    $headerText = @"
$repoName
origin HEAD $shortSha
"@.Trim()

    $manifest = [ordered]@{
        version = "1.0.0"
        encoder = [ordered]@{
            name    = "ImageArchive.Cli"
            version = "1.0.0"
            sha256  = $encoderSha
        }
        archive = [ordered]@{
            type             = "git"
            mimeType         = "application/x-git"
            source           = "clone"
            sourceUrl        = $originUrl
            commitHash       = $sha
            originalFileName = "$repoName.git-archive"
        }
        output = [ordered]@{
            path   = $Output.Replace('\', '/')
            format = $Format
        }
        header = [ordered]@{
            type   = "text"
            text   = $headerText
            qrCode = [ordered]@{
                content = $headerQr
                enabled = $true
            }
        }
        frames = @(@{})
    }

    $json = $manifest | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($manifestPath, $json + "`n")

    Write-Host "Manifest: $manifestPath"
    Write-Host "Encoding -> $Output"

    $env:IMAGEARCHIVE_TOOL_COMMIT_URL = $commitUrl
    & dotnet run --project $cliProject -c $Configuration -f $Framework --no-build -- encode --manifest $manifestPath
    if ($LASTEXITCODE -ne 0) {
        throw "imga encode failed (exit $LASTEXITCODE)"
    }

    if (-not (Test-Path $Output)) {
        throw "Expected output image missing: $Output"
    }

    $size = (Get-Item $Output).Length
    Write-Host ""
    Write-Host "Wrote ImageArchive of origin HEAD:"
    Write-Host "  commit: $sha"
    Write-Host "  image:  $Output ($size bytes)"
    Write-Host "  format: $Format"

    if (-not $SkipVerify) {
        Write-Host ""
        Write-Host "Decoding image -> $decodedTarGz"
        & dotnet run --project $cliProject -c $Configuration -f $Framework --no-build -- decode --input $Output --output $decodedTarGz
        if ($LASTEXITCODE -ne 0) {
            throw "imga decode failed (exit $LASTEXITCODE)"
        }
        if (-not (Test-Path $decodedTarGz)) {
            throw "Decoded archive missing: $decodedTarGz"
        }

        if (Test-Path $ExtractDir) {
            Remove-Item -LiteralPath $ExtractDir -Recurse -Force
        }
        Write-Host "Extracting tar.gz -> $ExtractDir"
        Expand-TarGz -TarGzPath $decodedTarGz -Destination $ExtractDir

        Write-Host "Comparing clone (pack excludes applied) to extract..."
        $cmp = Compare-ArchiveTrees -ExpectedRoot $cloneDir -ActualRoot $ExtractDir

        Write-Host "  expected files: $($cmp.ExpectedCount)"
        Write-Host "  actual files:   $($cmp.ActualCount)"

        if (-not $cmp.Ok) {
            if ($cmp.Missing.Count -gt 0) {
                Write-Host "Missing in extract ($($cmp.Missing.Count)):"
                $cmp.Missing | Select-Object -First 50 | ForEach-Object { Write-Host "  - $_" }
                if ($cmp.Missing.Count -gt 50) { Write-Host "  ... and $($cmp.Missing.Count - 50) more" }
            }
            if ($cmp.Extra.Count -gt 0) {
                Write-Host "Extra in extract ($($cmp.Extra.Count)):"
                $cmp.Extra | Select-Object -First 50 | ForEach-Object { Write-Host "  + $_" }
                if ($cmp.Extra.Count -gt 50) { Write-Host "  ... and $($cmp.Extra.Count - 50) more" }
            }
            if ($cmp.ContentDiffs.Count -gt 0) {
                Write-Host "Content mismatches ($($cmp.ContentDiffs.Count)):"
                $cmp.ContentDiffs | Select-Object -First 50 | ForEach-Object { Write-Host "  ~ $_" }
                if ($cmp.ContentDiffs.Count -gt 50) { Write-Host "  ... and $($cmp.ContentDiffs.Count - 50) more" }
            }
            throw "Round-trip verification failed: extract does not match clone."
        }

        Write-Host "Verify OK: no path or content diffs."
    }
    else {
        Write-Host "SkipVerify: decode/extract/compare not run."
    }

    # Machine-readable primary artifact for CI
    Write-Output $Output
}
finally {
    if (-not $KeepWorkDir -and $createdStage -and $WorkDir -and (Test-Path $WorkDir)) {
        try {
            Start-Sleep -Milliseconds 200
            Remove-Item -LiteralPath $WorkDir -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Could not remove stage dir $WorkDir : $_"
        }
    }
    elseif ($KeepWorkDir) {
        Write-Host "Kept stage dir: $WorkDir"
        if (Test-Path $ExtractDir) {
            Write-Host "  extract: $ExtractDir"
        }
    }
}
