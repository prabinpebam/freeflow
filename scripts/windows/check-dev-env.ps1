#requires -Version 5.1
<#
.SYNOPSIS
    Verifies the local Windows development environment for the FreeFlow Windows port.

.DESCRIPTION
    Checks for the tools and SDKs required to build the .NET 8 + WinUI 3 (Windows App SDK)
    port described in docs/windows/porting-plan.md and docs/windows/dev-environment.md.

    The script is read-only: it does not install anything. It prints a PASS / WARN / FAIL
    report with remediation hints, and exits non-zero if any required check fails.

.PARAMETER SkipNetworkCheck
    Skip the optional NuGet connectivity check (useful on offline machines).

.EXAMPLE
    pwsh -File scripts/windows/check-dev-env.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\windows\check-dev-env.ps1 -SkipNetworkCheck
#>
[CmdletBinding()]
param(
    [switch]$SkipNetworkCheck
)

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'

# Minimum versions / targets the port depends on.
$MinDotnetMajor       = 8
$MinWindowsSdkVersion = [version]'10.0.19041.0'   # Required for WinRT (Windows.Graphics.Capture) + packaging
$MinWindowsBuild      = 19041                      # Windows 10 2004; Win11 is preferred

$script:Results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('PASS', 'WARN', 'FAIL', 'INFO')][string]$Status,
        [string]$Detail = '',
        [string]$Fix = ''
    )
    $script:Results.Add([pscustomobject]@{
        Name   = $Name
        Status = $Status
        Detail = $Detail
        Fix    = $Fix
    })
}

function Get-CommandPath {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source } else { return $null }
}

Write-Host ''
Write-Host 'FreeFlow Windows dev-environment check' -ForegroundColor Cyan
Write-Host '======================================' -ForegroundColor Cyan

# --- 1. Operating system ---------------------------------------------------
$os = [System.Environment]::OSVersion.Version
if ($os.Major -ge 10 -and $os.Build -ge $MinWindowsBuild) {
    $edition = if ($os.Build -ge 22000) { 'Windows 11' } else { 'Windows 10' }
    Add-Result -Name 'Operating system' -Status 'PASS' -Detail "$edition (build $($os.Build))"
} else {
    Add-Result -Name 'Operating system' -Status 'FAIL' -Detail "Build $($os.Build) is below required $MinWindowsBuild" -Fix 'Update Windows to 10 2004+ / Windows 11.'
}

# --- 2. PowerShell ---------------------------------------------------------
$psv = $PSVersionTable.PSVersion
if ($psv.Major -ge 7) {
    Add-Result -Name 'PowerShell' -Status 'PASS' -Detail "v$psv"
} else {
    Add-Result -Name 'PowerShell' -Status 'WARN' -Detail "v$psv (Windows PowerShell)" -Fix 'Install PowerShell 7+: winget install Microsoft.PowerShell'
}

# --- 3. .NET 8 SDK ---------------------------------------------------------
$dotnet = Get-CommandPath 'dotnet'
if ($dotnet) {
    $sdks = & dotnet --list-sdks 2>$null
    $net8 = @($sdks | Where-Object { $_ -match '^8\.' })
    if ($net8.Count -gt 0) {
        $latest8 = ($net8 | ForEach-Object { ($_ -split '\s')[0] } | Sort-Object { [version]$_ } | Select-Object -Last 1)
        Add-Result -Name '.NET SDK 8.x' -Status 'PASS' -Detail "$latest8"
    } else {
        $any = ($sdks | ForEach-Object { ($_ -split '\s')[0] }) -join ', '
        Add-Result -Name '.NET SDK 8.x' -Status 'FAIL' -Detail "Found: $any" -Fix 'winget install Microsoft.DotNet.SDK.8'
    }
} else {
    Add-Result -Name '.NET SDK 8.x' -Status 'FAIL' -Detail 'dotnet not found on PATH' -Fix 'winget install Microsoft.DotNet.SDK.8'
}

# --- 4. .NET workloads (informational) -------------------------------------
if ($dotnet) {
    $wl = & dotnet workload list 2>$null
    $wlJoined = ($wl | Out-String)
    if ($wlJoined -match 'No workloads installed' -or [string]::IsNullOrWhiteSpace($wlJoined)) {
        Add-Result -Name '.NET workloads' -Status 'INFO' -Detail 'None installed (WinUI 3 unpackaged builds use NuGet, so this is usually fine).'
    } else {
        Add-Result -Name '.NET workloads' -Status 'INFO' -Detail 'Installed workloads present.'
    }
}

# --- 5. Windows SDK --------------------------------------------------------
$kitsBin = 'C:\Program Files (x86)\Windows Kits\10\bin'
$sdkVersions = @()
if (Test-Path $kitsBin) {
    $sdkVersions = Get-ChildItem $kitsBin -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^10\.0\.\d+\.\d+$' } |
        ForEach-Object { [version]$_.Name }
}
$goodSdk = @($sdkVersions | Where-Object { $_ -ge $MinWindowsSdkVersion } | Sort-Object)
if ($goodSdk.Count -gt 0) {
    Add-Result -Name 'Windows SDK' -Status 'PASS' -Detail (($goodSdk | ForEach-Object { $_.ToString() }) -join ', ')
} elseif ($sdkVersions.Count -gt 0) {
    Add-Result -Name 'Windows SDK' -Status 'FAIL' -Detail "Only older SDKs: $(($sdkVersions | ForEach-Object { $_.ToString() }) -join ', ')" -Fix "Install Windows SDK >= $MinWindowsSdkVersion (Visual Studio Installer or standalone)."
} else {
    Add-Result -Name 'Windows SDK' -Status 'FAIL' -Detail 'No Windows 10 SDK found' -Fix "Install Windows SDK >= $MinWindowsSdkVersion."
}

# --- 6. Packaging / signing tools ------------------------------------------
foreach ($tool in 'signtool.exe', 'makeappx.exe') {
    $found = $null
    if (Test-Path $kitsBin) {
        $found = Get-ChildItem $kitsBin -Recurse -Filter $tool -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Select-Object -First 1
    }
    if ($found) {
        Add-Result -Name $tool -Status 'PASS' -Detail $found.FullName
    } else {
        Add-Result -Name $tool -Status 'WARN' -Detail 'Not found (needed for MSIX packaging/signing).' -Fix 'Install the Windows SDK signing tools component.'
    }
}

# --- 7. Visual Studio / Build Tools (MSBuild) -------------------------------
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$winuiVsComponent = $null
if (Test-Path $vswhere) {
    $products = & $vswhere -products * -property displayName 2>$null
    if ($products) {
        Add-Result -Name 'Visual Studio / Build Tools' -Status 'PASS' -Detail (($products) -join '; ')
        $winuiVsComponent = & $vswhere -products * -requires Microsoft.VisualStudio.ComponentGroup.WindowsAppSDK.Cs -property displayName 2>$null
    } else {
        Add-Result -Name 'Visual Studio / Build Tools' -Status 'WARN' -Detail 'vswhere found but no products listed.' -Fix 'Install VS 2022 (or Build Tools) with .NET desktop + Windows App SDK.'
    }
} else {
    Add-Result -Name 'Visual Studio / Build Tools' -Status 'WARN' -Detail 'Not installed.' -Fix 'winget install Microsoft.VisualStudio.2022.BuildTools (add .NET desktop + Windows App SDK workloads).'
}

# --- 7b. Windows App SDK / WinUI 3 C# templates -----------------------------
# Accept either the Visual Studio component OR the dotnet CLI template pack
# (CLI pack works on Build Tools-only machines without the full VS IDE).
$dotnetWinuiPack = $null
try {
    $installedPacks = & dotnet new uninstall 2>$null
    if ($installedPacks -match 'WindowsAppSDK\.WinUI\.CSharp\.Templates') {
        $dotnetWinuiPack = 'Microsoft.WindowsAppSDK.WinUI.CSharp.Templates (dotnet CLI pack)'
    }
} catch {}
if ($winuiVsComponent) {
    Add-Result -Name 'Windows App SDK C# templates' -Status 'PASS' -Detail (($winuiVsComponent) -join '; ')
} elseif ($dotnetWinuiPack) {
    Add-Result -Name 'Windows App SDK C# templates' -Status 'PASS' -Detail $dotnetWinuiPack
} else {
    Add-Result -Name 'Windows App SDK C# templates' -Status 'WARN' -Detail 'Not detected (VS component or dotnet template pack).' -Fix 'Run: dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates  (or add the VS "Windows App SDK C# Templates" component).'
}

# --- 8. Editor (VS Code / Cursor) ------------------------------------------
$codePath = Get-CommandPath 'code'
if ($codePath) {
    Add-Result -Name 'Editor (code CLI)' -Status 'PASS' -Detail $codePath
} else {
    Add-Result -Name 'Editor (code CLI)' -Status 'WARN' -Detail "'code' not on PATH." -Fix 'winget install Microsoft.VisualStudio.Code (then enable shell command).'
}

# --- 9. Source control tooling ---------------------------------------------
$git = Get-CommandPath 'git'
if ($git) {
    Add-Result -Name 'git' -Status 'PASS' -Detail ((& git --version) 2>$null)
} else {
    Add-Result -Name 'git' -Status 'FAIL' -Detail 'Not found.' -Fix 'winget install Git.Git'
}

$gh = Get-CommandPath 'gh'
if ($gh) {
    Add-Result -Name 'GitHub CLI' -Status 'PASS' -Detail $gh
} else {
    Add-Result -Name 'GitHub CLI' -Status 'WARN' -Detail 'Not found (used for releases/PRs).' -Fix 'winget install GitHub.cli'
}

# --- 10. winget ------------------------------------------------------------
$winget = Get-CommandPath 'winget'
if ($winget) {
    Add-Result -Name 'winget' -Status 'PASS' -Detail $winget
} else {
    Add-Result -Name 'winget' -Status 'WARN' -Detail 'Not found (handy for installing the above).' -Fix 'Install "App Installer" from the Microsoft Store.'
}

# --- 11. Optional: NuGet connectivity --------------------------------------
if (-not $SkipNetworkCheck) {
    $ok = Test-NetConnection -ComputerName 'api.nuget.org' -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue
    if ($ok) {
        Add-Result -Name 'NuGet connectivity' -Status 'PASS' -Detail 'api.nuget.org:443 reachable'
    } else {
        Add-Result -Name 'NuGet connectivity' -Status 'WARN' -Detail 'Could not reach api.nuget.org:443.' -Fix 'Check network/proxy; restore may fail offline.'
    }
} else {
    Add-Result -Name 'NuGet connectivity' -Status 'INFO' -Detail 'Skipped (-SkipNetworkCheck).'
}

# --- Report ----------------------------------------------------------------
Write-Host ''
foreach ($r in $script:Results) {
    $color = switch ($r.Status) {
        'PASS' { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'Red' }
        default { 'Gray' }
    }
    $line = '[{0}] {1}' -f $r.Status.PadRight(4), $r.Name
    Write-Host $line -ForegroundColor $color
    if ($r.Detail) { Write-Host ("       {0}" -f $r.Detail) -ForegroundColor DarkGray }
    if ($r.Status -in @('WARN', 'FAIL') -and $r.Fix) {
        Write-Host ("       fix: {0}" -f $r.Fix) -ForegroundColor DarkCyan
    }
}

$fail = @($script:Results | Where-Object { $_.Status -eq 'FAIL' }).Count
$warn = @($script:Results | Where-Object { $_.Status -eq 'WARN' }).Count
$pass = @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count

Write-Host ''
Write-Host ("Summary: {0} pass, {1} warn, {2} fail" -f $pass, $warn, $fail) -ForegroundColor Cyan

if ($fail -gt 0) {
    Write-Host 'Environment is NOT ready: resolve FAIL items above.' -ForegroundColor Red
    exit 1
} elseif ($warn -gt 0) {
    Write-Host 'Environment is usable, but review WARN items for full functionality.' -ForegroundColor Yellow
    exit 0
} else {
    Write-Host 'Environment is ready.' -ForegroundColor Green
    exit 0
}
