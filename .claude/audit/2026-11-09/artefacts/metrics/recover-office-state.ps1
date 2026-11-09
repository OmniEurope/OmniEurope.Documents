$ErrorActionPreference='Stop'
. "$PSScriptRoot/../audit-state.ps1"
$lf=[char]10
$text=Get-Content "$script:AuditDirectory/artefacts/AUDIT_FINDINGS_Office.md" -Raw
$matches=[regex]::Matches($text,'(?ms)^## (?<path>[^\r\n]+)\r?\n\r?\nPreuve : (?<proof>[^\r\n]+)\r?\n\r?\n(?<conclusion>.*?)(?=^## |\z)')
$inventory=@(Get-Content $script:AuditInventory -Raw|ConvertFrom-Json|Where-Object module -eq 'Office')
$findings=@(Get-Content "$script:AuditDirectory/artefacts/findings-Office.json" -Raw|ConvertFrom-Json)
$prior=@(Get-Content "$script:AuditDirectory/artefacts/state-Office.json" -Raw|ConvertFrom-Json)
$priorByPath=@{};foreach($record in $prior){$priorByPath[$record.path]=$record}
$contractInputs=@('AGENTS.md','CLAUDE.md','.editorconfig','.claude/decisions.md','.claude/code-rules.md')|Where-Object {Test-Path -LiteralPath (Join-Path $script:AuditRepository $_)}|ForEach-Object {Get-Content -LiteralPath (Join-Path $script:AuditRepository $_) -Raw}
$contractHash=Get-AuditHash ("audit-360-cache-v2"+$lf+($contractInputs -join $lf)+(Get-Content 'C:/Dev/_Generic/docs/code-rules.md' -Raw))
$contextPaths=@('OmniEurope.Documents.slnx','Directory.Build.props','Directory.Packages.props','global.json')+@(git -C $script:AuditRepository ls-files '*.csproj' 'docs/adr/*' '.config/*')
$contextInputs=$contextPaths|Sort-Object -Unique|ForEach-Object {Get-Content -LiteralPath (Join-Path $script:AuditRepository $_) -Raw}
$contextHash=Get-AuditHash ($contextInputs -join $lf)
$now=[DateTimeOffset]::UtcNow.ToString('o')
$restored=[Collections.Generic.List[object]]::new()
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($match in $matches){
  $path=$match.Groups['path'].Value.Trim()
  if(-not $seen.Add($path)){throw "Duplicate evidence: $path"}
  $entry=@($inventory|Where-Object path -ceq $path)
  if($entry.Count -ne 1){throw "Unknown evidence: $path"}
  $entry=$entry[0]
  $hash=(Get-FileHash -LiteralPath (Join-Path $script:AuditRepository $path) -Algorithm SHA256).Hash.ToLowerInvariant()
  if($hash -cne $entry.contentHash){throw "Source changed: $path"}
  if($priorByPath.ContainsKey($path)){$restored.Add($priorByPath[$path]);continue}
  $proof=$match.Groups['proof'].Value
  $conclusion=$match.Groups['conclusion'].Value.Trim()
  $proofText=$lf+'## '+$path+$lf+$lf+'Preuve : '+$proof+$lf+$lf+$conclusion+$lf
  $ids=@([regex]::Matches($conclusion,'OFF-\d{3}')|ForEach-Object Value|Select-Object -Unique)
  $severity='none'
  foreach($id in $ids){$finding=$findings|Where-Object id -eq $id;if($finding){$severity=$finding.severity}}
  $restored.Add([pscustomobject][ordered]@{schema='audit-360-cache-v2';path=$path;module='Office';contentHash=$hash;contractHash=$contractHash;contextHash=$contextHash;evidenceHash=(Get-AuditHash $proofText);sourceRevision='2594b1d7cd6edceedd330e2e72377fe3b7c989af';sourceAudit='.claude/audit/2026-11-09';sourceFindings='.claude/audit/2026-11-09/artefacts/AUDIT_FINDINGS_Office.md';auditedAt=$now;mode='Full';maxSeverity=$severity;reusable=($path -notlike '*/TextFunctions.cs');semanticEvidence='Roslyn current repository verified; Full baseline, no incremental reuse';proof=$proof;reconstructedFromEvidence=$true;reconstructedAt=$now;reconstructionNote='État reconstruit depuis la preuve individuelle de lecture déjà conservée dans cette passe ; lecture antérieure à cette reconstruction, aucune lecture nouvelle revendiquée.'})
}
if($restored.Count -ne $matches.Count -or $restored.Count -lt 99){throw 'Recovery count mismatch'}
Write-AuditAtomic "$script:AuditDirectory/artefacts/state-Office.json" (ConvertTo-Json -InputObject @($restored.ToArray()) -Depth 8)
$lines=@($restored|Sort-Object path|ForEach-Object {ConvertTo-Json -InputObject $_ -Depth 8 -Compress})
Write-AuditAtomic "$script:AuditRepository/.claude/audit/index/AUDIT_INDEX_Office.jsonl" ($lines -join $lf)
Write-AuditRegister 'Office'
[pscustomobject]@{evidenceSections=$matches.Count;stateCount=$restored.Count;preservedExisting=$prior.Count;reconstructed=$restored.Count-$prior.Count;sourceHashesVerified=$restored.Count;recoveryNote='Sérialisation fautive -Depth8, restauration depuis preuves Markdown actuelles, aucun fichier non lu ajouté.'}|ConvertTo-Json|Tee-Object -FilePath "$PSScriptRoot/office-state-recovery.json"
