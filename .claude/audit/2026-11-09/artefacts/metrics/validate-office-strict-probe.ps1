$ErrorActionPreference='Stop'
$root='C:/Dev/OmniEurope/OmniEurope.Documents'
$assembly=[Reflection.Assembly]::LoadFrom("$root/src/OmniEurope.Documents/bin/Release/net10.0/OmniEurope.Documents.dll")
$office=$assembly.GetType('OmniEurope.Documents.Word.Validation.OfficeSchemas',$true)
$xmlSchema=$office.GetMethod('XmlNamespaceSchema',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@())
$set=[Xml.Schema.XmlSchemaSet]::new()
$set.XmlResolver=$null
[void]$set.Add($xmlSchema)
$outer=[IO.Compression.ZipFile]::OpenRead("$PSScriptRoot/ecma-originals/part1.zip")
$mem=[IO.MemoryStream]::new()
$stream=$outer.GetEntry('OfficeOpenXML-XMLSchema-Strict.zip').Open()
$stream.CopyTo($mem);$stream.Dispose();$outer.Dispose()
$mem.Position=0
$inner=[IO.Compression.ZipArchive]::new($mem,[IO.Compression.ZipArchiveMode]::Read)
$corrected=0
foreach($entry in $inner.Entries | Where-Object Name -Like '*.xsd'){
  $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
  $reader=[Xml.XmlReader]::Create($entry.Open(),$settings)
  try {$schema=[Xml.Schema.XmlSchema]::Read($reader,$null)} finally {$reader.Dispose()}
  foreach($type in $schema.Items | Where-Object {$_ -is [Xml.Schema.XmlSchemaComplexType]}){
    foreach($a in $type.Attributes | Where-Object {$_ -is [Xml.Schema.XmlSchemaAttribute] -and $_.SchemaTypeName.Name -eq 'ST_OnOff' -and $_.SchemaTypeName.Namespace -eq 'http://purl.oclc.org/ooxml/officeDocument/sharedTypes' -and $_.DefaultValue -in @('on','off')}){
      $a.DefaultValue=if($a.DefaultValue -eq 'on'){'true'}else{'false'};$corrected++
    }
  }
  [void]$set.Add($schema)
}
$inner.Dispose();$mem.Dispose()
$set.Compile()
$zip=[IO.Compression.ZipFile]::OpenRead("$PSScriptRoot/office-strict-proof.xlsx")
$results=@()
foreach($part in @('xl/workbook.xml','xl/worksheets/sheet1.xml','xl/styles.xml','xl/sharedStrings.xml')){
  $xml=[Xml.Linq.XDocument]::Load($zip.GetEntry($part).Open())
  $errors=[Collections.Generic.List[string]]::new()
  $handler=[Xml.Schema.ValidationEventHandler]{param($sender,$e) $errors.Add($e.Message)}
  [Xml.Schema.Extensions]::Validate($xml,$set,$handler)
  $results+=[pscustomobject]@{part=$part;errors=@($errors);schema='official strict archive';isValid=$errors.Count -eq 0}
}
$zip.Dispose()
[pscustomobject]@{setCompiled=$set.IsCompiled;strictDefaultsCorrected=$corrected;parts=$results} | ConvertTo-Json -Depth 7 | Tee-Object -FilePath "$PSScriptRoot/office-strict-schema-validation.json"
