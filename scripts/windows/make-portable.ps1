<#
.SYNOPSIS
    Produce a clean, portable FreeFlow layout: a real FreeFlow.exe at the root
    with all DLLs, language packs, and other files tucked into an "app"
    subfolder.

.DESCRIPTION
    A self-contained WinUI 3 app cannot have its DLLs relocated into a subfolder
    while leaving only its own exe at the root: the .NET host resolves framework
    assemblies by flat paths next to the exe, and WinUI's WinRT activation
    manifest registers its native DLLs next to the exe. Both break if moved
    (verified: "System.Runtime not found" / "ClassFactory cannot supply
    requested class").

    So the whole self-contained app is published into an "app" subfolder, and a
    tiny standalone launcher (scripts/windows/launcher/Launcher.cs, compiled to
    FreeFlow.exe) is placed at the root. The launcher targets .NET Framework 4.x
    (present on all supported Windows), so the root exe has no extra dependency
    of its own. It starts app\FreeFlow.App.exe with the working directory set to
    the app folder, so all assembly/native/WinRT resolution works exactly as a
    normal publish.

    Resulting layout:

        <OutputDir>\
            FreeFlow.exe      <- double-click this
            app\              <- all DLLs / language packs / resources / runtime
                FreeFlow.App.exe
                ...

.PARAMETER Platform
    x86 (default), x64, or ARM64.

.PARAMETER Version
    Version stamped into the publish. Defaults to 0.1.0.

.PARAMETER OutputDir
    Destination root. Defaults to artifacts\portable\<rid>.
#>
[CmdletBinding()]
param(
    [ValidateSet('x86', 'x64', 'ARM64')]
    [string]$Platform = 'x86',

    [string]$Version = '0.2.0',

    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$appProject = Join-Path $repoRoot 'windows' 'src' 'FreeFlow.App' 'FreeFlow.App.csproj'
$iconPath = Join-Path $repoRoot 'windows' 'src' 'FreeFlow.App' 'Assets' 'AppIcon.ico'
$launcherSrc = Join-Path $PSScriptRoot 'launcher' 'Launcher.cs'

$rid = "win-$($Platform.ToLowerInvariant())"
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'artifacts' 'portable' $rid
}

$appDir = Join-Path $OutputDir 'app'
$launcherExe = Join-Path $OutputDir 'FreeFlow.exe'

Write-Host "Publishing self-contained $rid (Platform=$Platform, Version=$Version)..." -ForegroundColor Cyan

# Clean destination so stale files never linger.
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $appDir | Out-Null

dotnet publish $appProject `
    -c Release `
    -r $rid `
    -p:Platform=$Platform `
    -p:Version=$Version `
    --self-contained true `
    -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid." }

$targetExe = Join-Path $appDir 'FreeFlow.App.exe'
if (-not (Test-Path $targetExe)) { throw "Published exe not found at $targetExe." }

Write-Host "Compiling root launcher FreeFlow.exe..." -ForegroundColor Cyan

# .NET Framework C# compiler is present on every Windows install. The /platform
# matches the app so the launcher bitness is consistent; the launcher itself has
# no managed dependencies beyond the always-present .NET Framework 4.x.
$cscPlatform = if ($Platform -ieq 'x86') { 'x86' } elseif ($Platform -ieq 'arm64') { 'anycpu' } else { 'x64' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw "Could not find the .NET Framework C# compiler (csc.exe)." }

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    "/platform:$cscPlatform",
    "/out:$launcherExe",
    '/reference:System.dll',
    '/reference:System.Windows.Forms.dll'
)
if (Test-Path $iconPath) { $cscArgs += "/win32icon:$iconPath" }
$cscArgs += $launcherSrc

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Launcher compilation failed." }
if (-not (Test-Path $launcherExe)) { throw "Launcher exe was not produced." }

$appItemCount = (Get-ChildItem $appDir | Measure-Object).Count
$launcherKb = [math]::Round((Get-Item $launcherExe).Length / 1KB, 1)
Write-Host "`nDone." -ForegroundColor Green
Write-Host "Portable layout: $OutputDir" -ForegroundColor Green
Write-Host "  FreeFlow.exe   ($launcherKb KB launcher — double-click this)" -ForegroundColor Green
Write-Host "  app\           ($appItemCount top-level items: DLLs, language packs, resources)" -ForegroundColor Green
