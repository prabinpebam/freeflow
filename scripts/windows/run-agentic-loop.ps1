#Requires -Version 7.0
<#
.SYNOPSIS
    Deterministic inner-loop driver: run the loop-tier tests, project them into
    verdict.json, print a human summary, and set an exit code a gate/agent can read.

.DESCRIPTION
    Implements step 1 of the agentic loop (docs/windows/testing-strategy.md, Section 9):
    run the deterministic tiers and emit the single machine-readable verdict.
    The actual fix/edit/re-run iteration is performed by the coding agent using
    verdict.json; this script is the run+evaluate half it calls each iteration.

    Exit codes:
      0 -> green (failed == 0 and determinism_ok == true)
      1 -> red   (one or more failures, or determinism guard tripped)
      2 -> harness error (build/test could not run)

.PARAMETER Solution
    Path to the solution to test.

.PARAMETER ResultsDir
    Directory for TRX/coverage output and verdict.json.

.PARAMETER Filter
    VSTest filter for the loop tiers. Defaults to the exclusive form so Reqnroll
    Category-tagged scenarios are included (see testing-strategy.md, Section 9.1).

.PARAMETER NoBuild
    Skip the implicit build (assume the solution is already built).

.PARAMETER Coverage
    Also collect XPlat code coverage.

.EXAMPLE
    pwsh -File scripts/windows/run-agentic-loop.ps1
#>
[CmdletBinding()]
param(
    [string] $Solution = 'windows/FreeFlow.Windows.sln',
    [string] $ResultsDir = 'artifacts/test',
    [string] $Filter = 'Tier!=L4&Tier!=L5',
    [switch] $NoBuild,
    [switch] $Coverage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath $ResultsDir) {
        Remove-Item -LiteralPath $ResultsDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $ResultsDir -Force | Out-Null

    $testArgs = @(
        'test', $Solution,
        '--nologo',
        '--filter', $Filter,
        '--logger', 'trx;LogFilePrefix=results',
        '--results-directory', $ResultsDir
    )
    if ($NoBuild) { $testArgs += '--no-build' }
    if ($Coverage) { $testArgs += @('--collect:XPlat Code Coverage') }

    Write-Host "loop> dotnet $($testArgs -join ' ')"
    & dotnet @testArgs
    $testExit = $LASTEXITCODE

    $verdictPath = Join-Path $ResultsDir 'verdict.json'
    & pwsh -File (Join-Path $PSScriptRoot 'build-verdict.ps1') -ResultsDir $ResultsDir -Out $verdictPath -SolutionPath $Solution
    if (-not (Test-Path -LiteralPath $verdictPath)) {
        Write-Error "verdict.json was not produced (dotnet test exit $testExit)."
        exit 2
    }

    $verdict = Get-Content -LiteralPath $verdictPath -Raw | ConvertFrom-Json
    $s = $verdict.summary
    Write-Host ''
    Write-Host "verdict: total=$($s.total) passed=$($s.passed) failed=$($s.failed) skipped=$($s.skipped) determinism_ok=$($verdict.determinism_ok)"

    if ($verdict.failures -and $verdict.failures.Count -gt 0) {
        Write-Host ''
        Write-Host 'failures:'
        foreach ($f in $verdict.failures) {
            Write-Host "  - [$($f.tier)] $($f.id)"
            if ($f.message) { Write-Host "      $($f.message)" }
            Write-Host "      repro: $($f.reproduce_cmd)"
        }
    }

    if ($s.failed -eq 0 -and $verdict.determinism_ok) {
        Write-Host ''
        Write-Host 'GREEN: inner loop is satisfied.'
        exit 0
    }

    Write-Host ''
    Write-Host 'RED: edit product code to satisfy the failing specs, then re-run.'
    exit 1
}
finally {
    Pop-Location
}
