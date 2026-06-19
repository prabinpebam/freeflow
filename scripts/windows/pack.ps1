#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes self-contained FreeFlow Windows builds and packages them with
    Velopack (vpk) for in-app auto-update over GitHub Releases.

.DESCRIPTION
    Implements the packaging pipeline described in
    docs/windows/packaging-and-updates.md (per ADR-001 / ADR-002):

      1. dotnet publish FreeFlow.App as a self-contained, unpackaged Windows App
         SDK app for each requested RID (win-x64, win-arm64), Release config.
      2. vpk pack each published folder into a Velopack release set
         (full + delta packages + Setup.exe) under artifacts/release/<rid>.

    Code signing is OPTIONAL and OFF by default. When -SignParams is supplied it
    is forwarded to vpk so the installer and app are Authenticode-signed
    (Windows Application Signing per the release checklist, gate 4). The MSI
    flavor (WiX v4) is intentionally produced by a separate signed-MSI job; this
    script focuses on the Velopack update channel that the running app consumes.

    The script does NOT publish to GitHub; it produces artifacts you can attach
    to a GitHub Release (or upload via `vpk upload github`) after review.

.PARAMETER Version
    SemVer version to stamp (e.g. 0.1.0 or 0.2.0-beta.1). Defaults to the
    <Version> in windows/Directory.Build.props.

.PARAMETER Runtimes
    Runtime identifiers to build. Defaults to win-x64, win-arm64.

.PARAMETER Channel
    Velopack channel: "stable" or "beta". Beta builds are offered only to users
    who opted into the beta channel (see UpdatePlanner / ReleaseChannel).

.PARAMETER SignParams
    Optional signtool parameters string forwarded to vpk (e.g.
    "/a /fd sha256 /tr http://timestamp.digicert.com /td sha256"). When omitted,
    packages are produced UNSIGNED (dev/test only).

.PARAMETER OutputDir
    Root output directory. Defaults to artifacts/release.

.EXAMPLE
    pwsh -File scripts/windows/pack.ps1 -Version 0.1.0

.EXAMPLE
    pwsh -File scripts/windows/pack.ps1 -Version 0.2.0-beta.1 -Channel beta -Runtimes win-x64
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [ValidateSet('stable', 'beta')]
    [string]$Channel = 'stable',
    [string]$SignParams,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$appProject = Join-Path $repoRoot 'windows' 'src' 'FreeFlow.App' 'FreeFlow.App.csproj'
$propsFile = Join-Path $repoRoot 'windows' 'Directory.Build.props'
$packId = 'FreeFlow'
$packTitle = 'FreeFlow'

function Get-PropsVersion {
    [xml]$props = Get-Content -LiteralPath $propsFile
    $node = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $node) { throw "No <Version> found in $propsFile" }
    return "$node".Trim()
}

if (-not $Version) { $Version = Get-PropsVersion }
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'artifacts' 'release' }

Write-Host "FreeFlow packaging" -ForegroundColor Cyan
Write-Host "  version : $Version"
Write-Host "  channel : $Channel"
Write-Host "  runtimes: $($Runtimes -join ', ')"
Write-Host "  output  : $OutputDir"

# Ensure the Velopack CLI (vpk) is available; install as a local-ish global tool if missing.
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "vpk not found; installing Velopack CLI (dotnet tool)..." -ForegroundColor Yellow
    dotnet tool install --global vpk
    if ($LASTEXITCODE -ne 0) { throw "Failed to install vpk. Install manually: dotnet tool install --global vpk" }
    $env:PATH = "$env:PATH;$([System.IO.Path]::Combine($HOME, '.dotnet', 'tools'))"
}

foreach ($rid in $Runtimes) {
    Write-Host "`n=== $rid ===" -ForegroundColor Cyan

    $publishDir = Join-Path $repoRoot 'artifacts' 'publish' $rid
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    $platform = switch ($rid) {
        'win-x64' { 'x64' }
        'win-arm64' { 'ARM64' }
        'win-x86' { 'x86' }
        default { throw "Unsupported runtime '$rid'." }
    }

    Write-Host "publish -> $publishDir"
    dotnet publish $appProject `
        -c Release `
        -r $rid `
        -p:Platform=$platform `
        -p:Version=$Version `
        --self-contained true `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid." }

    $relDir = Join-Path $OutputDir $rid
    New-Item -ItemType Directory -Force -Path $relDir | Out-Null

    $vpkArgs = @(
        'pack',
        '--packId', $packId,
        '--packTitle', $packTitle,
        '--packVersion', $Version,
        '--packDir', $publishDir,
        '--mainExe', 'FreeFlow.App.exe',
        '--outputDir', $relDir,
        '--channel', $Channel
    )
    if ($SignParams) {
        $vpkArgs += @('--signParams', $SignParams)
    }
    else {
        Write-Host "WARNING: building UNSIGNED packages (no -SignParams). Dev/test only." -ForegroundColor Yellow
    }

    Write-Host "vpk pack -> $relDir"
    vpk @vpkArgs
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed for $rid." }
}

Write-Host "`nDone. Velopack artifacts under: $OutputDir" -ForegroundColor Green
Write-Host "Next: attach the release set to a GitHub Release (or 'vpk upload github')." -ForegroundColor Green
Write-Host "See docs/windows/packaging-and-updates.md and release-checklist.md (gate 4)." -ForegroundColor Green
