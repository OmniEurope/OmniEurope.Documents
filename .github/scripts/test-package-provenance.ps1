# SPDX-License-Identifier: EUPL-1.2
# Fails unless provenance.json matches the expected repository, commit and CI run, and every hash matches its file.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [Parameter(Mandatory)][string]$ExpectedRepository,
    [Parameter(Mandatory)][long]$ExpectedRunId,
    [Parameter(Mandatory)][int]$ExpectedRunAttempt
)

$ErrorActionPreference = 'Stop'
$resolvedDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$provenancePath = Join-Path $resolvedDirectory 'provenance.json'
if (-not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
    throw "Package provenance is missing: $provenancePath"
}

$provenance = Get-Content -Raw -LiteralPath $provenancePath | ConvertFrom-Json
if ($provenance.schemaVersion -ne 1) { throw "Unsupported provenance schema: $($provenance.schemaVersion)" }
if ([string]$provenance.repository -cne $ExpectedRepository) { throw "Provenance repository mismatch: $($provenance.repository)" }
if ([string]$provenance.commit -cne $ExpectedCommit) { throw "Provenance commit mismatch: $($provenance.commit)" }
if ([string]$provenance.workflow -cne 'CI') { throw "Unexpected provenance workflow: $($provenance.workflow)" }
if ([long]$provenance.runId -ne $ExpectedRunId) {
    throw "Provenance run ID mismatch: expected $ExpectedRunId, found $($provenance.runId)."
}
if ([int]$provenance.runAttempt -ne $ExpectedRunAttempt) {
    throw "Provenance run attempt mismatch: expected $ExpectedRunAttempt, found $($provenance.runAttempt)."
}

$entries = @($provenance.packages)
if ($entries.Count -ne 2) { throw "Expected two package provenance entries, found $($entries.Count)." }
$names = @($entries.name | Sort-Object)
$actualNames = @(Get-ChildItem -LiteralPath $resolvedDirectory -File | Where-Object Name -Match '\.(nupkg|snupkg)$' | ForEach-Object Name | Sort-Object)
if (Compare-Object $names $actualNames) { throw 'Provenance package list does not match the downloaded artifact.' }

foreach ($entry in $entries) {
    $name = [string]$entry.name
    if ([IO.Path]::GetFileName($name) -cne $name) { throw "Unsafe provenance package name: $name" }
    if ([string]$entry.sha256 -notmatch '^[0-9a-f]{64}$') { throw "Invalid provenance hash for $name." }
    $path = Join-Path $resolvedDirectory $name
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne [string]$entry.sha256) { throw "Package hash mismatch for $name." }
}

Write-Host "Package provenance passed for commit $ExpectedCommit and run $($provenance.runId)."
