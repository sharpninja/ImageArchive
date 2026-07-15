[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = "Stop"
$BuildDir = Join-Path $PSScriptRoot "build"
$BuildProjectFile = Join-Path $BuildDir "_build.csproj"
# Nuke host is net10.0; default local configuration matches `dotnet run` (Debug).
$HostConfig = if ($env:NUKE_BUILD_CONFIGURATION) { $env:NUKE_BUILD_CONFIGURATION } else { "Debug" }
$HostExe = Join-Path $BuildDir "bin\$HostConfig\_build.exe"
$HostDll = Join-Path $BuildDir "bin\$HostConfig\_build.dll"

# Ensure .NET SDK
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is required to run the Nuke build."
}

function Get-NukeSourceFiles {
    Get-ChildItem -Path $BuildDir -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object {
            $rel = $_.FullName.Substring($BuildDir.Length).TrimStart('\', '/')
            if ($rel -match '^(bin|obj)([\\/]|$)') { return $false }
            $ext = $_.Extension
            $ext -in '.cs', '.csproj', '.props', '.targets', '.json'
        }
}

function Test-NukeSourcesNeedBuild {
    param([string]$BinaryPath)

    if (-not (Test-Path -LiteralPath $BinaryPath)) {
        return $true
    }

    $sources = @(Get-NukeSourceFiles)
    if ($sources.Count -eq 0) {
        # No sources found; prefer rebuild if binary is missing only (already checked).
        return $false
    }

    foreach ($f in $sources) {
        if (($f.Attributes -band [System.IO.FileAttributes]::Archive) -ne 0) {
            return $true
        }
    }
    return $false
}

function Clear-NukeSourceArchiveFlags {
    foreach ($f in Get-NukeSourceFiles) {
        try {
            $f.Attributes = $f.Attributes -band (-bnot [System.IO.FileAttributes]::Archive)
        }
        catch {
            # Best-effort; do not fail the build host launch.
        }
    }
}

Write-Output "Microsoft (R) .NET SDK version $(& dotnet --version)"

$needBuild = Test-NukeSourcesNeedBuild -BinaryPath $HostExe
if (-not $needBuild -and -not (Test-Path -LiteralPath $HostDll)) {
    $needBuild = $true
}

if ($needBuild) {
    Write-Output "Nuke host: building ($HostConfig) — source archive flag set or binary missing."
    & dotnet build $BuildProjectFile -c $HostConfig --nologo
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Clear-NukeSourceArchiveFlags
    Write-Output "Nuke host: archive flags cleared on build sources."
}
else {
    Write-Output "Nuke host: up-to-date (no archive flags on build sources); using $HostExe"
}

if (-not (Test-Path -LiteralPath $HostExe)) {
    throw "Nuke host binary not found after build: $HostExe"
}

# Run the already-built host so MSBuild never tries to rewrite a locked _build.dll.
& $HostExe @BuildArguments
exit $LASTEXITCODE
