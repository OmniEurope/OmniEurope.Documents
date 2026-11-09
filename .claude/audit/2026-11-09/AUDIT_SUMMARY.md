# Audit 360 : OmniEurope.Documents

9 novembre 2026, source develop 2594b1d7cd6edceedd330e2e72377fe3b7c989af. Nouvelle lecture complète, aucune ancienne conclusion portée comme preuve actuelle. Aucun code de production ni test corrigé, aucune version modifiée.

**27 constats à résoudre : 0 critique, 0 élevé, 23 moyens, 4 faibles.** Les constatations fonctionnelles reposent sur des parcours réels ; les écarts de garde et de documentation sont explicitement statiques. La réussite des suites existantes n'efface pas ces défauts reproduits hors des scénarios qu'elles couvrent.

## Priorités

1. Fidélité des données : l'éditeur Excel altère les textes qui ressemblent à des escapes OOXML ; un remplacement Word linguistique supprime la mauvaise longueur ; la coupure d'une paire UTF-16 remplace le caractère ; la fusion Word et certains calculs Excel peuvent interrompre le traitement.
2. Cohérence des contrats publics : Excel vers HTML perd les diagnostics du classeur, le rendu PDF/PNG peut annoncer une résolution différente de celle appliquée, et les formulaires PDF ignorent une contrainte MaxLen héritée. Les variations de noms sont traitées séparément dans la matrice pour préserver les distinctions de sémantique et la compatibilité.
3. Fidélité des conversions : sélecteurs descendants, styles de cellules, texte préformaté, références d'images Markdown avec espace, graphiques à valeurs extrêmes, débuts de section et première entête de tableau produisent des pertes ou positions erronées. Les dates PDF et l'alpha TIFF associé sont aussi altérés.
4. Gardes et documentation : les couches automatiques STD-PARTIAL et STD-FILESIZE attendues sont absentes, le README annonce encore 1.0.0, et xunit 4.0.3 est disponible tandis que le dépôt référence 4.0.2. Aucun de ces points n'a été modifié pendant l'audit.

## Noms, options et homogénéité des API

La [matrice dédiée](artefacts/AUDIT_COHERENCE.md) couvre 22 concepts et s'appuie sur les membres/paramètres de 304 types publics des deux DLL. Elle confronte noms, surcharges, options, valeurs par défaut, flux, annulation, unités, exceptions et diagnostics.

- Variation de vocabulaire certaine pour le même MIME : ContentType dans WordImage/ImageInfo/WordMarkdownImage, MediaType dans WordHtmlImage. La sonde produit image/png des deux côtés et un adaptateur emploie deux accesseurs ; aucun contenu incorrect n'est attribué à cette seule différence de nom.
- Accès aux feuilles : Worksheet/Worksheets dans le modèle, Sheet/SheetNames dans l'éditeur ; les exceptions de nom absent diffèrent aussi. Les types retournés et leur rôle diffèrent, le coût d'adaptation et les contrats actuels sont consignés.
- Load/Open/Parse/Read, Save/ToArray, suffixes Options et façades origine-vers-destination suivent leurs usages. Les budgets HTML/PDF différents ont une raison matérielle documentée. Name et Path ne désignent pas la même donnée ; les noms ne sont pas harmonisés arbitrairement.

Tout futur rapprochement de noms publics doit privilégier des alias additifs et conserver paramètres nommés, constructeurs et déconstruction. La matrice ne prétend pas que chaque membre a été exercé à l'exécution.

## Constats priorisés

Tous les constats actifs tiennent sous le plafond de 30 de la synthèse. Chaque JSON et rapport de module conserve conséquence, preuve, intervention minimale et critère d'acceptation. Aucun RAS, positif, doublon ou piste non prouvée n'est compté.

| ID | Sévérité | Problème observé | Source |
|---|---|---|---|
| CDC001 | Moyen | L’alpha associé TIFF est exposé comme un alpha droit | [TiffDecoder.cs:146](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Imaging/TiffDecoder.cs:146) |
| COH-001 | Moyen | Le résultat Excel vers HTML perd les diagnostics de fidélité du classeur | [ExcelToHtml.cs:45](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/ExcelToHtml.cs:45) |
| COH-002 | Moyen | Le PNG annonce la résolution demandée après un rendu à une résolution pincée | [PdfRenderer.cs:66](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Pdf/Rendering/PdfRenderer.cs:66) |
| CVT-001 | Moyen | Les sélecteurs descendants échouent après un attribut ou une pseudo-classe | [HtmlSelector.cs:122](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Html/HtmlSelector.cs:122) |
| CVT-002 | Moyen | Des valeurs de graphique finies donnent une échelle NaN par dépassement intermédiaire | [ValueScale.cs:53](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/Charts/ValueScale.cs:53) |
| CVT-004 | Moyen | Les styles de caractères des cellules HTML ne sont pas appliqués à leur contenu Word | [HtmlWordWriter.cs:64](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/HtmlLayout/HtmlWordWriter.cs:64) |
| CVT-005 | Moyen | Les retours à la ligne préformatés disparaissent avant un élément inline | [HtmlWordWriter.cs:380](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/HtmlLayout/HtmlWordWriter.cs:380) |
| CVT-006 | Moyen | Un dossier image contenant un espace produit une référence Markdown cassée | [MarkdownDocumentWriter.cs:247](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/WordMarkdown/MarkdownDocumentWriter.cs:247) |
| CVT-007 | Moyen | La coupure des mots peut scinder une paire UTF-16 et remplacer le caractère | [LineBreaker.cs:280](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/WordLayout/LineBreaker.cs:280) |
| CVT-008 | Moyen | Une section NextColumn commence sous la première colonne au lieu de la suivante | [Paginator.cs:71](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/WordLayout/Paginator.cs:71) |
| KIT-001 | Moyen | STD-PARTIAL : la couche GEN008 requise ne protège pas la compilation | [OmniEurope.Documents.csproj:1](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/OmniEurope.Documents.csproj:1) |
| KIT-002 | Moyen | STD-FILESIZE : aucun garde de la suite ne contrôle les 600 lignes effectives | [OmniEurope.Documents.Tests.csproj:1](C:/Dev/OmniEurope/OmniEurope.Documents/tests/OmniEurope.Documents.Tests/OmniEurope.Documents.Tests.csproj:1) |
| OFF-001 | Moyen | Le validateur refuse les listes MC séparées par tabulation ou saut de ligne | [MarkupCompatibility.cs:214](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Word/Validation/MarkupCompatibility.cs:214) |
| OFF-002 | Moyen | L’éditeur Excel modifie les textes qui ressemblent aux escapes OOXML | [XlsxEditableSheet.cs:143](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Excel/Editing/XlsxEditableSheet.cs:143) |
| OFF-004 | Moyen | IFERROR et IFNA calculent deux fois leur opérande sans erreur | [LogicalFunctions.cs:15](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Excel/Formulas/LogicalFunctions.cs:15) |
| OFF-005 | Moyen | CEILING et FLOOR interrompent tout le recalcul sur des nombres finis élevés | [MathFunctions.cs:289](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Excel/Formulas/MathFunctions.cs:289) |
| OFF-006 | Moyen | La fusion échoue si la cible omet la section finale facultative | [WordMerger.cs:352](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Word/Editing/WordMerger.cs:352) |
| OFF-007 | Moyen | Une liste déroulante vide accepte une valeur hors liste sans gap | [WordContentControlFiller.cs:83](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Word/Editing/WordContentControlFiller.cs:83) |
| OFF-008 | Moyen | Le remplacement linguistique supprime une longueur UTF-16 incorrecte | [WordTextReplacer.cs:82](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Word/Editing/WordTextReplacer.cs:82) |
| PDF-001 | Moyen | Le rendu peut devenir entièrement blanc sans gap quand un flux de contenu est illisible | [PdfPage.cs:84](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Pdf/PdfPage.cs:84) |
| PDF-002 | Moyen | Les opérations PDF perdent les dates des métadonnées pourtant annoncées conservées | [PdfEditor.cs:121](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Pdf/Editing/PdfEditor.cs:121) |
| PDF-003 | Moyen | MaxLen hérité est perdu entre lecture du formulaire et validation du remplissage | [FormTree.cs:30](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Pdf/Forms/FormTree.cs:30) |
| PDF-005 | Moyen | La première entête du tableau est dessinée hors marge avant le saut de page | [PdfTable.cs:97](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Pdf/Writing/PdfTable.cs:97) |
| CVT-003 | Faible | Les mots-clés CSS de graisse et italique sont traités avec une casse significative | [CssStyle.cs:40](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Conversion/HtmlLayout/CssStyle.cs:40) |
| DEP-001 | Faible | xunit.v3 reste en 4.0.2 alors que 4.0.3 stable est disponible | [Directory.Packages.props:9](C:/Dev/OmniEurope/OmniEurope.Documents/Directory.Packages.props:9) |
| GOV-001 | Faible | Le statut du README annonce encore 1.0.0 | [README.md:7](C:/Dev/OmniEurope/OmniEurope.Documents/README.md:7) |
| OFF-003 | Faible | Un classeur Strict valide est signalé à tort comme dépourvu de feuille | [XlsxPackageReader.cs:99](C:/Dev/OmniEurope/OmniEurope.Documents/src/OmniEurope.Documents/Excel/XlsxPackageReader.cs:99) |

## Couverture de revue

949 fichiers initiaux suivis ou non ignorés, plus le registre de décisions destiné à la livraison, soit **950/950 preuves actuelles**, zéro entrée non auditée. Full 950, Diff 0, Cache 0. Les 740 candidats de contenu inchangé de l'audit précédent sont invalidés par le contexte modifié ; aucun résultat ancien n'a été réutilisé. Les sorties propres de l'audit sont exclues de cet inventaire pour éviter la récursion.

| Registre | Couverture | Mode |
|---|---|---|
| Codecs | 105/105 | Full |
| Conversion | 105/105 | Full |
| FontAssembly | 39/39 | Full |
| Governance | 30/30 | Full |
| Office | 142/142 | Full |
| Pdf | 96/96 | Full |
| Stress | 10/10 | Full |
| Tests | 423/423 | Full |

Le [contrôle structurel indépendant](artefacts/metrics/EVIDENCE_CHECK.json) vérifie chemins uniques, signatures des fichiers actuels, blocs par fichier, empreintes des preuves, registres et index. Il ne remplace ni le jugement sémantique ni les parcours réels. Les ressources binaires sont inspectées nativement, pas déclarées revues sur leur seule signature :261 fixtures, 50 fontes et 41 XSD, avec comparaisons de références et provenance quand disponibles.

## Validation réelle

- Compilation Release Windows et Linux : zéro avertissement, zéro erreur.
- Une seule exécution effective par suite, en parallèle dans un conteneur de 4 CPU et 4 GiB : **2 809 tests rapides et 50 tests de stress réussis**, zéro échec et zéro ignoré. Aucun test désactivé.
- Les deux DLL sont présentes dans Cobertura : **97,44 % des lignes** (26746/27448), **87,55 % des branches** (19079/21791). Seuils 95/85 respectés.
- Garde CRAP : **4 035 méthodes**, code 0, aucune exception ni exclusion. Complexité et couverture proviennent du même rapport et des mêmes identités de méthodes.
- Paquet local 1.1.0 : deux DLL, zéro dépendance, fontes et licences contrôlées. Consommateur PackageReference et cache neuf : 14 assertions générales et 12 liées aux fontes, octets des deux DLL identiques au Release vérifié.
- Scan NuGet réel : aucun paquet vulnérable rapporté. Gitleaks 8.30.1 en conteneur sans réseau : historique de 118 commits et arbre suivi, aucun secret détecté. Fichiers ignorés hors de ce scan.

Les [commandes, résultats et limites](artefacts/metrics/VALIDATION.md) et les TRX/Cobertura sont conservés localement. Deux échecs préalables du transport/lanceur n'ont lancé aucun test : lanceur ignoré absent de l'export, puis cmdlet Windows Get-CimInstance indisponible sur Linux. Les commandes CI existantes ont ensuite été exécutées dans le conteneur, sans modifier lanceur, seuils, assertions ou exceptions.

## Limites de l'audit

- **Affichage HTML non vérifié.** La politique du navigateur autonome refuse le protocole file: et interdit tout contournement. Structure et syntaxe JavaScript contrôlées statiquement seulement ; le propriétaire a expressément accepté la livraison avec cette limite. Voir [contrôle visuel](artefacts/metrics/VISUAL_CHECK.md).
- Aucun run CI hébergé, moteur Office externe, certification PDF/A/cryptographique, Native AOT ou application WebAssembly exécuté. Les signatures PAdES sont revues sans prétendre une certification externe.
- Pas de SAST général. Les résultats Gitleaks/NuGet et la revue manuelle ciblent des surfaces différentes ; aucune absence universelle de vulnérabilités n'est affirmée.
- Roslyn est réellement callable, mais sa métadonnée TFM netcoreapp1.0 est erronée ; net10.0 et SDK10.0.403 sont établis par manifests et compilation. Le MCP omnieurope est absent, les règles du kit ont été lues sur disque.
- Une sonde REPT à grand compteur a déclenché une itération et a été interrompue après 10 secondes sur l'hôte ; aucun résultat terminé ni défaut de coût n'en est déduit. Elle n'a pas été répétée sur le PC. Une piste jp2h a été testée dans un conteneur strict et a rendu une exception contrôlée, sans panne ; elle n'est pas un constat. Ces deux fichiers restent exclus de réutilisation en cache par prudence.
- Un index intermédiaire Office a été régénéré après erreur de paramètre, sans perte des 103 preuves alors présentes ni reconstruction de dates. Les vérifications finales de tous les ensembles et empreintes passent.
- audit.md (81 Ko) et registre de règles du kit (86 Ko) dépassent 20 Ko ; anomalies d'instructions séparées du produit. Les caractères Unicode utiles aux tables normatives et aux tests ne sont pas comptés comme défauts de style.

## Historique et livraison

Au démarrage, main et develop locaux/distants pointaient sur 2594b1d, arbre propre : aucun commit préalable nécessaire. Le commit des treize corrections antérieures, 3ed1101, appartient aux deux branches ; l'ancien identifiant 5fed746 de la conversation est différent, aucune perte n'en a été présumée. L'audit juge le code actuel.

La décision reçue pendant ce travail impose un commit local de livraison sans confirmation supplémentaire. Les caches, paquets et binaires de sondes demeurent dans l'état local ; les preuves et livrables textuels nécessaires sont conservés avec la livraison. Aucun changement de branche ni modification du code.

Proportionnalité : les deux assemblages du NuGet répondent au chargement explicite des fontes et les façades de conversion reflètent leurs moteurs réels ; aucune couche générique supplémentaire n'est recommandée par cette revue.

[Rapport HTML interactif](audit-report.html) · [Architecture](artefacts/AUDIT_ARCHITECTURE.md) · [Règles](artefacts/AUDIT_KIT.md) · [Dépendances](artefacts/AUDIT_DEPENDENCIES.md) · [Cohérence des API](artefacts/AUDIT_COHERENCE.md)