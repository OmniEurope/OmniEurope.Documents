$ErrorActionPreference='Stop'
. "$PSScriptRoot/../audit-state.ps1"
$root=$script:AuditRepository
$assembly=[Reflection.Assembly]::LoadFrom("$root/src/OmniEurope.Documents/bin/Release/net10.0/OmniEurope.Documents.dll")
$schemaType=$assembly.GetType('OmniEurope.Documents.Word.Validation.OfficeSchemas',$true)
$compiled=$schemaType.GetProperty('Set').GetValue($null)
if(-not $compiled.IsCompiled) {throw 'Embedded schema set is not compiled.'}
$corrected=$schemaType.GetProperty('StrictDefaultsCorrected',[Reflection.BindingFlags]'NonPublic,Static').GetValue($null)
if($corrected -ne 3) {throw "Unexpected strict defaults corrections: $corrected"}
$archives=@{
  Opc=@('part2.zip','OpenPackagingConventions-XMLSchema.zip','1d489dc491168ea1f9e9a59063acc8dd5f02b4ad1d21aa7ec19ba9a58d020c70')
  Strict=@('part1.zip','OfficeOpenXML-XMLSchema-Strict.zip','9d0bcad9cf06054785b03762fcfadbf6bab7e54a5f9d69434e34b7fd464d4129')
  Transitional=@('part4.zip','OfficeOpenXML-XMLSchema-Transitional.zip','bd25da1109f73762356596918bf5ff8b74a1331642dba5f1c1d1dfc6bed34ecd')
}
$nativeResults=[Collections.Generic.List[object]]::new()
foreach($group in $archives.Keys) {
  $description=$archives[$group]
  $archivePath="$PSScriptRoot/ecma-originals/$($description[0])"
  if((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $description[2]) {throw "Official archive hash mismatch: $group"}
  $outer=[IO.Compression.ZipFile]::OpenRead($archivePath)
  try {
    $nestedMemory=[IO.MemoryStream]::new()
    $source=$outer.GetEntry($description[1]).Open()
    try {$source.CopyTo($nestedMemory)} finally {$source.Dispose()}
    $nestedMemory.Position=0
    $nested=[IO.Compression.ZipArchive]::new($nestedMemory,[IO.Compression.ZipArchiveMode]::Read)
    try {
      $entries=Get-Content $script:AuditInventory -Raw | ConvertFrom-Json | Where-Object {$_.module -eq 'Office' -and $_.path -like "*/Schemas/$group/*.xsd"}
      foreach($entry in $entries) {
        $local=[IO.File]::ReadAllBytes("$root/$($entry.path)")
        $remoteEntry=@($nested.Entries | Where-Object Name -CEQ ([IO.Path]::GetFileName($entry.path)))
        if($remoteEntry.Count -ne 1) {throw "Official schema missing/ambiguous: $($entry.path)"}
        $remoteMemory=[IO.MemoryStream]::new()
        $remote=$remoteEntry[0].Open()
        try {$remote.CopyTo($remoteMemory)} finally {$remote.Dispose()}
        $sameOfficial=[Convert]::ToBase64String($local) -ceq [Convert]::ToBase64String($remoteMemory.ToArray())
        $remoteMemory.Dispose()
        if(-not $sameOfficial) {throw "Official schema differs byte-for-byte: $($entry.path)"}
        $name="OmniEurope.Documents.Word.Schemas.$group.$([IO.Path]::GetFileName($entry.path))"
        $embedded=$assembly.GetManifestResourceStream($name)
        if(-not $embedded) {throw "Embedded resource missing: $name"}
        $embeddedMemory=[IO.MemoryStream]::new()
        try {$embedded.CopyTo($embeddedMemory)} finally {$embedded.Dispose()}
        $sameEmbedded=[Convert]::ToBase64String($local) -ceq [Convert]::ToBase64String($embeddedMemory.ToArray())
        $embeddedMemory.Dispose()
        if(-not $sameEmbedded) {throw "Embedded schema differs: $name"}
        $settings=[Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver=$null
        $reader=[Xml.XmlReader]::Create([IO.MemoryStream]::new($local),$settings)
        try {$schema=[Xml.Schema.XmlSchema]::Read($reader,$null)} finally {$reader.Dispose()}
        $imports=@($schema.Includes | ForEach-Object {"$($_.Namespace)=$($_.SchemaLocation)"})
        $ns=$schema.TargetNamespace
        $row=[pscustomobject]@{path=$entry.path;bytes=$local.Length;targetNamespace=$ns;items=$schema.Items.Count;imports=$imports;officialBytesIdentical=$sameOfficial;embeddedBytesIdentical=$sameEmbedded;nativeRead='XmlSchema.Read full';compiledEmbeddedSet=$compiled.IsCompiled;strictDefaultsCorrected=$corrected}
        $nativeResults.Add($row)
        $proof="Parsing natif intégral XmlSchema.Read : $($schema.Items.Count) déclarations, espace $ns, $($imports.Count) imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire."
        Complete-AuditFile -Path $entry.path -Proof $proof -Conclusion 'RAS'
        $row | ConvertTo-Json -Compress -Depth 5
      }
    } finally {$nested.Dispose();$nestedMemory.Dispose()}
  } finally {$outer.Dispose()}
}
$nativeResults | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$PSScriptRoot/office-schema-native-proof.json" -Encoding utf8
