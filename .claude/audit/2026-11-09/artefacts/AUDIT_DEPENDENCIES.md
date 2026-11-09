# Audit des dépendances

Référence : `develop`, `2594b1d`, 9 novembre 2026. Manifestes et graphes réellement interrogés, sans changement de version, installation ni publication.

## Inventaire et licences

Les quatre csproj, Directory.Packages.props, Directory.Build.props, global.json et NuGet.Config ont été lus. Gestionnaire NuGet uniquement, pas de manifeste npm, Go ou Cargo dans l’inventaire courant. Les deux productions n’ont aucun PackageReference explicite. `Microsoft.NET.ILLink.Tasks`10.0.14 apparaît comme auto-référence de construction du SDK pour IsTrimmable/IsAotCompatible ; il ne devient pas une dépendance runtime livrée.

| Dépendance directe des deux suites | Résolue | Dernière stable rapportée | Licence déclarée |
|---|---|---|---|
| coverlet.MTP |10.1.0| aucune mise à jour rapportée | MIT |
| Microsoft.Testing.Extensions.TrxReport |2.5.2| aucune mise à jour rapportée | MIT |
| xunit.v3 |4.0.2|4.0.3| Apache-2.0 |

Les deux suites présentent le même graphe : trois références directes, 26 transitives. Les 30 identités/version distinctes, outil ILLink inclus, portent une expression de licence MIT ou Apache-2.0 dans leurs nuspec réellement restaurés. Sources et chemins conservés dans metrics/dependencies-licenses.json. Il s’agit d’une lecture technique des métadonnées déclarées. Aucun de ces paquets de tests ne fait partie du NuGet de production.

Versions centrales exactes, aucun intervalle explicite ou flottant dans le manifeste central. La plage ILLink est une référence automatique du SDK. Aucun conflit de version distinct entre les deux graphes de tests. La requête obsolescence démontre une publication récente xunit ; aucune conclusion générale de maintenance de tous les auteurs n’est déduite du simple numéro de version.

Le résultat réel de verify-package.ps1, relu dans metrics/verify-package.log, confirme pour OmniEurope.Documents.1.1.0.nupkg : aucune dépendance et licences des six familles cœur et des sept familles supplémentaires. Les polices OFL, métriques Adobe et schémas ECMA sont les données tierces explicitement autorisées ; leur présence n’est pas assimilée à une bibliothèque copiée.

## Commandes et résultats réels

| Commande | Résultat |
|---|---|
| `dotnet list OmniEurope.Documents.slnx package --vulnerable --include-transitive --format json` | Sortie1 dans le bac : restauration signalée impossible, trace initiale conservée. |
| Même requête avec `--no-restore` dans le bac | Sortie1 : DNS api.nuget.org indisponible, trace conservée. |
| Même requête `--no-restore` avec accès réseau autorisé | Sortie0, source NuGet.org v3, aucun paquet vulnérable rapporté dans les quatre projets. Trace metrics/dependencies-vulnerable-network.json. |
| `dotnet list OmniEurope.Documents.slnx package --outdated --format json --no-restore` | Bac sortie 1 DNS ; accès réseau autorisé sortie 0. xunit.v3 4.0.2 vers 4.0.3 dans les deux suites, trace metrics/dependencies-outdated-network.json. |
| `dotnet list OmniEurope.Documents.slnx package --include-transitive --format json --no-restore` | Sortie0, graphes actuels et net10.0 réels dans metrics/dependencies-inventory.json. |

Aucune restauration concurrente lancée : les requêtes ont commencé après la fin restore/build signalée par l’orchestrateur ; après la première erreur native, toutes utilisent les assets restaurés. Les premières erreurs sont des limitations de bac levées par le nouvel essai autorisé, pas des défauts produit. Le résultat CVE est celui du registre interrogé à cet instant, pas une preuve d’absence de faille dans le code ou de vulnérabilité inconnue.

## Secret et sécurité outillée

La commande gitleaks du poste est absente. L’image officielle déjà locale `ghcr.io/gitleaks/gitleaks@sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f` a permis le scan réel, sans pull ni installation : version 8.30.1, aide CLI consultée, Docker `--network none --cpus 1 --memory 512m`, dépôt monté en lecture seule, sortie limitée aux artefacts du présent audit et `--redact=100`.

- Historique : `gitleaks git /repo --log-opts=--all --timeout 120`, sortie 0, 118 commits et 5,64MB inspectés en 49,6 s, aucun secret détecté. JSON et log dans metrics/gitleaks-history.*.
- État courant suivi : `git archive HEAD` extrait dans /tmp du conteneur, puis `gitleaks dir /tmp/current --timeout 120`, sortie 0, 12,36MB inspectés en 3,01 s, aucun secret détecté. `git status --short` et liste des non-suivis non ignorés vides après ces scans, l’archive correspond donc aux sources courantes. JSON et log dans metrics/gitleaks-current.*.

Les deux JSON sont des tableaux vides. Les fichiers locaux ignorés, sorties de build et archives internes non décompressées ne sont pas couverts par le scan de l’état suivi. Aucun SAST général n’a été exécuté ; la revue manuelle des sources conserve ses limites propres.

## Constat

DEP-001, faible : xunit.v34.0.2 est en retard sur 4.0.3 stable, contraire à la règle de mise à jour explicite. Correction et revalidation à autoriser après cet audit ; aucune modification appliquée ici. Aucun autre écart de dépendances démontré par ces commandes.

PROPORTIONALITY: NONE. Les dépendances déclarées sont réservées aux tests et à leur instrumentation ; l’alternative concrète d’un harnais maison remplacerait découverte, assertions et TRX sans réduire le paquet livré, qui est déjà sans dépendance.
