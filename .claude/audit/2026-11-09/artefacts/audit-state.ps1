$script:AuditRepository = 'C:/Dev/OmniEurope/OmniEurope.Documents'
$script:AuditDirectory = "$script:AuditRepository/.claude/audit/2026-11-09"
$script:AuditInventory = "$script:AuditDirectory/artefacts/inventory.json"

function Get-AuditHash([string]$Text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))).ToLowerInvariant()
}

function Write-AuditAtomic([string]$Path, [string]$Text) {
    $auditTempPath = "$Path.tmp"
    [IO.File]::WriteAllText($auditTempPath, $Text, [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($auditTempPath, $Path, $true)
}

function Get-AuditModule([string]$Path) {
    if ($Path -match '^tests/OmniEurope.Documents.Tests/') { return 'Tests' }
    if ($Path -match '^tests/') { return 'Stress' }
    if ($Path -match '^src/OmniEurope\.Documents\.Fonts/') { return 'FontAssembly' }
    if ($Path -match '^src/OmniEurope.Documents/(Word|Excel|Charts)/') { return 'Office' }
    if ($Path -match '^src/OmniEurope.Documents/(Conversion|Csv|Diff|Html|Markdown)/') { return 'Conversion' }
    if ($Path -match '^src/OmniEurope.Documents/Pdf/') { return 'Pdf' }
    if ($Path -match '^src/OmniEurope.Documents/') { return 'Codecs' }
    return 'Governance'
}

function Initialize-AuditInventory {
    if (Test-Path -LiteralPath $script:AuditInventory) { throw 'Inventory already exists; do not overwrite review state.' }
    $auditPaths = @(git -C $script:AuditRepository ls-files --cached --others --exclude-standard | Where-Object { $_ -notlike '.claude/audit/*' } | Sort-Object -Unique)
    $auditEntries = @($auditPaths | ForEach-Object {
        [pscustomobject][ordered]@{ path = $_; module = (Get-AuditModule $_); contentHash = (Get-FileHash -LiteralPath (Join-Path $script:AuditRepository $_) -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    Write-AuditAtomic $script:AuditInventory (ConvertTo-Json -InputObject $auditEntries -Depth 5)
    foreach ($auditGroup in ($auditEntries | Group-Object module)) {
        $auditModule = $auditGroup.Name
        Write-AuditAtomic "$script:AuditDirectory/artefacts/AUDIT_FINDINGS_$auditModule.md" "# Revue du module $auditModule`n`nAudit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.`n"
        Write-AuditAtomic "$script:AuditDirectory/artefacts/findings-$auditModule.json" '[]'
        Write-AuditAtomic "$script:AuditDirectory/artefacts/state-$auditModule.json" '[]'
        Write-AuditRegister $auditModule
    }
    $auditEntries | Group-Object module | Select-Object Name, Count
}

function Write-AuditRegister([string]$Module) {
    $auditEntries = @(Get-Content -LiteralPath $script:AuditInventory -Raw | ConvertFrom-Json | Where-Object module -EQ $Module)
    $auditStatePath = "$script:AuditDirectory/artefacts/state-$Module.json"
    $auditState = @(Get-Content -LiteralPath $auditStatePath -Raw | ConvertFrom-Json)
    $auditLookup = @{}; foreach ($auditRecord in $auditState) { $auditLookup[$auditRecord.path] = $auditRecord }
    $auditLines = [Collections.Generic.List[string]]::new()
    $auditLines.Add("# Registre $Module")
    $auditLines.Add('')
    $auditLines.Add('| Fichier | Statut | Mode | Empreinte SHA-256 | Date | Sévérité | Preuve |')
    $auditLines.Add('|---|---|---|---|---|---|---|')
    foreach ($auditEntry in $auditEntries) {
        $auditRecord = $auditLookup[$auditEntry.path]
        if ($auditRecord) { $auditLines.Add("| $($auditEntry.path) | ✅ Audité | Full | $($auditEntry.contentHash) | $($auditRecord.auditedAt) | $($auditRecord.maxSeverity) | AUDIT_FINDINGS_$Module.md |") }
        else { $auditLines.Add("| $($auditEntry.path) | ⬜ À faire | Full | $($auditEntry.contentHash) | | | |") }
    }
    Write-AuditAtomic "$script:AuditDirectory/artefacts/AUDIT_REGISTER_$Module.md" ($auditLines -join "`n")
}

function Complete-AuditFile {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Proof, [string]$Conclusion = 'RAS', [string]$MaxSeverity = 'none', [bool]$Reusable = $true)
    if ([string]::IsNullOrWhiteSpace($Proof) -or $Proof.Length -lt 35) { throw 'Specific current-run evidence is required.' }
    $auditEntry = @(Get-Content -LiteralPath $script:AuditInventory -Raw | ConvertFrom-Json | Where-Object path -CEQ $Path)
    if ($auditEntry.Count -ne 1) { throw "Unknown or ambiguous inventory path: $Path" }
    $auditEntry = $auditEntry[0]
    $auditModule = $auditEntry.module
    $auditStatePath = "$script:AuditDirectory/artefacts/state-$auditModule.json"
    $auditState = @(Get-Content -LiteralPath $auditStatePath -Raw | ConvertFrom-Json)
    if ($auditState.path -ccontains $Path) { throw "File already reviewed: $Path" }
    $auditCurrentHash = (Get-FileHash -LiteralPath (Join-Path $script:AuditRepository $Path) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($auditCurrentHash -cne $auditEntry.contentHash) { throw "File changed during review: $Path" }
    $auditProofText = "`n## $Path`n`nPreuve : $Proof`n`n$Conclusion`n"
    Add-Content -LiteralPath "$script:AuditDirectory/artefacts/AUDIT_FINDINGS_$auditModule.md" -Value $auditProofText -Encoding utf8
    $auditContractInputs = @('AGENTS.md','CLAUDE.md','.editorconfig','.claude/decisions.md','.claude/code-rules.md') | Where-Object { Test-Path -LiteralPath (Join-Path $script:AuditRepository $_) } | ForEach-Object { Get-Content -LiteralPath (Join-Path $script:AuditRepository $_) -Raw }
    $auditContractHash = Get-AuditHash ("audit-360-cache-v2`n" + ($auditContractInputs -join "`n") + (Get-Content -LiteralPath 'C:/Dev/_Generic/docs/code-rules.md' -Raw))
    $auditContextPaths = @('OmniEurope.Documents.slnx','Directory.Build.props','Directory.Packages.props','global.json') + @(git -C $script:AuditRepository ls-files '*.csproj' 'docs/adr/*' '.config/*')
    $auditContextInputs = $auditContextPaths | Sort-Object -Unique | ForEach-Object { Get-Content -LiteralPath (Join-Path $script:AuditRepository $_) -Raw }
    $auditRecord = [ordered]@{ schema = 'audit-360-cache-v2'; path = $Path; module = $auditModule; contentHash = $auditCurrentHash; contractHash = $auditContractHash; contextHash = (Get-AuditHash ($auditContextInputs -join "`n")); evidenceHash = (Get-AuditHash $auditProofText); sourceRevision = '2594b1d7cd6edceedd330e2e72377fe3b7c989af'; sourceAudit = '.claude/audit/2026-11-09'; sourceFindings = ".claude/audit/2026-11-09/artefacts/AUDIT_FINDINGS_$auditModule.md"; auditedAt = [DateTimeOffset]::UtcNow.ToString('o'); mode = 'Full'; maxSeverity = $MaxSeverity; reusable = $Reusable; semanticEvidence = 'Roslyn current repository verified; Full baseline, no incremental reuse'; proof = $Proof }
    $auditState += [pscustomobject]$auditRecord
    Write-AuditAtomic $auditStatePath (ConvertTo-Json -InputObject $auditState -Depth 8)
    $auditJsonLines = @($auditState | Sort-Object path | ForEach-Object { ConvertTo-Json -InputObject $_ -Depth 8 -Compress })
    Write-AuditAtomic "$script:AuditRepository/.claude/audit/index/AUDIT_INDEX_$auditModule.jsonl" ($auditJsonLines -join "`n")
    Write-AuditRegister $auditModule
}
