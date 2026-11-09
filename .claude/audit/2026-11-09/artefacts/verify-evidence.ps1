$ErrorActionPreference = 'Stop'
$auditRepository = 'C:/Dev/OmniEurope/OmniEurope.Documents'
$auditRoot = "$auditRepository/.claude/audit/2026-11-09"
$auditArtifacts = "$auditRoot/artefacts"
$auditInventory = @(Get-Content -LiteralPath "$auditArtifacts/inventory.json" -Raw | ConvertFrom-Json)
$auditCurrent = @(git -C $auditRepository ls-files --cached --others --exclude-standard | Where-Object { $_ -notlike '.claude/audit/*' } | Sort-Object -Unique)
$auditCurrent = @($auditCurrent + @($auditInventory | Where-Object deliveryMetadata | Select-Object -ExpandProperty path) | Sort-Object -Unique)
$auditErrors = [Collections.Generic.List[string]]::new()
if (@(Compare-Object @($auditInventory.path | Sort-Object) $auditCurrent).Count) { $auditErrors.Add('Current Git inventory differs from the initial inventory.') }
$auditStates = @(Get-ChildItem -LiteralPath $auditArtifacts -Filter 'state-*.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
if (@($auditStates | Group-Object path | Where-Object Count -GT 1).Count) { $auditErrors.Add('Duplicate review state path.') }
$auditStateLookup = @{}; foreach ($auditState in $auditStates) { $auditStateLookup[$auditState.path] = $auditState }
$auditModuleResults = @()
foreach ($auditModule in ($auditInventory | Group-Object module)) {
    $auditName = $auditModule.Name
    $auditFindingsText = Get-Content -LiteralPath "$auditArtifacts/AUDIT_FINDINGS_$auditName.md" -Raw
    $auditRegister = Get-Content -LiteralPath "$auditArtifacts/AUDIT_REGISTER_$auditName.md" -Raw
    $auditIndex = @(Get-Content -LiteralPath "$auditRepository/.claude/audit/index/AUDIT_INDEX_$auditName.jsonl" | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
    if (@($auditIndex | Group-Object path | Where-Object Count -GT 1).Count) { $auditErrors.Add("Duplicate index path: $auditName") }
    if (@(Compare-Object @($auditIndex.path | Sort-Object) @($auditModule.Group.path | Sort-Object)).Count) { $auditErrors.Add("Index inventory mismatch: $auditName") }
    $auditReviewed = 0
    foreach ($auditEntry in $auditModule.Group) {
        $auditState = $auditStateLookup[$auditEntry.path]
        if (-not $auditState) { continue }
        $auditReviewed++
        $auditCurrentHash = (Get-FileHash -LiteralPath (Join-Path $auditRepository $auditEntry.path) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($auditCurrentHash -cne $auditEntry.contentHash -or $auditState.contentHash -cne $auditEntry.contentHash) { $auditErrors.Add("Content hash mismatch: $($auditEntry.path)") }
        if (-not $auditFindingsText.Contains("## $($auditEntry.path)")) { $auditErrors.Add("Missing per-file block: $($auditEntry.path)") }
        if (-not $auditFindingsText.Contains($auditState.proof)) { $auditErrors.Add("Proof absent from per-file block: $($auditEntry.path)") }
        if ($auditState.proof.Length -lt 35 -or -not $auditState.evidenceHash -or -not $auditState.contractHash -or -not $auditState.contextHash) { $auditErrors.Add("Incomplete evidence record: $($auditEntry.path)") }
        if ($auditState.mode -ne 'Full') { $auditErrors.Add("Fresh audit reused content: $($auditEntry.path)") }
        $auditIndexed = @($auditIndex | Where-Object path -CEQ $auditEntry.path)
        if ($auditIndexed.Count -ne 1 -or $auditIndexed[0].evidenceHash -cne $auditState.evidenceHash) { $auditErrors.Add("Index mismatch: $($auditEntry.path)") }
        $auditBlockPattern = '(?ms)^## ' + [regex]::Escape($auditEntry.path) + '\r?\n.*?(?=^## |\z)'
        $auditBlocks = [regex]::Matches($auditFindingsText, $auditBlockPattern)
        if ($auditBlocks.Count -ne 1) { $auditErrors.Add("Missing or duplicate evidence block: $($auditEntry.path)") }
        else {
            $canonical = "`n" + $auditBlocks[0].Value.Replace("`r`n","`n").TrimEnd("`r","`n") + "`n"
            $blockHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
            if ($blockHash -cne $auditState.evidenceHash) { $auditErrors.Add("Evidence hash mismatch: $($auditEntry.path)") }
        }
        if (-not $auditRegister.Contains("| $($auditEntry.path) | ✅ Audité | Full | $($auditEntry.contentHash) |")) { $auditErrors.Add("Register mismatch: $($auditEntry.path)") }
    }
    $auditModuleResults += [pscustomobject]@{ module=$auditName; inventory=$auditModule.Count; reviewed=$auditReviewed; remaining=($auditModule.Count-$auditReviewed); full=$auditReviewed; diff=0; cache=0 }
}
$auditFindings = @(Get-ChildItem -LiteralPath $auditArtifacts -Filter 'findings-*.json' | Where-Object BaseName -NE 'findings-consolidated' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
foreach ($auditFinding in $auditFindings) {
    foreach ($auditKey in @('id','severity','file','line','title','consequence','evidence','recommendation','acceptance','proofKind')) { if (-not $auditFinding.$auditKey) { $auditErrors.Add("Finding missing $auditKey : $($auditFinding.id)") } }
    $auditFile = Join-Path $auditRepository $auditFinding.file
    if (-not (Test-Path -LiteralPath $auditFile)) { $auditErrors.Add("Finding source absent: $($auditFinding.id)") }
    elseif ($auditFinding.line -gt [IO.File]::ReadAllLines($auditFile).Length) { $auditErrors.Add("Finding line outside source: $($auditFinding.id)") }
}
if (@($auditFindings | Group-Object id | Where-Object Count -GT 1).Count) { $auditErrors.Add('Duplicate finding identifier.') }
if (@($auditFindings | Group-Object module | Where-Object Count -GT 25).Count) { $auditErrors.Add('Module findings cap exceeded.') }
if (@($auditFindings | Group-Object file | Where-Object Count -GT 5).Count) { $auditErrors.Add('Per-file findings cap exceeded.') }
$auditHead = (git -C $auditRepository rev-parse HEAD).Trim()
$auditBranch = (git -C $auditRepository branch --show-current).Trim()
$auditGitStatus = @(git -C $auditRepository status --porcelain)
$auditChangedPaths = @(@(git -C $auditRepository diff --name-only HEAD) + @(git -C $auditRepository ls-files --others --exclude-standard) | Sort-Object -Unique)
$auditSourceChanges = @($auditChangedPaths | Where-Object { $_ -notlike '.claude/audit/*' -and $_ -ne '.claude/decisions.md' })
if ($auditHead -ne '2594b1d7cd6edceedd330e2e72377fe3b7c989af' -or $auditBranch -ne 'develop' -or $auditSourceChanges.Count) { $auditErrors.Add('Source Git state changed during audit.') }
$auditResult = [ordered]@{ result=$(if($auditErrors.Count){'FAIL'}else{'PASS'}); inventory=$auditInventory.Count; reviewed=$auditStates.Count; remaining=($auditInventory.Count-$auditStates.Count); sourceBranch=$auditBranch; sourceRevision=$auditHead; sourceTreeClean=($auditGitStatus.Count-eq 0); full=$auditStates.Count; diff=0; cache=0; contextInvalidated=0; selfStateExcluded=@(git -C $auditRepository ls-files --cached --others --exclude-standard | Where-Object { $_ -like '.claude/audit/*' }).Count; findings=$auditFindings.Count; modules=$auditModuleResults; errors=@($auditErrors); note='Contrôle structurel des preuves, empreintes et registres. Ne remplace ni la lecture sémantique ni les preuves runtime observées par le coordinateur.' }
$auditResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$auditArtifacts/metrics/EVIDENCE_CHECK.json" -Encoding utf8
$auditResult.sourceTreeClean = ($auditSourceChanges.Count -eq 0)
$auditResult.gitTreeClean = ($auditGitStatus.Count -eq 0)
$auditResult.auditMetadataChanges = $auditChangedPaths.Count - $auditSourceChanges.Count
$auditResult.sourceChanges = $auditSourceChanges
if (Test-Path -LiteralPath "$auditArtifacts/metrics/CACHE_CLASSIFICATION.json") {
    $classification = Get-Content -LiteralPath "$auditArtifacts/metrics/CACHE_CLASSIFICATION.json" -Raw | ConvertFrom-Json
    $auditResult.contextInvalidated = $classification.contextInvalidatedCandidates
    $auditResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$auditArtifacts/metrics/EVIDENCE_CHECK.json" -Encoding utf8
}
$auditResult | ConvertTo-Json -Depth 8
if ($auditErrors.Count) { exit 1 }
