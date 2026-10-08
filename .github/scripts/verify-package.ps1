# SPDX-License-Identifier: EUPL-1.2
# Fails unless the package declares no dependency, embeds every bundled font in its assembly and ships the
# licence file of every font family next to it.
param(
    [Parameter(Mandatory)] [string]$Directory,
    [Parameter(Mandatory)] [string]$Fonts
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = Get-ChildItem -LiteralPath $Directory -Filter 'OmniEurope.Documents.*.nupkg' -File | Where-Object { $_.Name -notlike '*.symbols.nupkg' } | Select-Object -First 1
if (-not $package) { throw "No OmniEurope.Documents package in $Directory." }

$zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName })
    function Read-Entry([string]$name) {
        $entry = $zip.GetEntry($name)
        if (-not $entry) { throw "$($package.Name) has no $name." }
        $stream = $entry.Open()
        try { $memory = [IO.MemoryStream]::new(); $stream.CopyTo($memory); return $memory.ToArray() } finally { $stream.Dispose() }
    }

    $nuspec = [xml][Text.Encoding]::UTF8.GetString((Read-Entry ($entries | Where-Object { $_ -like '*.nuspec' } | Select-Object -First 1))).TrimStart([char]0xFEFF)
    $dependencies = @($nuspec.GetElementsByTagName('dependency'))
    if ($dependencies.Count -gt 0) {
        throw "The package declares $($dependencies.Count) dependency(ies): $(($dependencies | ForEach-Object { $_.id }) -join ', ')."
    }

    $assemblies = @($entries | Where-Object { $_ -like 'lib/*/OmniEurope.Documents.dll' })
    if ($assemblies.Count -eq 0) { throw "$($package.Name) holds no lib/*/OmniEurope.Documents.dll." }
    $bundled = @(Get-ChildItem -LiteralPath $Fonts -File)
    if (-not ($bundled | Where-Object Extension -eq '.ttf')) { throw "No bundled font in $Fonts." }
    foreach ($assembly in $assemblies) {
        # Manifest resource names are stored as UTF-8 in the metadata string heap.
        $metadata = [Text.Encoding]::Latin1.GetString((Read-Entry $assembly))
        foreach ($file in $bundled) {
            $resource = "OmniEurope.Documents.Fonts.$($file.Name)"
            if (-not $metadata.Contains($resource)) { throw "$assembly does not embed $resource." }
        }
    }

    foreach ($licence in $bundled | Where-Object Name -like 'LICENSE-*.txt') {
        if ("fonts/$($licence.Name)" -notin $entries) {
            $found = @($entries | Where-Object { $_ -like '*LICENSE*' }) -join ', '
            throw "$($package.Name) does not ship fonts/$($licence.Name) (licence entries: $(if ($found) { $found } else { 'none' }))."
        }
    }

    $families = @($bundled | Where-Object Extension -eq '.ttf' | ForEach-Object { ($_.BaseName -split '-')[0] -replace '(Sans|Serif|Mono)$', '' } | Sort-Object -Unique)
    foreach ($family in $families) {
        if ("fonts/LICENSE-$family.txt" -notin $entries) { throw "No licence shipped for the $family fonts." }
    }

    "Package $($package.Name): no dependency, $($bundled.Count) font files embedded in $($assemblies.Count) assembly(ies), licences of $($families -join ', ') shipped."
}
finally {
    $zip.Dispose()
}
