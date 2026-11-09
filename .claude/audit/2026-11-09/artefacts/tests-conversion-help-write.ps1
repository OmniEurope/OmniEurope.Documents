function Add-TestConversionEvidence([string]$Name, [string]$Proof, [string]$Conclusion = 'RAS, assertions utiles sur parcours réel ; lecture seule, test non exécuté par ce réviseur.') {
    $auditRoot = 'C:/Dev/OmniEurope/OmniEurope.Documents'
    $auditBase = "$auditRoot/.claude/audit/2026-11-09/artefacts"
    $auditPath = "tests/OmniEurope.Documents.Tests/Conversion/$Name.cs"
    $auditEntry = @(Get-Content "$auditBase/tests-conversion-help-paths.json" -Raw | ConvertFrom-Json | Where-Object path -CEQ $auditPath)
    if ($auditEntry.Count -ne 1) { throw "Not reserved: $auditPath" }
    $auditHash = (Get-FileHash -LiteralPath "$auditRoot/$auditPath" -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($auditHash -cne $auditEntry[0].contentHash) { throw "Changed: $auditPath" }
    $auditOutput = "$auditBase/tests-conversion-help-evidence.jsonl"
    if (Test-Path $auditOutput) {
        if (@(Get-Content $auditOutput | ForEach-Object { $_ | ConvertFrom-Json }).path -ccontains $auditPath) { throw "Already recorded: $auditPath" }
    }
    $auditRecord = [ordered]@{ path=$auditPath;contentHash=$auditHash;proof=$Proof;conclusion=$Conclusion;maxSeverity='none';findings=@() }
    Add-Content -LiteralPath $auditOutput -Value (ConvertTo-Json -InputObject $auditRecord -Depth 8 -Compress) -Encoding utf8
}
