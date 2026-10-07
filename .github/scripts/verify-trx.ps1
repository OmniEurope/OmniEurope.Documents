# SPDX-License-Identifier: EUPL-1.2
# Fails unless the test run left its TRX file with at least one executed test and no failure.
param(
    [Parameter(Mandatory)] [string]$Directory,
    [Parameter(Mandatory)] [string]$Name
)
$ErrorActionPreference = 'Stop'
$trx = Get-ChildItem -LiteralPath $Directory -Filter $Name -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $trx) { throw "The test run produced no $Name." }
$counters = ([xml](Get-Content -LiteralPath $trx.FullName -Raw)).TestRun.ResultSummary.Counters
if ([int]$counters.total -eq 0) { throw "$Name holds no test." }
if ([int]$counters.failed -gt 0) { throw "$Name reports $($counters.failed) failed test(s)." }
"Tests: $($counters.passed) passed of $($counters.total)."
