# Revue du module Conversion

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

## Clôture du module

**105 fichiers sur 105 lus intégralement et consignés**, sans réutiliser les anciens constats. Huit défauts actuels reproduits : sept Moyen et un Faible, dans `findings-Conversion.json`. Deux fichiers renvoient aussi à COH-001, porté uniquement dans `findings-Coherence.json`.

| ID | Localisation principale | Conséquence reproduite |
|---|---|---|
| CVT-001 | Html/HtmlSelector.cs:122 | Sélecteur descendant valide après attribut/pseudo-classe refusé. |
| CVT-002 | Conversion/Charts/ValueScale.cs:53 | Valeurs finies extrêmes, axe PDF NaN sans gap. |
| CVT-003 | Conversion/HtmlLayout/CssStyle.cs:40 | BOLD/ITALIC valides perdus, contrairement aux minuscules. |
| CVT-004 | Conversion/HtmlLayout/HtmlWordWriter.cs:64 | Rouge/gras/taille du td perdus après HTML vers DOCX puis lecture. |
| CVT-005 | Conversion/HtmlLayout/HtmlWordWriter.cs:380 | Retours de ligne pre supprimés avant un élément inline. |
| CVT-006 | Conversion/WordMarkdown/MarkdownDocumentWriter.cs:247 | Dossier image contenant un espace, définition Markdown invalide. |
| CVT-007 | Conversion/WordLayout/LineBreaker.cs:280 | U+10140 disponible dans la fonte devient deux U+FFFD à la coupure. |
| CVT-008 | Conversion/WordLayout/Paginator.cs:71 | NextColumn démarre sous la première colonne au lieu de la suivante. |

Preuve commune : `dotnet run --project .claude/audit/2026-11-09/artefacts/coherence-probe/CoherenceProbe.csproj -c Release`, code 0. La sonde référence les DLL Release existantes, utilise les vrais convertisseurs, sauvegarde/recharge DOCX et PDF, décode ses PNG, interroge les arbres HTML et mesure les lettres PDF. `coherence-probe/results.json`, `run.log`, sources et fichiers de preuve conservés. Elle ne remplace pas la suite du dépôt.

Sécurité : lecture manuelle des entrées/URI/HTML/limites/récursions, best-effort sans SAST général. Scan global des secrets séparé dans `metrics/SECURITY_SCAN.md`. Détection locale des modificateurs partial, stubs NotImplemented, suppressions de diagnostics et U+2014 : aucun candidat dans ce module. L'absence d'un motif n'est pas une preuve globale de sécurité.

Métriques : `metrics/crap_report.md` attend encore la collecte finale centrale ; aucun score ni taux de couverture actuel inventé. Aucun build source, suite complète, changement produit/test, politique ou publication exécuté par ce réviseur. Les variations de conventions publiques et leur coût de consommation sont détaillés dans `AUDIT_COHERENCE.md`, distincts des bugs fonctionnels.

Proportionnalité : les collaborateurs internes servent les phases réelles de parsing, mise en page ou sérialisation ; aucune façade universelle supplémentaire proposée pour uniformiser artificiellement les formats.

## src/OmniEurope.Documents/Conversion/DocumentConverters.cs

Preuve : Lecture intégrale actuelle : HTML texte vers Word, styles de classes, feuilles liées signalées et chemin Markdown/HTML vers PDF contrôlés.

RAS


## src/OmniEurope.Documents/Conversion/ExcelToPdf.cs

Preuve : Lecture intégrale actuelle : limites cellules, propagation GapDetails, construction Word par feuille et étendue des graphiques vérifiées.

RAS


## src/OmniEurope.Documents/Conversion/ImagesToPdf.cs

Preuve : Lecture intégrale actuelle : formats admis, pages TIFF, géométrie naturelle et conversion DPI contrôlées ; roundtrip réel dans coherence-probe.

RAS


## src/OmniEurope.Documents/Conversion/WordFields/WordFieldUpdater.cs

Preuve : Lecture intégrale actuelle : quatre passes bornées, champs de dates, numéros et références ; parcours XML et collecte des gaps examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlImages.cs

Preuve : Lecture intégrale actuelle : reconnaissance réelle du MIME, transcodage TIFF/EMF, URI et ressources séparées, erreurs et gaps contrôlés.

RAS


## src/OmniEurope.Documents/Conversion/WordMarkdown/MarkdownContext.cs

Preuve : Lecture intégrale actuelle : références notes/images, chemins relatifs, extensions MIME et compteurs de listes avec portée examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordToHtml.cs

Preuve : Lecture intégrale actuelle : options CSP, images et résultat public ; propagation source/context GapDetails et surcharges contrôlées.

RAS


## src/OmniEurope.Documents/Conversion/WordToMarkdown.cs

Preuve : Lecture intégrale actuelle : contrat GFM, ressources images, options dossier et propagation des gaps source/context contrôlés.

RAS


## src/OmniEurope.Documents/Conversion/WordToPdf.cs

Preuve : Lecture intégrale actuelle : fonts et conformance, pagination bornée, détection formes flottantes et propagation complète des gaps examinées.

RAS


## src/OmniEurope.Documents/Csv/CsvReader.cs

Preuve : Lecture intégrale actuelle : détection encodage/délimiteur, parser incrémental, caches champs, async, leaveOpen et nettoyage sur échec examinés ; vrais flux sondés.

RAS


## src/OmniEurope.Documents/Csv/CsvReaderOptions.cs

Preuve : Lecture intégrale actuelle : valeurs par défaut, limites caractères, encoding explicite/repli, politiques quoting et contrat CsvFormatException examinés.

RAS


## src/OmniEurope.Documents/Csv/CsvWriter.cs

Preuve : Lecture intégrale actuelle : quoting et neutralisation formules, BOM séparé et maintien fallback encodage, dispose/async et leaveOpen vérifiés.

RAS


## src/OmniEurope.Documents/Html/HtmlParser.cs

Preuve : Lecture intégrale actuelle : fusions document, clôtures implicites, profondeur 512, parents de tables et fin de balises contrôlés.

RAS


## src/OmniEurope.Documents/Markdown/MarkdownOptions.cs

Preuve : Lecture intégrale actuelle : presets immuables CommonMark/GitHub, HTML brut optionnel et filtre URL par défaut examinés.

RAS


## src/OmniEurope.Documents/Markdown/MarkdownParser.cs

Preuve : Lecture intégrale actuelle : parsing blocs/inlines, marqueurs tâches et tables ; options CommonMark et rendu HTML avec arbre contrôlés.

RAS


## src/OmniEurope.Documents/Conversion/ExcelToHtml.cs

Preuve : Lecture intégrale actuelle : options, budget plages et les deux façades ; vrai XLSX avec dessin illisible chargé et converti, contrat Gaps reproduit dans coherence-probe.

COH-001, Moyen, résultat sans Gaps/GapDetails malgré le diagnostic du classeur. Voir AUDIT_COHERENCE.md et findings-Coherence.json ; pas de doublon local.


## src/OmniEurope.Documents/Conversion/ExcelHtml/ExcelHtmlWriter.cs

Preuve : Lecture intégrale actuelle : feuille/plage/cellules, CSS et fusion bornée des cellules ; construction du résultat relue et perte de gap reproduite sur vrai XLSX.

COH-001 porté dans findings-Coherence.json : résultat limité à HTML/CSS, diagnostic source perdu. Aucun doublon dans findings-Conversion.json.


## src/OmniEurope.Documents/Csv/CsvDelimiterDetector.cs

Preuve : Lecture intégrale 59 lignes : première ligne non vide, candidats ordonnés et comptage hors guillemets ; cas sans séparateur et quoting désactivé examinés.

RAS


## src/OmniEurope.Documents/Csv/CsvFormula.cs

Preuve : Lecture intégrale 49 lignes : signes de formule après espaces/contrôles, tabulations initiales et exclusion des nombres par parsing invariant examinés.

RAS


## src/OmniEurope.Documents/Csv/CsvRecordParser.cs

Preuve : Lecture intégrale 298 lignes : états chunk/CRLF, guillemets doublés, EOF, compteurs physiques et bornes des caractères/champs contrôlés sans supposer une validation RFC stricte.

RAS


## src/OmniEurope.Documents/Csv/TextValueParser.cs

Preuve : Lecture intégrale 122 lignes : normalisation bornée, signes comptables, séparateurs, dates exactes et protection de dépassement TimeSpan examinés.

RAS


## src/OmniEurope.Documents/Diff/MyersDiff.cs

Preuve : Lecture intégrale 161 lignes : tableau diagonal partagé, recherche avant/arrière et pile explicite des sous-problèmes ; réduction préfixes/suffixes et remplacement de repli examinés.

RAS


## src/OmniEurope.Documents/Diff/TextDiff.cs

Preuve : Lecture intégrale 249 lignes : découpe lignes/UTF16/mots, comparaison configurable et traduction des marqueurs Myers en blocs ; contrats des terminators et numéros 1-based examinés.

RAS


## src/OmniEurope.Documents/Html/HtmlElement.cs

Preuve : Lecture intégrale 101 lignes : normalisation noms de balises/attributs, remplacement case-insensitive, listes classes et remontée Closest examinés.

RAS


## src/OmniEurope.Documents/Html/HtmlNodes.cs

Preuve : Lecture intégrale 261 lignes : mutations parent/enfants, prévention cycles, déplacement même parent, parcours pile explicite et sérialisation du document examinés.

RAS


## src/OmniEurope.Documents/Html/HtmlSanitizer.cs

Preuve : Lecture intégrale 172 lignes : politique tags/attributs, contenu actif supprimé, décodage puis filtrage des URL et callback explicite ; sécurité best-effort manuelle sans SAST général.

RAS


## src/OmniEurope.Documents/Html/HtmlSerializer.cs

Preuve : Lecture intégrale 95 lignes : échappement contexte texte et attribut cité, cas raw-text et void, commentaires et parcours des enfants examinés.

RAS


## src/OmniEurope.Documents/Html/HtmlTags.cs

Preuve : Lecture intégrale 58 lignes : ensembles void/raw/RCDATA, fermetures paragraphes et frontières de portée comparés aux usages du parser et serializer.

RAS


## src/OmniEurope.Documents/Html/HtmlTokenizer.cs

Preuve : Lecture intégrale 265 lignes : tokenisation texte/markup, récupération erreurs, raw/RCDATA jusqu à vraie balise fin, attributs cités et entités décodées contrôlés.

RAS


## src/OmniEurope.Documents/Markdown/MarkdownWriter.cs

Preuve : Lecture intégrale 182 lignes : échappement texte aux positions bloc/inline, marqueurs listes ordonnées, table GFM, champs courts et séparation des blocs examinés.

RAS


## src/OmniEurope.Documents/Html/HtmlSelector.cs

Preuve : Lecture intégrale 394 lignes : grammar selectors et predicates ; sonde réelle QuerySelectorAll sur DOM construit démontre FormatException pour sélecteurs descendants après attribut/nth/not.

CVT-001, Moyen : TryConsume (ligne 122) absorbe les espaces après ] ou ), puis ReadComplex (ligne 156) refuse le combinator descendant perdu. Reproductions : div[title] span, div:nth-child(1) span, div:not(.missing) span ; témoins div span et div[title]>span valides. Corriger la conservation du séparateur ; acceptation : chaque requête retourne le span attendu, syntaxe invalide toujours refusée. Voir coherence-probe/results.json selectorDescendant.


## src/OmniEurope.Documents/Conversion/Charts/CartesianPlot.cs

Preuve : Lecture intégrale 164 lignes : axe valeurs/catégories, orientation, espacements, labels et bornes de boîtes ; dépendance ValueScale relue avec cas extrême réel.

Dépendance au défaut CVT-002 porté par ValueScale.cs : l axe récupère la valeur NaN du calcul d échelle ; aucun doublon de constat.


## src/OmniEurope.Documents/Conversion/Charts/ChartPainter.cs

Preuve : Lecture intégrale 190 lignes : clipping au cadre, légende/catégories, tailles, titres et graphe de dispatch plots ; codes pertes et limites de séries examinés.

RAS


## src/OmniEurope.Documents/Conversion/Charts/ChartPlots.cs

Preuve : Lecture intégrale 250 lignes : barres standard/empilées, segments discontinus, labels et secteurs proportionnels absolus ; géométrie des anneaux et limites de valeurs examinées.

RAS


## src/OmniEurope.Documents/Conversion/Charts/ChartText.cs

Preuve : Lecture intégrale 66 lignes : métriques/layout partagé, culture NumberFormatter, rotation et couleur ; ChartBox.Inset protège les dimensions négatives.

RAS


## src/OmniEurope.Documents/Conversion/Charts/ValueScale.cs

Preuve : Lecture intégrale 135 lignes : choix du pas 1/2/5, somme des empilements et proportion ; vrai XLSX chargé avec valeurs finies -1E308 et 1E308 converti en PDF contenant NaN.

CVT-002, Moyen : high-low déborde en infini à la ligne 53, Nice puis Min/Max produisent NaN. La conversion réussit et le vrai texte PDF affiche NaN sans gap ; voir coherence-probe/finite-extreme-chart.xlsx, .pdf et results.json finiteChart. Calculer une échelle normalisée bornée ; acceptation : mêmes valeurs finies sans NaN et signes conservés, ou refus explicite documenté avant production.


## src/OmniEurope.Documents/Conversion/ExcelHtml/ExcelHtmlCss.cs

Preuve : Lecture intégrale 151 lignes : regroupement règles, largeur numérique invariante, mapping alignement, validation RRGGBB et réduction des noms de police examinés.

RAS


## src/OmniEurope.Documents/Conversion/ExcelRowLayout.cs

Preuve : Lecture intégrale 166 lignes : cartographie fusions par ligne, allocation propriétaires et débordement texte dans cellules réellement vides ; limites de plage et alignements examinés.

RAS


## src/OmniEurope.Documents/Conversion/ExcelSheetCharts.cs

Preuve : Lecture intégrale 48 lignes : ancrages points/cellules, fusion propriétaire, réduction selon largeur page et flottants Word devant table examinés.

RAS


## src/OmniEurope.Documents/Conversion/ExcelSheetTable.cs

Preuve : Lecture intégrale 274 lignes relue sans troncature : largeurs Excel/points, répétition entêtes gelées, overflow texte/nombres, styles et fusion verticale examinés.

RAS


## src/OmniEurope.Documents/Conversion/NoteNumbering.cs

Preuve : Lecture intégrale 99 lignes : lecture ordonnée références, réglages de section, numérotation séparée notes et fin de notes, reprise par pages et comparaison de mapping examinées.

RAS


## src/OmniEurope.Documents/Conversion/WordResolution.cs

Preuve : Lecture intégrale 22 lignes : résolution styles communs puis formats conditionnels et priorité finale des propriétés directes Word vérifiées.

RAS


## src/OmniEurope.Documents/Conversion/WordTableFormats.cs

Preuve : Lecture intégrale 107 lignes : régions et ordre des styles conditionnels, coins/bandes, marges héritées et priorité des bordures cellules/table examinés.

RAS


## src/OmniEurope.Documents/Conversion/HtmlLayout/CssClassRules.cs

Preuve : Lecture intégrale 188 lignes : sélecteurs simples autorisés, cascade par position source, priorité inline, médias, commentaires et exclusion at-rules examinés ; aucune feuille distante récupérée.

RAS


## src/OmniEurope.Documents/Conversion/HtmlLayout/HtmlTableBuilder.cs

Preuve : Lecture intégrale 136 lignes : réordonnancement head/body/foot, grille colspan/rowspan bornés, colonnes Word, cellules entête et répétition des lignes examinés.

CVT-004 porté par HtmlWordWriter.WriteInto : les styles de caractères de td/th sont perdus au passage des cellules. Aucun doublon ici.


## src/OmniEurope.Documents/Conversion/HtmlLayout/HtmlToWordGapCodes.cs

Preuve : Lecture intégrale 11 lignes : trois codes de gaps feuilles liées/images, noms et ressources attendues comparés aux appels du convertisseur.

RAS


## src/OmniEurope.Documents/Conversion/HtmlLayout/CssStyle.cs

Preuve : Lecture intégrale 143 lignes : unités/couleurs/décorations et cascade examinées ; vraie conversion HTML puis DOCX sauvé/rechargé prouve que BOLD/ITALIC sont perdus contrairement aux mots minuscules.

CVT-003, Faible : valeurs CSS font-weight et font-style comparées ordinalement aux minuscules dans Declarations ; preuve coherence-probe/results.json htmlFormatting, LowercaseBold.Bold=true, UppercaseBold.Bold=false. Normaliser uniquement les mots-clés insensibles à la casse ; acceptation : BOLD/ITALIC comme bold/italic, noms de familles et textes préservés.


## src/OmniEurope.Documents/Conversion/HtmlLayout/HtmlWordWriter.cs

Preuve : Lecture intégrale 454 lignes : parcours blocs/inline, listes, tableaux, images data URI, liens sûrs et gestion espaces examinés ; deux pertes reproduites sur conversions réelles HTML/DOCX.

CVT-004, Moyen : WriteInto ne passe pas le style de caractères de td/th dans CssStyle.Run, rouge/gras/20pt perdus mais conservés sur span ; DOCX rechargé pour preuve. CVT-005, Moyen : Text en mode pre retire chaque fin de ligne de fragment via TrimEnd, donc a\n<b>b</b>\nc devient ab\nc. Voir coherence-probe/results.json htmlFormatting. Accepter styles de cellule identiques au span témoin et préserver le saut entre a et b, avec passage DOCX.


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlBlockWriter.cs

Preuve : Lecture intégrale 114 lignes : paragraphes adressés, titres/listes, wrappers contrôles, contextual spacing et boîtes texte après ancre examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlContext.cs

Preuve : Lecture intégrale 50 lignes : état par conversion, références notes uniques, révisions acceptées/marquées et comparaison adresse sans casse examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlControlWrappers.cs

Preuve : Lecture intégrale 62 lignes : préfixes communs de marques, fermeture/ouverture wrappers imbriqués et attributs de contrôle passés à Encode examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlCss.cs

Preuve : Lecture intégrale 224 lignes : nombres invariants, couleurs validées, familles réduites, effets run/bloc, bordures et différences de style examinés ; valeurs document non concaténées comme CSS libre.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlGapCodes.cs

Preuve : Lecture intégrale 26 lignes : codes des pertes HTML pour sauts, formes, notes, fontes/images, paragraphes sans adresse et entêtes/pieds examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlOutput.cs

Preuve : Lecture intégrale 99 lignes : classes générées remplacent style, échappement attributs/textes, paires UTF16 conservées et contrôles interdits supprimés examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlPageStyle.cs

Preuve : Lecture intégrale 45 lignes : validation ASCII du nonce et padding, choix link/style et écriture via attributs encodés ; absence de valeur document dans Raw CSS examinée.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlStyleClasses.cs

Preuve : Lecture intégrale 45 lignes : déduplication déterministe des déclarations, rejet caractères terminant une règle/style et ordre source des classes examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlInlineWriter.cs

Preuve : Lecture intégrale 318 lignes : regroupement runs/révisions, listes, notes, champs et ressources images ; filtrage schemes liens et clôture wrappers examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlPageWriter.cs

Preuve : Lecture intégrale 203 lignes : feuille CSS, sections/running blocks, colonnes, gaps limites du navigateur et références de notes récursives bornées par IDs examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordHtml/HtmlTableWriter.cs

Preuve : Lecture intégrale 223 lignes : grille avant/après, fusion verticale en rowspan, ordre styles conditionnels et priorités bordures/marges ; aucune cellule couverte écrite deux fois.

RAS


## src/OmniEurope.Documents/Conversion/Emf/EmfRenderer.cs

Preuve : Lecture intégrale 441 lignes : validation en-tête et tailles records, compteurs points bornés aux octets, chemins/états, polices/bitmap et transformations ; approximations EMF déclarées examinées.

RAS


## src/OmniEurope.Documents/Conversion/Emf/EmfState.cs

Preuve : Lecture intégrale 86 lignes : composition affine, états GDI immuables, modes métriques/isotropiques, ratio nul et conversion vers coordonnées page examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordFields/FieldGapCodes.cs

Preuve : Lecture intégrale 18 lignes : codes de champs non calculés, références absentes, tables matières refusées et pagination non stabilisée relus avec leurs appelants.

RAS


## src/OmniEurope.Documents/Conversion/WordFields/WordFieldValues.cs

Preuve : Lecture intégrale 103 lignes : décodage des pictures date/heure, séquences citées, designators AM/PM et formats numéro/page Roman/alphabetic examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordFields/WordPageMap.cs

Preuve : Lecture intégrale 72 lignes : parcours placements et notes, première page des adresses, sections et comptes, récursion dans les lignes de table examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordFields/WordTocBuilder.cs

Preuve : Lecture intégrale 304 lignes : switches acceptés/refusés, conservation texte autour champ, sélection titres/styles, bookmarks uniques, génération PAGEREF et styles TOC examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordFields/WordXmlFields.cs

Preuve : Lecture intégrale 224 lignes : pile des champs imbriqués, marquage instructions, exclusions révisions/fallback, tokens cités et remplacement XML des résultats avec formats examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordPdfGapCodes.cs

Preuve : Lecture intégrale 24 lignes : codes partagés PDF pour polices/formes, glyphes absents, pagination, graphiques et images ; aucun stub fonctionnel annoncé.

RAS


## src/OmniEurope.Documents/Conversion/WordMarkdown/MarkdownInlineWriter.cs

Preuve : Lecture intégrale 330 lignes : échappement bloc/inline, piles de marqueurs, espaces retardés, liens scheme filtré, notes/images et signalement propriétés non représentables examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordMarkdown/MarkdownGapCodes.cs

Preuve : Lecture intégrale 32 lignes : codes de pertes Markdown sur styles, structures, éléments non représentés et références ; comparés aux branches des writers.

RAS


## src/OmniEurope.Documents/Conversion/WordMarkdown/MarkdownDocumentWriter.cs

Preuve : Lecture intégrale 265 lignes : listes/indentation, tables et notes, échappement des blocs et écritures des références images ; vraie conversion avec ImageFolder contenant un espace reproduit référence non reconnue.

CVT-006, Moyen : Images écrit le chemin brut dans la définition de référence ; ImageFolder=my images donne [image1]: my images/image1.png, puis MarkdownRenderer ne produit aucune image. Conserver Path disque mais encoder/citer la destination Markdown ; acceptation : image reconnue et URL vers le bon fichier avec espaces.


## src/OmniEurope.Documents/Markdown/HtmlBlockSyntax.cs

Preuve : Lecture intégrale 127 lignes : sept débuts HTML CommonMark, tags blocs et conditions de fin, arrêt aux lignes vides et règles raw tags examinés.

RAS


## src/OmniEurope.Documents/Markdown/InlineCleanup.cs

Preuve : Lecture intégrale 45 lignes : fusion amortie via StringBuilder, suppression textes vides et traitement récursif des seuls containers produits par le parser examinés.

RAS


## src/OmniEurope.Documents/Markdown/InlineHtmlSyntax.cs

Preuve : Lecture intégrale 190 lignes : grammaire noms/attributs, valeurs citées/non citées, déclarations/commentaires et CDATA, indices de fin sans interprétation script examinés.

RAS


## src/OmniEurope.Documents/Markdown/ListMarker.cs

Preuve : Lecture intégrale 153 lignes : marqueurs numériques bornés à neuf chiffres, indentation tabs, interruption paragraphes et détermination listes tight examinés.

RAS


## src/OmniEurope.Documents/Markdown/MarkdownSyntax.cs

Preuve : Lecture intégrale 295 lignes : surface des arbres publics, invariants de construction interne, lignes source, propriétés read-only et distinction raw HTML/texte examinés.

RAS


## src/OmniEurope.Documents/Markdown/MarkdownUrl.cs

Preuve : Lecture intégrale 110 lignes : schemes après compaction contrôles, relative/fragments et data image, encodage URL UTF8 incluant surrogates et escapes existantes examinés.

RAS


## src/OmniEurope.Documents/Markdown/TableSyntax.cs

Preuve : Lecture intégrale 124 lignes : détection entête/délimiteur, alignements, lignes brutes et traitement pipes échappées ; aucune assurance de conformité exhaustive au corpus GFM.

RAS


## src/OmniEurope.Documents/Markdown/Autolinks.cs

Preuve : Lecture intégrale 301 lignes : autoliens CommonMark et extension GFM, grammaire domains/email, frontières de mots et suppression ponctuation/parenthèses terminales examinées.

RAS


## src/OmniEurope.Documents/Markdown/MarkdownHtmlWriter.cs

Preuve : Lecture intégrale 300 lignes : émission des blocs/listes/tables, échappement attributs, filtre URL avant liens/images, extraction alternative text et raw HTML explicitement parsé examinés.

RAS


## src/OmniEurope.Documents/Markdown/LinkSyntax.cs

Preuve : Lecture intégrale 340 lignes : labels bornés, décodage entités, destinations brutes/encadrées, parenthèses max32 et définitions de référence multilignes examinés.

RAS


## src/OmniEurope.Documents/Markdown/InlineParser.cs

Preuve : Lecture intégrale 375 lignes : avance de position sur chaque token, backticks, flanking Unicode, empilement brackets, résolution références et neutralisation liens imbriqués examinés.

RAS


## src/OmniEurope.Documents/Markdown/InlineList.cs

Preuve : Lecture intégrale 281 lignes : liste double et piles de délimiteurs, règle de trois, enveloppement liens/emphasis, mise à jour de pointeurs et retrait des bornes examinés.

RAS


## src/OmniEurope.Documents/Markdown/BlockParser.cs

Preuve : Lecture intégrale 536 lignes : pile blocs, profondeur max128, continuation lazy/indentée, tabulations partielles, code fence, extraction références et remplacement NUL examinés.

RAS


## src/OmniEurope.Documents/Markdown/BlockStarts.cs

Preuve : Lecture intégrale 301 lignes : priorité quote/titre/fence/html/table/thematic/list/code, clôtures fence et séquences finales, interruption paragraphes et indentation examinées.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/BidiLine.cs

Preuve : Lecture intégrale 126 lignes : morceaux niveaux BiDi, largeur ajustée, ordre visuel L2, L1 sur blancs/tabs et inversion par graphèmes avec miroirs examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/Columns.cs

Preuve : Lecture intégrale 77 lignes : colonnes explicites ou égales, espaces par colonne et remplissage glouton avec règles keep ; retraits de slots bornés examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/FlowItems.cs

Preuve : Lecture intégrale 126 lignes : contrats hauteur/espaces/ruptures, peinture des frames/bordures, métadonnées de ligne et séparateur de notes examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/InkPainter.cs

Preuve : Lecture intégrale 20 lignes : conversion des points d encre en tracés vectoriels ouverts avec largeur/couleur/opacité examinée.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/PageFields.cs

Preuve : Lecture intégrale 58 lignes : cache entêtes/pieds par valeurs page, restauration Fields via finally et maintien de hauteur lors du recalcul examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/PageFrame.cs

Preuve : Lecture intégrale 76 lignes : bornes corps après entêtes/pieds, notes et séparateurs, régions, placements et flottants à coordonnées explicites examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/TextStyle.cs

Preuve : Lecture intégrale 135 lignes : résolution script complexe, tailles de repli, shifts, couleurs/highlights et décorations en points examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/Tokens.cs

Preuve : Lecture intégrale 151 lignes : objets tokens champs/tabs/textes/boîtes/ancrages, dimensions réservées, fonctions peinture et contexte champs examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/BlockLayout.cs

Preuve : Lecture intégrale 179 lignes : résolution de paragraphes, keep/ruptures et espaces fusionnés, dispatch formes, rognage boîtes et clipping images ; fallback images suivi dans LayoutContext.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/InlineBuilder.cs

Preuve : Lecture intégrale 249 lignes : résolution des scripts/polices, texte caché et suppressions, champs page, symboles, coupures mots et capitales/petites capitales examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/Symbols.cs

Preuve : Lecture intégrale 119 lignes : mapping Symbol/Wingdings, métriques AFM pour avances, police symbole et signalement approximations des zones privées examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/LayoutContext.cs

Preuve : Lecture intégrale 175 lignes : caches images/métriques par conversion, signalement des fontes, restauration compteurs via finally et nouvelle passe numérotation notes ; fichier image invalide réellement converti sans crash dans coherence-probe.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/FloatLayout.cs

Preuve : Lecture intégrale 298 lignes : références page/marges/ligne/caractère, ancrages et wraps, retrait des intervalles, passes bornées au nombre de formes, évitement tableaux et superposition examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/FragmentPainter.cs

Preuve : Lecture intégrale 207 lignes : peinture tokens/textes avec metrics, underline/strike, tab leaders, liens et flottants différés ; séquences SaveState/RestoreState et unités examinées.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/PageAlignment.cs

Preuve : Lecture intégrale 101 lignes : centrage/bas et justification verticale, gap pour formes/régions multiples, calcul des paragraphes et mise à jour des bottoms examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/PagePainter.cs

Preuve : Lecture intégrale 90 lignes : ordre running/body/formes avant/notes, bookmarks et séparateurs, contexte numérotation page/section, frames et colonnes examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/ParagraphFlow.cs

Preuve : Lecture intégrale 176 lignes : relayout à largeur/zone différente, niveaux BiDi par caractères, métadonnées premières/dernières lignes, widow/keep et sources de bookmarks examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/RowItem.cs

Preuve : Lecture intégrale 241 lignes : mesure cellules, coupes récursives et keep, marges aux suites, ordre fills/contenus/bordures, clipping hauteur exacte et overflow examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/TableLayout.cs

Preuve : Lecture intégrale 150 lignes : projection révisions visibles, colonnes et pourcentages, contexte relayout conservant les listes, formats conditionnels et merges verticaux examinés.

RAS


## src/OmniEurope.Documents/Conversion/WordLayout/LineBreaker.cs

Preuve : Lecture intégrale 575 lignes : overflow, justification, tabs, soft breaks et découpe mots ; vraie conversion PDF du caractère U+10140 pris dans la fonte fournie montre séparation des surrogates en colonne étroite.

CVT-007, Moyen : SplitWord coupe les chaînes au nombre d unités UTF16, sans protéger les paires. Codepoint65856 avec glyph disponible, témoin40pt conserve U+10140, colonne3,06pt produit deux U+FFFD et Gaps vide. Preuve coherence-probe/results.json surrogateWrap et symbol-wide/narrow.pdf. Couper aux frontières scalaires, acceptation même symbole intact dans le PDF étroit.


## src/OmniEurope.Documents/Conversion/WordLayout/Paginator.cs

Preuve : Lecture intégrale 580 lignes : files, keep/backtrack, notes et caches, relayout flottants/tables, balance par essais bornés ; vrai PDF mesuré pour section NextColumn dans document à deux colonnes.

CVT-008, Moyen : StartSection traite NextColumn comme Continuous et crée une région sous le contenu au lieu de passer dans la colonne suivante. PDF réel : premier A X=10,Y=179,5264 ; premier B X=10,Y=157,0244, seconde colonne attendue X=105 ; Gaps vide. Voir coherence-probe/next-column.pdf et results.json nextColumnSection. Préserver le fonctionnement Continuous et faire démarrer NextColumn dans la colonne suivante.
