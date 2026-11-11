# SPDX-License-Identifier: EUPL-1.2
# Fails unless the package declares no dependency, holds both assemblies (OmniEurope.Documents.dll and the extra
# fonts assembly OmniEurope.Documents.Fonts.dll) for every target framework with their XML docs, embeds every font of each folder in its
# assembly and ships the licence file of every font family next to it (fonts/ and fonts/extra/), each one the SIL
# Open Font License 1.1. It also checks
# that the symbol package holds the portable PDB of each assembly, that the package version equals the release tag
# when one is given, and that neither an assembly nor a PDB leaks a build machine path (a deterministic build maps
# every path to /_/).
param(
    [string]$ExpectedVersion,
    [Parameter(Mandatory)] [string]$Directory,
    [Parameter(Mandatory)] [string]$Fonts,
    [Parameter(Mandatory)] [string]$ExtraFonts,
    # Every framework the package targets (Directory.Build.props): a pack that drops one must fail.
    [string[]]$Frameworks = @('net10.0', 'net11.0')
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

    foreach ($framework in $Frameworks) {
        foreach ($file in 'OmniEurope.Documents.dll', 'OmniEurope.Documents.Fonts.dll') {
            if ($entries -notcontains "lib/$framework/$file") { throw "$($package.Name) has no lib/$framework/$file." }
        }
    }

    $core = @($entries | Where-Object { $_ -like 'lib/*/OmniEurope.Documents.dll' })
    if ($core.Count -eq 0) { throw "$($package.Name) holds no lib/*/OmniEurope.Documents.dll." }
    $extra = @($entries | Where-Object { $_ -like 'lib/*/OmniEurope.Documents.Fonts.dll' })
    if ($extra.Count -ne $core.Count) { throw "$($package.Name) holds $($extra.Count) lib/*/OmniEurope.Documents.Fonts.dll for $($core.Count) core assembly(ies)." }
    $assemblies = @($core) + @($extra)
    foreach ($assembly in $assemblies) {
        $doc = [IO.Path]::ChangeExtension($assembly, '.xml')
        if ($doc -notin $entries) { throw "$($package.Name) has no XML documentation $doc." }
    }

    # Both assemblies carry the package version (X.Y.Z.0) as assembly and file version.
    $packageVersion = ([string]$nuspec.package.metadata.version -split '[-+]')[0]
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $scratch | Out-Null
    try {
        foreach ($assembly in $assemblies) {
            $file = Join-Path $scratch ([IO.Path]::GetFileName($assembly))
            [IO.File]::WriteAllBytes($file, (Read-Entry $assembly))
            $assemblyVersion = [string][Reflection.AssemblyName]::GetAssemblyName($file).Version
            $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($file).FileVersion
            if ($assemblyVersion -cne "$packageVersion.0" -or $fileVersion -cne "$packageVersion.0") {
                throw "$assembly has assembly version $assemblyVersion and file version $fileVersion, not $packageVersion.0."
            }
        }
    }
    finally { Remove-Item -LiteralPath $scratch -Recurse -Force }

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
        # Each assembly records its own PDB path under /_/ when the build is deterministic.
        foreach ($assembly in $assemblies) {
            $text = [Text.Encoding]::Latin1.GetString((Read-Entry $assembly))
            $pdb = [regex]::Escape([IO.Path]::GetFileNameWithoutExtension($assembly) + '.pdb')
            if (-not [regex]::IsMatch($text, "/_/[\x20-\x7e]*/$pdb")) { throw "$assembly does not record a deterministic /_/ PDB path." }
        }
    }
    finally { $symbolZip.Dispose() }

    # Every file of a font folder is embedded in its assembly and every family's licence ships under its package
    # folder. A family is the file name before '-'; its licence is LICENSE-<family>.txt, the core folder also
    # accepting the family without its Sans, Serif or Mono suffix (one Liberation licence for three families).
    function Assert-Fonts([string]$folder, [string[]]$targets, [string]$packagePath, [bool]$stripSuffix) {
        $files = @(Get-ChildItem -LiteralPath $folder -File)
        if (-not ($files | Where-Object Extension -eq '.ttf')) { throw "No font in $folder." }
        foreach ($assembly in $targets) {
            # Manifest resource names are stored as UTF-8 in the metadata string heap.
            $metadata = [Text.Encoding]::Latin1.GetString((Read-Entry $assembly))
            foreach ($file in $files) {
                $resource = "OmniEurope.Documents.Fonts.$($file.Name)"
                if (-not $metadata.Contains($resource)) { throw "$assembly does not embed $resource." }
            }
        }

        foreach ($licence in $files | Where-Object Name -like 'LICENSE-*.txt') {
            if ("$packagePath$($licence.Name)" -notin $entries) {
                $found = @($entries | Where-Object { $_ -like '*LICENSE*' }) -join ', '
                throw "$($package.Name) does not ship $packagePath$($licence.Name) (licence entries: $(if ($found) { $found } else { 'none' }))."
            }
            # Only SIL OFL 1.1 fonts ship with the package.
            $text = [Text.Encoding]::UTF8.GetString((Read-Entry "$packagePath$($licence.Name)"))
            if (-not $text.Contains('SIL OPEN FONT LICENSE Version 1.1') -or $text.Contains('Apache License')) { throw "$packagePath$($licence.Name) is not the SIL Open Font License 1.1." }
        }

        $families = @($files | Where-Object Extension -eq '.ttf' | ForEach-Object { ($_.BaseName -split '-')[0] } | Sort-Object -Unique)
        foreach ($family in $families) {
            $names = @("LICENSE-$family.txt")
            if ($stripSuffix) { $names = @("LICENSE-$($family -replace '(Sans|Serif|Mono)$', '').txt") }
            if (-not ($names | Where-Object { "$packagePath$_" -in $entries })) { throw "No licence shipped for the $family fonts under $packagePath." }
        }

        return "$($files.Count) files embedded in $($targets.Count) assembly(ies), licences of $($families -join ', ') shipped"
    }

    $coreReport = Assert-Fonts $Fonts $core 'fonts/' $true
    $extraReport = Assert-Fonts $ExtraFonts $extra 'fonts/extra/' $false
    if ('fonts/extra/NOTICE.txt' -notin $entries) { throw "$($package.Name) does not ship fonts/extra/NOTICE.txt." }

    "Package $($package.Name): no dependency; bundled fonts: $coreReport; extra fonts: $extraReport."
}
finally {
    $zip.Dispose()
}
