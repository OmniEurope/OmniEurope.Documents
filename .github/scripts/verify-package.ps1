# SPDX-License-Identifier: EUPL-1.2
# Fails unless the package declares no dependency, embeds every bundled font in its assembly and ships the
# licence file of every font family next to it. It also checks that the symbol package holds the portable PDB,
# that the package version equals the release tag when one is given, and that neither the assembly nor the
# PDB leaks a build machine path (a deterministic build maps every path to /_/).
param(
    [string]$ExpectedVersion,
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

    if ($ExpectedVersion) {
        # The release tag is vX.Y.Z or X.Y.Z; the package version must be exactly X.Y.Z.
        $expected = $ExpectedVersion -replace '^v', ''
        $actual = [string]$nuspec.package.metadata.version
        if ($actual -cne $expected) { throw "NuGet version mismatch: expected $expected, found $actual." }
    }
    if ($entries | Where-Object { $_ -match '(?i)\.pdb$' }) { throw "$($package.Name) contains an embedded PDB; symbols belong in the .snupkg." }

    $assemblies = @($entries | Where-Object { $_ -like 'lib/*/OmniEurope.Documents.dll' })
    if ($assemblies.Count -eq 0) { throw "$($package.Name) holds no lib/*/OmniEurope.Documents.dll." }

    # A build machine path (drive letter, /home/, /Users/) before .pdb or .cs means the build was not deterministic.
    $leak = '(?:[A-Za-z]:[\\/]|/home/|/Users/)[\x20-\x7e]*\.(?:pdb|cs)\b'
    function Assert-NoBuildPath([string]$name, [byte[]]$bytes) {
        $m = [regex]::Match([Text.Encoding]::Latin1.GetString($bytes), $leak)
        if ($m.Success) { throw "$name leaks a non-deterministic build path: $($m.Value)" }
    }
    foreach ($assembly in $assemblies) { Assert-NoBuildPath "$($package.Name)!$assembly" (Read-Entry $assembly) }

    # The symbol package carries the portable PDB of every assembly, with deterministic paths too.
    $symbolPackage = Get-ChildItem -LiteralPath $Directory -Filter "$($package.BaseName).snupkg" -File | Select-Object -First 1
    if (-not $symbolPackage) { throw "No symbol package $($package.BaseName).snupkg in $Directory." }
    $symbolZip = [IO.Compression.ZipFile]::OpenRead($symbolPackage.FullName)
    try {
        foreach ($assembly in $assemblies) {
            $pdbName = [IO.Path]::ChangeExtension($assembly, '.pdb')
            $pdbEntry = $symbolZip.GetEntry($pdbName)
            if (-not $pdbEntry) { throw "$($symbolPackage.Name) has no $pdbName." }
            $stream = $pdbEntry.Open()
            try { $memory = [IO.MemoryStream]::new(); $stream.CopyTo($memory); $pdbBytes = $memory.ToArray() } finally { $stream.Dispose() }
            Assert-NoBuildPath "$($symbolPackage.Name)!$pdbName" $pdbBytes
        }
        # The assembly records the PDB path under /_/ when the build is deterministic.
        foreach ($assembly in $assemblies) {
            $text = [Text.Encoding]::Latin1.GetString((Read-Entry $assembly))
            if (-not [regex]::IsMatch($text, '/_/[\x20-\x7e]*OmniEurope\.Documents\.pdb')) { throw "$assembly does not record a deterministic /_/ PDB path." }
        }
    }
    finally { $symbolZip.Dispose() }
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
