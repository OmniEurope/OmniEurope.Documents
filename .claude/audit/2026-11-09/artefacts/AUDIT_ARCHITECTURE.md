# Audit de l’architecture

Cible : develop, commit 2594b1d, 9 novembre 2026. Pass1 et Pass2b terminées. **RAS sur l’architecture examinée** : aucun constat ne franchit les quatre tests de pertinence. findings-Architecture.json contient une liste vide. Ce résultat ne certifie pas chaque fonctionnalité.

## Cible et limites

Le contrat effectif est deux assemblages dans un NuGet. Il est explicite dans README.md:135 et docs/plans/PLAN-009-controles-de-contenu-word.md:23. La séparation des polices supplémentaires ne constitue pas un défaut.

Prévol réellement exécuté : get_project_graph et find_symbol(WordDocument), puis cycles de projets/types, diagnostics et graphe d’appels. Le TFM netcoreapp1.0 renvoyé par le MCP est incorrect : Directory.Build.props:3 impose net10.0, aucun des quatre csproj ne le remplace ; global.json et dotnet --version indiquent 10.0.403. Cette métadonnée MCP est exclue des conclusions.

Aucun build, test, pack ni publication exécuté par ce réviseur. L’orchestrateur signale une compilation Release avec zéro erreur et avertissement, enregistrée dans metrics/build-release.log, distincte de ces résultats sémantiques. Aucun paquet ni parcours WebAssembly n’a été réexécuté ici.

Le MCP des règles du kit n’est pas exposé. Secours autorisé : lecture de C:/Dev/_Generic/docs/code-rules.md, registre et sections applicables. Anomalie documentaire : 86 325 octets, au-delà de 20 Ko. .claude/decisions.md a été lu après sa création par l’orchestrateur. Aucun ADR trouvé dans l’inventaire.

## Périmètre lu

AGENTS.md, README.md, registre des plans et plans actifs 007/009/010, périmètre initial archivé et sections structurelles des plans archivés ; solution, quatre csproj, Directory.Build.props, Directory.Packages.props, global.json, NuGet.Config, .editorconfig, .config/verify-rules.json, workflows CI/publication, lanceur et contrôle de paquet.

Sources structurelles : façades Conversion, modèles Word/Excel, FontLibrary et DocumentFonts, DocumentGap/GapSet, infrastructures OPC/ZIP et types concernés par les cycles ci-dessous. La revue profonde des 949 fichiers et 377 C# de production de l’inventaire global relève des registres de modules ; ce rapport ne revendique pas cette lecture individuelle.

## Graphe des projets

Une flèche représente une référence de compilation, les pointillés l’emballage.

~~~mermaid
graph LR
  Extra[OmniEurope.Documents.Fonts] --> Core[OmniEurope.Documents]
  Tests[OmniEurope.Documents.Tests] --> Core
  Tests --> Extra
  Stress[OmniEurope.Documents.StressTests] --> Core
  Package[NuGet OmniEurope.Documents] -. embarque .-> Core
  Package -. embarque .-> Extra
~~~

Le projet Fonts est non packable et référence le cœur (src/OmniEurope.Documents.Fonts/OmniEurope.Documents.Fonts.csproj:11 et :16). Le cœur n’a aucun ProjectReference vers Fonts. Ses cibles d’emballage invoquent ce projet, puis ajoutent DLL/XML/PDB (src/OmniEurope.Documents/OmniEurope.Documents.csproj:59). Cette arête d’emballage n’est pas une référence circulaire d’assemblages. La version vient de Directory.Build.props:12.

Le contrôleur de paquet vérifie deux assemblages, versions, symboles, ressources et licences et refuse les dépendances NuGet (.github/scripts/verify-package.ps1:31, :44, :62, :104). Sa lecture ne prouve pas son exécution aujourd’hui. Aucun PackageReference n’est présent dans les deux projets de production lus. Les dépendances xUnit/TRX/coverlet sont dans les projets de tests.

## Chemins entre formats et contrats

~~~mermaid
graph LR
  Markdown --> HTML
  HTML --> Word[Modèle Word]
  Excel[Modèle Excel] --> ToWord[ExcelToPdf.ToWord]
  ToWord --> Word
  Word --> Layout[WordToPdf et mise en page]
  Layout --> Pdf[Écriture PDF]
  Images --> Pdf
  Word --> WordHtml[WordToHtml]
  Word --> WordMarkdown[WordToMarkdown]
  Excel --> ExcelHtml[ExcelToHtml]
  Excel --> Csv[XlsxCsvConverter]
  Csv --> Excel
~~~

Sources lues, relatives à src/OmniEurope.Documents : Conversion/DocumentConverters.cs:19, :57, :71, :83 ; Conversion/ExcelToPdf.cs:41 et :49 ; Conversion/WordToPdf.cs:50 ; Conversion/ImagesToPdf.cs:37 ; Excel/XlsxCsvConverter.cs:20 et :64. Le graphe sémantique de ExcelToPdf.Convert, profondeur 2, résout quinze dépendances, dont ToWord, WordToPdf.Convert, Paginate, PagePainter.Paint et PdfDocumentBuilder.ToArray.

WordPdfResult partagé par HTML, Markdown et Excel et WordPdfOptions exposé sous ExcelPdfOptions.Pdf reflètent ce chemin réel par le moteur Word. L’asymétrie de noms seule ne prouve aucun défaut. Les budgets MaxCells différents entre CSV, HTML et PDF sont explicitement documentés avec des parcours matériels différents, flux texte, HTML ou mise en page PDF. Aucune uniformisation arbitraire n’est recommandée.

Les modèles Word/Excel reconstruisent un sous-ensemble tandis que les éditeurs conservent les parties OPC. Cette différence est annoncée dans Word/WordDocument.cs:8, Excel/XlsxWorkbook.cs:7 et le README. Les infrastructures communes lues sont Internal/OpcPackage.cs, Internal/SafeZip.cs, Internal/OpenXmlPackageWriter.cs, Fonts/FontLibrary.cs, DocumentGap.cs et Internal/GapSet.cs. Les questions de comportements de conversion et de noms publics sont traitées dans leurs registres ; elles ne sont pas dupliquées ici.

## Cycles sémantiques qualifiés

detect_circular_dependencies(scope=projects) rend zéro cycle. Le scope types du cœur rend dix chaînes. Leur qualification repose sur les sources lues, pas sur leur seul nombre.

| Chaînes | Sources et rôle |
|---|---|
| PdfDocumentBuilder ↔ PdfCanvas | Pdf/Writing/PdfDocumentBuilder.cs:17, :86 ; PdfCanvas.cs:14, :22. Le document possède les pages, celles-ci accèdent à ses ressources. |
| XlsxWorkbook ↔ XlsxWorksheet et XlsxWorksheet ↔ XlsxCell | Excel/XlsxWorkbook.cs:15, :56 ; XlsxWorksheet.cs:23, :28, :98 ; XlsxCell.cs:34, :42. Liens parent/enfant d’un même agrégat. |
| BlockLayout ↔ ShapeFactory | Conversion/WordLayout/BlockLayout.cs:19 et type ShapeFactory dans le même fichier. La zone de texte remet ses blocs imbriqués en page. |
| RedactionState ↔ RedactionRewriter et RedactionPatterns → RedactionState → RedactionRewriter → RedactionPatterns | Pdf/Editing/RedactionState.cs:62, RedactionRewriter.cs:74, RedactionPatterns.cs:14. Opérateurs, état graphique et motifs du même sous-système. |
| PageRenderer ↔ TextPainter, ImagePainter, TransparencyPainter | Pdf/Rendering/PageRenderer.cs:101, :102, :103 ; TextPainter.cs:12, ImagePainter.cs:14, TransparencyPainter.cs:21. Collaborateurs du contexte graphique de rendu. |
| WordContentReader ↔ WordDrawingReader | Word/Reading/WordContentReader.cs:93 ; WordDrawingReader.cs:16, :197. Un dessin peut contenir des blocs, donc rappeler leur lecteur. |

Ces relations décrivent le graphe ; aucun effet observable imposant de les casser n’est établi. Elles ne deviennent pas des constats par leur seule présence.

## Diagnostics et corrections antérieures

metrics/roslyn-project-graph.json contient la réponse brute du graphe. metrics/semantic-preflight.json contient symbole, cycles, diagnostics, graphe d’appels et vérification TFM. Le diagnostic solution rend 53 entrées, 52 hidden et une info, aucune error ni warning dans cette réponse. Ces diagnostics restent distincts d’une compilation et ne sont pas transformés en constats.

La plage 17d9e29..5fed746 a été réexaminée pour son empreinte structurelle : inventaire des changements, diff de production et ajout du garde de couverture dans la CI. Elle ne crée aucun projet, dépendance de production ni couche d’architecture. Les corrections restent dans les composants CSV, formules, modèle/rendu Word et PDF existants. Les preuves de PLAN-007 sont historiques, elles ne sont pas présentées comme exécutées aujourd’hui ; aucun ancien constat n’est réémis.

Proportionnalité : deux assemblages dans un paquet, façades statiques et collaborateurs internes spécialisés restent proportionnés au contrat actuel ; une seule DLL supprimerait le chargement différé des polices demandé, tandis que des paquets par format ou une couche générique supplémentaire n’ont aucun besoin démontré dans cette passe.
