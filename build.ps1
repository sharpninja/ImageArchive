[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = "Stop"
$BuildProjectFile = "$PSScriptRoot\build\_build.csproj"

# Ensure .NET SDK
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is required to run the Nuke build."
}

Write-Output "Microsoft (R) .NET SDK version $(& dotnet --version)"
dotnet run --project $BuildProjectFile --no-launch-profile -- $BuildArguments
exit $LASTEXITCODE
