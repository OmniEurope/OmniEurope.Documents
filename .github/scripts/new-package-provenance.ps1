# SPDX-License-Identifier: EUPL-1.2
# Writes provenance.json (repository, commit, CI run identity, SHA-256 of the package and the symbol package).
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][long]$RunId,
    [Parameter(Mandatory)][long]$RunAttempt,
    [string]$Workflow = 'CI'
)

$ErrorActionPreference = 'Stop'
$resolvedDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
if ($Commit -notmatch '^[0-9a-f]{40}$') { throw "Invalid commit SHA: $Commit" }
if ($RunId -le 0 -or $RunAttempt -le 0) { throw 'Run identity must be positive.' }
$packages = @(Get-ChildItem -LiteralPath $resolvedDirectory -File |
    Where-Object Name -Match '\.(nupkg|snupkg)$' |
    Sort-Object Name)
if ($packages.Count -ne 2) {
    throw "Expected one NuGet package and one symbol package, found $($packages.Count)."
}

$provenance = [ordered]@{
    schemaVersion = 1
    repository = $Repository
    commit = $Commit
    workflow = $Workflow
    runId = $RunId
    runAttempt = $RunAttempt
    packages = @($packages | ForEach-Object {
        [ordered]@{
            name = $_.Name
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
}
$provenance | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $resolvedDirectory 'provenance.json') -Encoding utf8
Write-Host "Package provenance written for commit $Commit and run $RunId, two files hashed."
