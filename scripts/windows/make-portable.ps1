<#
.SYNOPSIS
    Produce a clean, portable FreeFlow layout: a single subfolder holding the
    whole self-contained app, with a launcher shortcut at the root so the
    executable is easy to find and the "mess" (runtime DLLs, resources) stays
    tucked away.

.DESCRIPTION
    A self-contained WinUI 3 app cannot have its DLLs relocated into a subfolder
    while leaving only the exe at the root: the .NET host resolves framework
    assemblies by flat paths next to the exe, and WinUI's WinRT activation
    manifest registers its native DLLs next to the exe. Both break if moved.

    The robust, idiomatic Windows layout is therefore:

        <OutputDir>\
            FreeFlow.lnk          <- launcher shortcut (the "executable outside")
            FreeFlow\             <- everything else (the app + runtime)
                FreeFlow.App.exe
                ...all DLLs/resources...

    Double-clicking FreeFlow.lnk runs FreeFlow\FreeFlow.App.exe with its working
    directory set to the app folder, so all resolution works exactly as a normal
    publish. The shortcut carries the app icon.

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

    [string]$Version = '0.1.0',

    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$appProject = Join-Path $repoRoot 'windows' 'src' 'FreeFlow.App' 'FreeFlow.App.csproj'
$iconPath = Join-Path $repoRoot 'windows' 'src' 'FreeFlow.App' 'Assets' 'AppIcon.ico'

$rid = "win-$($Platform.ToLowerInvariant())"
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'artifacts' 'portable' $rid
}

$appFolderName = 'FreeFlow'
$appDir = Join-Path $OutputDir $appFolderName
$shortcutPath = Join-Path $OutputDir 'FreeFlow.lnk'

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

Write-Host "Creating launcher shortcut: $shortcutPath" -ForegroundColor Cyan
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($shortcutPath)
$lnk.TargetPath = $targetExe
$lnk.WorkingDirectory = $appDir
$lnk.Description = 'FreeFlow'
if (Test-Path $iconPath) { $lnk.IconLocation = "$iconPath,0" }
$lnk.Save()

$appItemCount = (Get-ChildItem $appDir | Measure-Object).Count
Write-Host "`nDone." -ForegroundColor Green
Write-Host "Portable layout: $OutputDir" -ForegroundColor Green
Write-Host "  FreeFlow.lnk           (launch this)" -ForegroundColor Green
Write-Host "  $appFolderName\  ($appItemCount top-level items)" -ForegroundColor Green
