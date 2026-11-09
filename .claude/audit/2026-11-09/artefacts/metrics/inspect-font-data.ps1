$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../audit-state.ps1"
$core = [Reflection.Assembly]::LoadFrom((Resolve-Path 'src/OmniEurope.Documents/bin/Release/net10.0/OmniEurope.Documents.dll').Path)
$extra = [Reflection.Assembly]::LoadFrom((Resolve-Path 'src/OmniEurope.Documents.Fonts/bin/Release/net10.0/OmniEurope.Documents.Fonts.dll').Path)
$notice = Get-Content 'src/OmniEurope.Documents.Fonts/Fonts/NOTICE.txt' -Raw
$all = @(Get-Content $script:AuditInventory -Raw | ConvertFrom-Json | Where-Object { $_.module -in @('Codecs','FontAssembly') -and $_.path.EndsWith('.ttf') })
$results = [Collections.Generic.List[object]]::new()
foreach ($entry in $all) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $script:AuditRepository $entry.path))
    $font = [OmniEurope.Documents.Fonts.TrueTypeFont]::Load($bytes)
    $points = 0L; $contours = 0L; $nonEmpty = 0
    for ($glyph = 0; $glyph -lt $font.GlyphCount; $glyph++) {
        $outline = $font.Glyphs.GetOutline($glyph)
        $contours += $outline.Contours.Count
        if ($outline.Contours.Count -gt 0) { $nonEmpty++ }
        foreach ($contour in $outline.Contours) { $points += $contour.Count }
        if ($font.GetAdvanceWidth($glyph) -lt 0) { throw 'Negative advance' }
    }
    foreach ($mapping in $font.CharacterMap.GetEnumerator()) {
        if ($mapping.Value -ge $font.GlyphCount -or $mapping.Key -gt 0x10ffff) { throw "Invalid cmap $($entry.path)" }
    }
    $file = [IO.Path]::GetFileName($entry.path)
    $assembly = if ($entry.module -eq 'Codecs') { $core } else { $extra }
    $resource = $assembly.GetManifestResourceStream("OmniEurope.Documents.Fonts.$file")
    if (!$resource) { throw "Missing embedded $file" }
    $copy = [IO.MemoryStream]::new(); $resource.CopyTo($copy); $resource.Dispose()
    $embeddedEqual = [Convert]::ToBase64String($bytes) -ceq [Convert]::ToBase64String($copy.ToArray()); $copy.Dispose()
    if (!$embeddedEqual) { throw "Embedded mismatch $file" }
    $noticeMatch = $null
    if ($entry.module -eq 'FontAssembly') {
        $match = [regex]::Match($notice, '(?m)^\s+' + [regex]::Escape($file) + '\s+(?:\S+\.ttf\s+)?([0-9a-f]{64})\s*$')
        $noticeMatch = $match.Success -and $match.Groups[1].Value -ceq $entry.contentHash
        if (!$noticeMatch) { throw "Notice mismatch $file" }
    }
    if (!$font.EmbeddingAllowed -or $points -eq 0 -or !$font.Names.Family) { throw "Invalid licensed face $file" }
    $record = [ordered]@{ path=$entry.path; sha256=$entry.contentHash; family=$font.Names.Family; style=$font.Names.Subfamily; fullName=$font.Names.FullName; postScript=$font.Names.PostScriptName; glyphs=$font.GlyphCount; mapped=$font.CharacterMap.Count; outlines=$nonEmpty; contours=$contours; points=$points; unitsPerEm=$font.UnitsPerEm; weight=$font.WeightClass; bold=$font.IsBold; italic=$font.IsItalic; embeddingAllowed=$font.EmbeddingAllowed; embeddedEqual=$embeddedEqual; noticeHashMatch=$noticeMatch }
    $results.Add([pscustomobject]$record)
    $proof = "Inspection native intégrale de la fonte $($font.Names.FullName) : $($font.GlyphCount) glyphes parcourus, $($font.CharacterMap.Count) correspondances cmap, $contours contours/$points points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées."
    if ($noticeMatch) { $proof += ' Empreinte conforme au NOTICE épinglé.' }
    Complete-AuditFile -Path $entry.path -Proof $proof
    [IO.File]::WriteAllText("$PSScriptRoot/font-data-native-proof.json", (ConvertTo-Json -InputObject @($results) -Depth 6))
    "$file : glyphs=$($font.GlyphCount), points=$points, embedded=True"
}
