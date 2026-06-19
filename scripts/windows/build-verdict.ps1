#Requires -Version 7.0
<#
.SYNOPSIS
    Project test results (TRX) into the single machine-readable verdict.json
    contract consumed by the agentic test loop (see docs/windows/testing-strategy.md, Section 9).

.DESCRIPTION
    Parses every *.trx under -ResultsDir, aggregates pass/fail/skip counts, and
    emits a structured verdict.json. Each failure is shaped per the documented
    contract (id, tier, oracle, spec_ref, expected/actual/diff refs, category,
    suggested_locus, reproduce_cmd) and additionally carries message/stack_trace
    so a coding agent can act without re-running the suite first.

    Optional inputs (produced by later tiers; ignored if absent):
      <ResultsDir>/eval/eval.json          -> { "score": 0.0-1.0 }  => summary.eval_score
      <ResultsDir>/determinism/result.json -> { "ok": true|false }  => determinism_ok

.PARAMETER ResultsDir
    Directory containing one or more *.trx files (searched recursively).

.PARAMETER Out
    Path to write verdict.json. Parent directory is created if needed.

.EXAMPLE
    pwsh -File scripts/windows/build-verdict.ps1 -ResultsDir artifacts/test -Out artifacts/test/verdict.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResultsDir,

    [Parameter(Mandatory = $true)]
    [string] $Out,

    [string] $SolutionPath = 'windows/FreeFlow.Windows.sln'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ResultsDir)) {
    throw "ResultsDir not found: $ResultsDir"
}

$trxFiles = @(Get-ChildItem -LiteralPath $ResultsDir -Recurse -Filter '*.trx' -File)
if ($trxFiles.Count -eq 0) {
    throw "No .trx files found under: $ResultsDir"
}

function Get-TierFromName {
    param([string] $Name)
    if ($Name -match '(?i)\bL([0-5])\b') { return "L$($Matches[1])" }
    return 'unknown'
}

function Get-ReproduceCommand {
    param([string] $Name, [string] $Solution)
    # Reqnroll scenario titles contain spaces; plain xUnit names are dotted FQNs.
    if ($Name -match '\s') {
        $escaped = $Name.Replace('"', '\"')
        return "dotnet test $Solution --filter `"DisplayName~$escaped`""
    }
    return "dotnet test $Solution --filter `"FullyQualifiedName~$Name`""
}

$passed = 0
$failed = 0
$skipped = 0
$failures = [System.Collections.Generic.List[object]]::new()

foreach ($trx in $trxFiles) {
    [xml] $doc = Get-Content -LiteralPath $trx.FullName -Raw

    $results = @($doc.SelectNodes("//*[local-name()='UnitTestResult']"))

    foreach ($r in $results) {
        $outcome = $r.GetAttribute('outcome')
        $name = $r.GetAttribute('testName')

        switch ($outcome) {
            'Passed' { $passed++ }
            'NotExecuted' { $skipped++ }
            'Failed' {
                $failed++

                $err = $r.SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']")
                $message = $null
                $stack = $null
                if ($null -ne $err) {
                    $msgNode = $err.SelectSingleNode("*[local-name()='Message']")
                    $stkNode = $err.SelectSingleNode("*[local-name()='StackTrace']")
                    if ($null -ne $msgNode) { $message = $msgNode.InnerText }
                    if ($null -ne $stkNode) { $stack = $stkNode.InnerText }
                }

                $failures.Add([ordered]@{
                    id              = $name
                    tier            = Get-TierFromName -Name $name
                    oracle          = 'invariant'
                    spec_ref        = $null
                    expected_ref    = $null
                    actual_ref      = $null
                    diff_path       = $null
                    category        = $null
                    suggested_locus = $null
                    reproduce_cmd   = Get-ReproduceCommand -Name $name -Solution $SolutionPath
                    message         = $message
                    stack_trace     = $stack
                }) | Out-Null
            }
            default {
                # Inconclusive / Timeout / Aborted / Error are treated as failures for gating.
                $failed++
                $failures.Add([ordered]@{
                    id              = $name
                    tier            = Get-TierFromName -Name $name
                    oracle          = 'invariant'
                    spec_ref        = $null
                    expected_ref    = $null
                    actual_ref      = $null
                    diff_path       = $null
                    category        = $outcome
                    suggested_locus = $null
                    reproduce_cmd   = Get-ReproduceCommand -Name $name -Solution $SolutionPath
                    message         = "Non-passing outcome: $outcome"
                    stack_trace     = $null
                }) | Out-Null
            }
        }
    }
}

$total = $passed + $failed + $skipped

# Optional eval score.
$evalScore = $null
$evalPath = Join-Path $ResultsDir 'eval/eval.json'
if (Test-Path -LiteralPath $evalPath) {
    try { $evalScore = (Get-Content -LiteralPath $evalPath -Raw | ConvertFrom-Json).score } catch { $evalScore = $null }
}

# determinism_ok: explicit marker file wins; otherwise infer from a determinism-guard test.
$determinismOk = $true
$detPath = Join-Path $ResultsDir 'determinism/result.json'
if (Test-Path -LiteralPath $detPath) {
    try { $determinismOk = [bool] (Get-Content -LiteralPath $detPath -Raw | ConvertFrom-Json).ok } catch { $determinismOk = $true }
}
elseif ($failures | Where-Object { $_.id -match '(?i)determinism' }) {
    $determinismOk = $false
}

$verdict = [ordered]@{
    run_id = (Get-Date).ToString('yyyy-MM-ddTHH-mm-ss')
    summary = [ordered]@{
        total      = $total
        passed     = $passed
        failed     = $failed
        skipped    = $skipped
        eval_score = $evalScore
    }
    determinism_ok = $determinismOk
    failures = $failures
}

$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$json = $verdict | ConvertTo-Json -Depth 8
Set-Content -LiteralPath $Out -Value $json -Encoding utf8

Write-Host "verdict: total=$total passed=$passed failed=$failed skipped=$skipped determinism_ok=$determinismOk -> $Out"

# Reporter exits 0 even on test failures; the loop driver decides pass/fail from verdict.json.
exit 0
