# Validation réelle du nouvel audit

Source develop 2594b1d, version conservée 1.1.0. Aucun fichier de production ou test modifié, aucun test désactivé.

| Contrôle | Résultat observé |
|---|---|
| Compilation Windows Release | Sortie0, zéro avertissement/erreur, build-release.log |
| Compilation Linux Release | Sortie0, zéro avertissement/erreur, final-suite/build.log |
| Suite rapide | 2809/2809 réussis, zéro échec/ignoré, final-suite/unit/tests.trx |
| Suite stress | 50/50 réussis, zéro échec/ignoré, final-suite/stress/integration.trx |
| Couverture des deux DLL | 97,44 % lignes (26746/27448), 87,55 % branches (19079/21791) |
| Garde CRAP | 4035 méthodes, sortie0, aucune exception ni exclusion, final-suite/crap-gate.log et crap-scores.csv |
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