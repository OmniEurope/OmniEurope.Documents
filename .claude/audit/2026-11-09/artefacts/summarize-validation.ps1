$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$artifacts = Join-Path $root '.claude/audit/2026-11-09/artefacts'
$suite = Join-Path $artifacts 'metrics/final-suite'
$coverageFile = @(Get-ChildItem (Join-Path $suite 'unit') -Recurse -File -Filter '*.cobertura*.xml')
if ($coverageFile.Count -ne 1) { throw 'Un rapport Cobertura unique est attendu.' }
[xml]$coverage = Get-Content -LiteralPath $coverageFile[0].FullName -Raw
$results = foreach ($entry in @(@{name='unit';file='unit/tests.trx'},@{name='stress';file='stress/integration.trx'})) {
    [xml]$trx = Get-Content -LiteralPath (Join-Path $suite $entry.file) -Raw
    $count = $trx.TestRun.ResultSummary.Counters
    [pscustomobject]@{suite=$entry.name;total=[int]$count.total;passed=[int]$count.passed;failed=[int]$count.failed;skipped=[int]$count.notExecuted;errors=[int]$count.error}
}
$coveredLines = [int]$coverage.coverage.'lines-covered'
$validLines = [int]$coverage.coverage.'lines-valid'
$coveredBranches = [int]$coverage.coverage.'branches-covered'
$validBranches = [int]$coverage.coverage.'branches-valid'
$linePercent = 100.0 * $coveredLines / $validLines
$branchPercent = 100.0 * $coveredBranches / $validBranches
$scores = @(Import-Csv -LiteralPath (Join-Path $suite 'crap-scores.csv'))
$hashes = @('src/OmniEurope.Documents/bin/Release/net10.0/OmniEurope.Documents.dll',
    (Join-Path $artifacts 'package-consumer/packages/omnieurope.documents/1.1.0/lib/net10.0/OmniEurope.Documents.dll'),
    (Join-Path $artifacts 'package-consumer/bin/Release/net10.0/OmniEurope.Documents.dll')) | ForEach-Object { Get-FileHash -LiteralPath $_ }
if (@($hashes.Hash | Select-Object -Unique).Count -ne 1) { throw 'DLL source Release et consommateur différentes.' }
$fontsHashes = @('src/OmniEurope.Documents.Fonts/bin/Release/net10.0/OmniEurope.Documents.Fonts.dll',
    (Join-Path $artifacts 'package-consumer/packages/omnieurope.documents/1.1.0/lib/net10.0/OmniEurope.Documents.Fonts.dll'),
    (Join-Path $artifacts 'package-consumer/bin/Release/net10.0/OmniEurope.Documents.Fonts.dll')) | ForEach-Object { Get-FileHash -LiteralPath $_ }
if (@($fontsHashes.Hash | Select-Object -Unique).Count -ne 1) { throw 'DLL fontes Release et consommateur différentes.' }
$data = [ordered]@{
    result='PASS';sourceRevision='2594b1d7cd6edceedd330e2e72377fe3b7c989af';configuration='Release';platform='Linux, conteneur SDK10.0.403';containerCpus=4;containerMemoryGiB=4
    suiteRuns=1;results=@($results);linePercent=$linePercent;branchPercent=$branchPercent;coveredLines=$coveredLines;validLines=$validLines;coveredBranches=$coveredBranches;validBranches=$validBranches
    packages=@($coverage.coverage.packages.package.name);crapMethods=$scores.Count;crapGateExit=0;exceptions=0;excludedFiles=0;packageVersion='1.1.0';consumerAssertions=26
    packageHash=(Get-FileHash -LiteralPath (Join-Path $artifacts 'package/OmniEurope.Documents.1.1.0.nupkg')).Hash
    coreAssemblyHash=$hashes[0].Hash;fontsAssemblyHash=$fontsHashes[0].Hash;visualVerified=$false
}
if (@($results | Where-Object { $_.failed -or $_.errors -or $_.skipped -or $_.passed -ne $_.total }).Count -or $linePercent -lt 95 -or $branchPercent -lt 85 -or (Get-Content (Join-Path $suite 'exit-code.txt')).Trim() -ne '0') { $data.result='FAIL' }
$data | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $artifacts 'metrics/VALIDATION.json')
$lines = $linePercent.ToString('F2',[Globalization.CultureInfo]::GetCultureInfo('fr-FR'))
$branches = $branchPercent.ToString('F2',[Globalization.CultureInfo]::GetCultureInfo('fr-FR'))
$markdown = @"
# Validation réelle du nouvel audit

Source develop 2594b1d, version conservée 1.1.0. Aucun fichier de production ou test modifié, aucun test désactivé.

| Contrôle | Résultat observé |
|---|---|
| Compilation Windows Release | Sortie0, zéro avertissement/erreur, build-release.log |
| Compilation Linux Release | Sortie0, zéro avertissement/erreur, final-suite/build.log |
| Suite rapide | 2809/2809 réussis, zéro échec/ignoré, final-suite/unit/tests.trx |
| Suite stress | 50/50 réussis, zéro échec/ignoré, final-suite/stress/integration.trx |
| Couverture des deux DLL | $lines % lignes ($coveredLines/$validLines), $branches % branches ($coveredBranches/$validBranches) |
| Garde CRAP | $($scores.Count) méthodes, sortie0, aucune exception ni exclusion, final-suite/crap-gate.log et crap-scores.csv |
| Paquet local | Deux DLL, zéro dépendance, fontes et licences vérifiées, pack.log et verify-package.log |
| Consommateur neuf | PackageReference1.1.0,14 assertions générales+12fontes, mêmes octets Release/cache/bin pour chaque DLL, package-consumer.log |
| Données et fixtures | 261 fixtures décodées/validées en conteneur ;50fontes et41XSD inspectés nativement, preuves individuelles séparées |
| Sécurité | NuGet sans vulnérabilité rapportée ;Gitleaks historique118commits+arbre suivi, aucun secret détecté |
| HTML | Structure et JavaScript contrôlables statiquement ;affichage non vérifié, protocole file: refusé |

Une seule exécution effective de chaque suite. Les deux tentatives préalables de transport/lanceur ont échoué avant tout test : script ignoré absent de l'export, puis Get-CimInstance Windows indisponible sur Linux. Traces attempt-1 et attempt-2. Le repli utilise les commandes CI existantes : restore, build Release, deux dotnet test en parallèle, verify-trx puis crap-gate ; aucun seuil, filtre, assertion ni exception assoupli. Dépôt monté readonly, copie isolée en conteneur4CPU/4GiB, délai global900s. La présence réelle des deux assemblies dans Cobertura est vérifiée.

## Commandes et preuves

- Prévol : dotnet restore OmniEurope.Documents.slnx puis dotnet build OmniEurope.Documents.slnx --configuration Release --no-restore -p:ContinuousIntegrationBuild=true.
- Validation finale : pwsh -NoProfile -File .claude/audit/2026-11-09/artefacts/validate-final-container.ps1 ; scripts exacts et journaux dans final-suite/.
- Paquet : dotnet pack --configuration Release --no-build, puis verify-package.ps1 avec Fonts et ExtraFonts.
- Consommateur : build Release avec NuGet.Config local et cache neuf, puis dotnet PackageConsumer.dll.
- Cohérence : sonde C# lue et rejouée par le coordinateur, coherence-independent.log ; les défauts constatés restent présents, aucune correction prétendue.

Le contrôle n'est pas une certification Office, PDF/A, cryptographique, Native AOT ou WebAssembly. Les parcours réels sont explicitement distingués des preuves statiques et de la seule compilation. Aucun SAST général revendiqué. Visuel HTML non vérifié.
"@
[IO.File]::WriteAllText((Join-Path $artifacts 'metrics/VALIDATION.md'),$markdown,[Text.UTF8Encoding]::new($false))
$data | ConvertTo-Json -Depth 6
if ($data.result -ne 'PASS') { exit 1 }
