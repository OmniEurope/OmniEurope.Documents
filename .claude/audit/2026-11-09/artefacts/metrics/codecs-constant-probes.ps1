$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Resolve-Path 'src/OmniEurope.Documents/bin/Release/net10.0/OmniEurope.Documents.dll').Path)
function Make-Tiff([ushort]$extraAlpha) {
    $stream=[IO.MemoryStream]::new();$writer=[IO.BinaryWriter]::new($stream)
    $writer.Write([byte[]]@(73,73,42,0,8,0,0,0));$writer.Write([ushort]10)
    $bitsOffset=8+2+10*12+4
    $pixelOffset=$bitsOffset+8
    $entries=@(@(256,4,1,1),@(257,4,1,1),@(258,3,4,$bitsOffset),@(259,3,1,1),@(262,3,1,2),@(273,4,1,$pixelOffset),@(277,3,1,4),@(278,4,1,1),@(279,4,1,4),@(338,3,1,$extraAlpha))
    foreach($entry in $entries){$writer.Write([ushort]$entry[0]);$writer.Write([ushort]$entry[1]);$writer.Write([uint]$entry[2]);$writer.Write([uint]$entry[3])}
    $writer.Write([uint]0);foreach($channel in 0..3){$writer.Write([ushort]8)};$writer.Write([byte[]]@(128,0,0,128));$writer.Flush();$bytes=$stream.ToArray();$writer.Dispose();return ,$bytes
}
$tiffs=@(foreach($kind in @(1,2)){$bytes=Make-Tiff $kind;$image=[OmniEurope.Documents.Imaging.TiffDecoder]::Decode($bytes);[pscustomobject]@{extraSamples=$kind;input=@(128,0,0,128);output=@($image.Pixels);colorType=$image.ColorType.ToString();bytes=$bytes.Length}})
$tiffs|ConvertTo-Json -Depth 6|Set-Content "$PSScriptRoot/tiff-alpha-proof.json"
function U16($data,$at){return ([int]$data[$at]*256)+$data[$at+1]}
function S16($data,$at){$v=U16 $data $at;return $(if($v-ge32768){$v-65536}else{$v})}
function U32($data,$at){return ([long]$data[$at]*16777216)+([long]$data[$at+1]*65536)+([long]$data[$at+2]*256)+$data[$at+3]}
function Tables($data){$t=@{};for($i=0;$i-lt(U16 $data 4);$i++){$at=12+$i*16;$tag=[Text.Encoding]::ASCII.GetString($data,$at,4);$t[$tag]=U32 $data ($at+8)};return $t}
$lsb=@(foreach($family in @('LiberationSans','Carlito','Caladea')){
    $font=[OmniEurope.Documents.Fonts.FontLibrary]::Bundled($family,$false,$false);$tables=Tables $font.Data;$metrics=U16 $font.Data ($tables.hhea+34);$locaLong=(U16 $font.Data ($tables.head+50))-eq1
    $mismatches=@(for($g=0;$g-lt$font.GlyphCount;$g++){$lsbAt=if($g-lt$metrics){$tables.hmtx+$g*4+2}else{$tables.hmtx+$metrics*4+($g-$metrics)*2};$lsbOriginal=S16 $font.Data $lsbAt;$go=if($locaLong){U32 $font.Data ($tables.loca+$g*4)}else{2*(U16 $font.Data ($tables.loca+$g*2))};$end=if($locaLong){U32 $font.Data ($tables.loca+($g+1)*4)}else{2*(U16 $font.Data ($tables.loca+($g+1)*2))};$bbox=if($end-gt$go){S16 $font.Data ($tables.glyf+$go+2)}else{0};if($lsbOriginal-ne$bbox){[pscustomobject]@{glyph=$g;originalLsb=$lsbOriginal;subsetLsb=$bbox}}})
    [pscustomobject]@{family=$family;glyphs=$font.GlyphCount;mismatches=$mismatches}
})
$lsb|ConvertTo-Json -Depth 6|Set-Content "$PSScriptRoot/font-subset-lsb-proof.json"
$tiffs;$lsb|Select-Object family,glyphs,@{n='mismatches';e={$_.mismatches.Count}}
