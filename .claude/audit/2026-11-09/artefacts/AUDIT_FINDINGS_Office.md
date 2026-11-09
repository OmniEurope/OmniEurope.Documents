# Revue du module Office

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

Bilan final : 142 fichiers sur 142 inspectés dans cette passe, dont 41 XSD par parsing natif intégral, comparaison octet pour octet aux archives Ecma officielles et aux ressources Release, ensemble embarqué compilé. Huit constats dans findings-Office.json : sept moyens, un faible. Les preuves ciblées sont des appels réels des API publiques de la DLL Release, sans mocks ; les scénarios d’entrée Word/Strict concernés sont aussi validés sur les schémas officiels. Aucun code, test, version ni publication modifié.

| Constat | Conséquence démontrée |
|---|---|
| OFF-001 | Une liste MC séparée par tabulation ou LF fait refuser un DOCX valide. |
| OFF-002 | Le texte littéral _x0042_ édité devient B après sauvegarde et relecture. |
| OFF-003 | Un XLSX Strict valide est signalé à tort comme dépourvu de feuille ; correction minimale du diagnostic et du contrat, aucun ajout implicite de support. |
| OFF-004 | IFERROR/IFNA doublent le calcul et dépassent un budget suffisant pour leur opérande. |
| OFF-005 | CEILING/FLOOR sur un nombre fini élevé lèvent OverflowException et bloquent le reste du recalcul. |
| OFF-006 | Append échoue sur une cible DOCX valide sans section finale facultative. |
| OFF-007 | Un DropDownList vide accepte du texte libre sans signaler de gap. |
| OFF-008 | Le remplacement linguistique de ab dans a+U+00AD+b laisse un b supplémentaire. |

Limites : complexité/CRAP et couverture courantes encore en attente de la collecte finale dans metrics/crap_report.md. Sécurité manuelle best-effort, non outillée par SAST général ; portée de Gitleaks et dépendances dans metrics/SECURITY_SCAN.md. La sonde REPT interrompue ne prouve aucun résultat ni exception, détail dans metrics/office-probe-limits.md ; TextFunctions.cs reste non réutilisable en cache pour cette limite. L’anomalie d’index temporaire est séparée du produit dans metrics/office-state-recovery.json : 103 enregistrements originaux conservés, aucune preuve/date reconstruite. Contrôle final des 142 chemins et empreintes dans metrics/office-final-check.json.

Proportionnalité : modèles de format, éditeurs OPC et composants spécialisés répondent aux parcours actuels ; remplacer ces chemins par une abstraction de document universelle ajouterait une couche sans besoin démontré. Les corrections proposées restent locales aux comportements reproduits.

## src/OmniEurope.Documents/Word/Schemas/NOTICE-Ecma.txt

Preuve : Lecture intégrale : trois archives officielles, licences et règles de conservation, contournement des trois defaults stricts explicitement déclaré.

RAS


## src/OmniEurope.Documents/Word/Validation/OfficeSchemas.cs

Preuve : Lecture intégrale Compile/CorrectStrictDefaults/XmlNamespaceSchema : ressources embarquées, resolver nul, trois defaults traités en mémoire et verrou de validation.

RAS


## src/OmniEurope.Documents/Word/Validation/WordSchemaValidator.cs

Preuve : Lecture intégrale Validate/ValidatePart/CheckRelationshipIds/Parse/PathOf : parties XML, MC, références et limites documentées, fermeture du flux contrôlée.

RAS


## src/OmniEurope.Documents/Word/Validation/WordSchemaError.cs

Preuve : Lecture intégrale du record public : coordonnées 1-based, chemin et message, contrat MC et ToString concordants.

RAS


## src/OmniEurope.Documents/Word/Validation/WordSchemaReport.cs

Preuve : Lecture intégrale : listes erreurs/parties contrôlées/non contrôlées et IsValid fondé explicitement sur les seules parties validées.

RAS


## src/OmniEurope.Documents/Word/Validation/MarkupCompatibility.cs

Preuve : Lecture intégrale scopes/Alternate/Requires/ProcessContent ; DOCX réel DLL Release valide avec espace, invalide avec tabulation ou LF dans Ignorable, preuve JSON dédiée.

OFF-001 : listes MC découpées sur espace seul, faux refus de documents conformes.


## src/OmniEurope.Documents/Charts/Chart.cs

Preuve : Lecture intégrale des enums et modèles publics : types supportés/Unsupported, catégories/valeurs, couleurs, tailles effectives et budgets internes explicités.

RAS


## src/OmniEurope.Documents/Charts/ChartPalette.cs

Preuve : Lecture intégrale Office/Roles/Presets, résolution couleurs, pourcentages et conversions HSL ; transformations bornées et index négatifs normalisés.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/dml-chart.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 218 déclarations, espace http://schemas.openxmlformats.org/drawingml/2006/chart, 4 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/dml-chartDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 17 déclarations, espace http://schemas.openxmlformats.org/drawingml/2006/chartDrawing, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/dml-diagram.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 138 déclarations, espace http://schemas.openxmlformats.org/drawingml/2006/diagram, 3 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/dml-lockedCanvas.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 1 déclarations, espace http://schemas.openxmlformats.org/drawingml/2006/lockedCanvas, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/dml-main.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 361 déclarations, espace http://schemas.openxmlformats.org/drawingml/2006/main, 6 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/dml-picture.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 3 déclarations, espace http://schemas.openxmlformats.org/drawingml/2006/picture, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/dml-wordprocessingDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 33 déclarations, espace http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing, 4 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-bibliography.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 9 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/bibliography, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-commonSimpleTypes.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 28 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-customXmlDataProperties.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 4 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/customXml, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-customXmlSchemaProperties.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 3 déclarations, espace http://schemas.openxmlformats.org/schemaLibrary/2006/main, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-documentPropertiesCustom.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 3 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/custom-properties, 2 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-documentPropertiesExtended.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 5 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/extended-properties, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-documentPropertiesVariantTypes.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 44 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-math.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 92 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/math, 3 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/shared-relationshipReference.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 15 déclarations, espace http://schemas.openxmlformats.org/officeDocument/2006/relationships, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/vml-main.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 73 déclarations, espace urn:schemas-microsoft-com:vml, 7 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/vml-officeDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 120 déclarations, espace urn:schemas-microsoft-com:office:office, 3 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/vml-presentationDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 4 déclarations, espace urn:schemas-microsoft-com:office:powerpoint, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/vml-spreadsheetDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 4 déclarations, espace urn:schemas-microsoft-com:office:excel, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/vml-wordprocessingDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 15 déclarations, espace urn:schemas-microsoft-com:office:word, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Transitional/wml.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 436 déclarations, espace http://schemas.openxmlformats.org/wordprocessingml/2006/main, 6 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/dml-chart.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 210 déclarations, espace http://purl.oclc.org/ooxml/drawingml/chart, 4 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/dml-chartDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 17 déclarations, espace http://purl.oclc.org/ooxml/drawingml/chartDrawing, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/dml-diagram.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 138 déclarations, espace http://purl.oclc.org/ooxml/drawingml/diagram, 3 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/dml-lockedCanvas.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 1 déclarations, espace http://purl.oclc.org/ooxml/drawingml/lockedCanvas, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/dml-main.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 354 déclarations, espace http://purl.oclc.org/ooxml/drawingml/main, 6 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/dml-picture.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 3 déclarations, espace http://purl.oclc.org/ooxml/drawingml/picture, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/dml-wordprocessingDrawing.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 33 déclarations, espace http://purl.oclc.org/ooxml/drawingml/wordprocessingDrawing, 4 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-bibliography.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 9 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/bibliography, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-commonSimpleTypes.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 21 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/sharedTypes, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-customXmlDataProperties.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 4 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/customXml, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-customXmlSchemaProperties.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 3 déclarations, espace http://purl.oclc.org/ooxml/schemaLibrary/main, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-documentPropertiesCustom.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 3 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/customProperties, 2 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-documentPropertiesExtended.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 5 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/extendedProperties, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-documentPropertiesVariantTypes.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 44 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/docPropsVTypes, 1 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-math.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 92 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/math, 3 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/shared-relationshipReference.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 15 déclarations, espace http://purl.oclc.org/ooxml/officeDocument/relationships, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Strict/wml.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 425 déclarations, espace http://purl.oclc.org/ooxml/wordprocessingml/main, 6 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Opc/opc-contentTypes.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 8 déclarations, espace http://schemas.openxmlformats.org/package/2006/content-types, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Word/Schemas/Opc/opc-relationships.xsd

Preuve : Parsing natif intégral XmlSchema.Read : 5 déclarations, espace http://schemas.openxmlformats.org/package/2006/relationships, 0 imports ; octets comparés à l'archive officielle et à la ressource DLL Release, identiques ; ensemble compilé, 3 defaults Strict corrigés seulement en mémoire.

RAS


## src/OmniEurope.Documents/Excel/CellReference.cs

Preuve : Lecture intégrale références A1/plages et ExcelDate : bornes de parsing, références absolues, date1904 et correction 1900 suivies dans les méthodes.

RAS


## src/OmniEurope.Documents/Excel/XlsxStyle.cs

Preuve : Lecture intégrale enums/style immutable, égalité et normalisation RRGGBB/AARRGGBB ; propriétés de protection des formules documentées.

RAS


## src/OmniEurope.Documents/Excel/XlsxCell.cs

Preuve : Lecture intégrale conversions Value, erreurs non finies, dates/durées, FormatValue/NumberValue et séparation formule/résultat mémorisé.

RAS


## src/OmniEurope.Documents/Excel/XlsxWorkbook.cs

Preuve : Lecture intégrale chargement/sauvegarde, feuilles, recalcul et budgets ; pertes du modèle et nécessité de Recalculate explicites.

RAS


## src/OmniEurope.Documents/Excel/XlsxRecalculation.cs

Preuve : Lecture intégrale options et exception circulaire : budgets positifs, budget formule >= matrice, Now injectable et Unsupported contractuel.

RAS


## src/OmniEurope.Documents/Excel/XlsxChart.cs

Preuve : Lecture intégrale du modèle de graphique ancré : cellules 1-based, offsets/tailles points, données source internes conservées.

RAS


## src/OmniEurope.Documents/Excel/XlsxPackageWriter.cs

Preuve : Lecture intégrale : parties OPC, relations, chaînes partagées, styles, formules, protection QuotePrefix, vues et fusion, XML écrit via XmlWriter.

RAS


## src/OmniEurope.Documents/Excel/XlsxSheetReader.cs

Preuve : Lecture intégrale flux XML cellules/colonnes/volets/fusions : références, formules partagées, données typées, formats dates et durée suivis.

RAS


## src/OmniEurope.Documents/Excel/Editing/XlsxEditableSheet.cs

Preuve : Lecture intégrale GetValue/InlineText/Content/Inline/Guard/Normalize/AddCell ; sonde réelle éditeur->sauvegarde->modèle : _x0042_ devient B, preuve office-editor-escape-proof.json.

OFF-002 : absence encode/décode des escapes OOXML dans les textes édités.


## src/OmniEurope.Documents/Excel/Editing/XlsxEditor.cs

Preuve : Lecture intégrale OPC feuilles/relations/shared strings, ChangedParts, Save et CellChanged ; délégation de décodage des textes à InlineText examinée avec OFF-002.

OFF-002 : chemin SharedString affecté, constat porté par XlsxEditableSheet.cs.


## src/OmniEurope.Documents/Excel/XlsxWorksheet.cs

Preuve : Lecture intégrale références/plages, cellules, UsedRange, dimensions, volets, fusions, noms et estimation AutoFit ; comparaison des contrats modèle et éditeur.

RAS


## src/OmniEurope.Documents/Excel/XlsxStyleReader.cs

Preuve : Lecture intégrale StyleTables/ReadFont/ReadFill/ReadBorder/ReadXf/Children : sections de styles, références bornées, propriétés et RGB examinés.

RAS


## src/OmniEurope.Documents/Excel/XlsxStyleTable.cs

Preuve : Lecture intégrale déduplication formats/polices/fills/borders et XML xf/alignment ; écritures culture invariante et normalisation des couleurs suivies.

RAS


## src/OmniEurope.Documents/Excel/ExcelGapCodes.cs

Preuve : Lecture intégrale du catalogue ExcelDrawingUnreadable et de son partage explicite des autres codes avec ReadGapCodes.

RAS


## src/OmniEurope.Documents/Excel/XlsxCsvConverter.cs

Preuve : Lecture intégrale WriteCsv/ToCsv/FromCsv : budget avant écriture, culture affichage/nombres, conservation flux, titres et filtres suivis.

RAS


## src/OmniEurope.Documents/Excel/XlsxPackageReader.cs

Preuve : Lecture intégrale packages/relations/styles/shared/feuilles ; XLSX Strict de 3 Ko validé sur schémas officiels puis refus réel Load : aucune feuille, r:id namespace figé ligne99.

OFF-003 : classeur OOXML Strict valide refusé par lecteur et éditeur.


## src/OmniEurope.Documents/Excel/Formulas/FormulaCursor.cs

Preuve : Lecture intégrale du curseur : bornes Current/Next, déplacements, espaces, opérateurs et exceptions positionnées vérifiés avec le parser.

RAS


## src/OmniEurope.Documents/Excel/Formulas/FormulaLimits.cs

Preuve : Lecture intégrale limites profondeur/matrices/budget cumulatif et exceptions ; bornes par formule documentées, aucun test de surcharge effectué.

RAS


## src/OmniEurope.Documents/Excel/Formulas/FormulaNode.cs

Preuve : Lecture intégrale arbres/areas/flags WholeRows/Columns et PreOrder itératif ; calcul Depth et absence récursion traversée examinés.

RAS


## src/OmniEurope.Documents/Excel/Formulas/FormulaParser.cs

Preuve : Lecture intégrale Expression/Unary/Primary/Arguments/Array : précédence Excel, références, noms unsupported, profondeur et constantes bornées examinées.

RAS


## src/OmniEurope.Documents/Excel/Formulas/FormulaReferenceReader.cs

Preuve : Lecture intégrale coins/références/feuilles citées et plages entières ; curseur restauré si essai échoue, lignes/colonnes et flags bornés.

RAS


## src/OmniEurope.Documents/Excel/Formulas/FormulaEvaluator.cs

Preuve : Lecture intégrale évaluateur, chaîne gauche itérative, plages partielles, allocations budgétées, zip/broadcast, coercitions et comparaison type Excel.

RAS


## src/OmniEurope.Documents/Excel/Formulas/FormulaValue.cs

Preuve : Lecture intégrale valeurs/erreurs/tableaux partiels, expansion différée, conversions scalaire/nombre/texte/bool et export cellule.

RAS


## src/OmniEurope.Documents/Excel/Formulas/XlsxCalculator.cs

Preuve : Lecture intégrale recalcul : parse/unsupported, dépendances indexées, tri Kahn et cycles avant calcul ; résultats mémorisés et budgets préservés.

RAS


## src/OmniEurope.Documents/Excel/Formulas/FormulaFunctions.cs

Preuve : Lecture intégrale registre/helpers, coercitions critères et wildcards ; regex avec délai, fonctions paresseuses et références directes versus plages suivies.

RAS


## src/OmniEurope.Documents/Excel/Formulas/LookupFunctions.cs

Preuve : Lecture intégrale V/HLOOKUP, MATCH, INDEX, XLOOKUP, dimensions/indices et grilles partielles ; erreurs et modes de correspondance analysés.

RAS


## src/OmniEurope.Documents/Excel/Formulas/DateFunctions.cs

Preuve : Lecture intégrale DATE/TIME/TODAY/NOW/parties/WEEKDAY/mois/DATEDIF ; horloge injectée, systèmes1900/1904 et erreurs de domaine suivis.

RAS


## src/OmniEurope.Documents/Excel/Formulas/LogicalFunctions.cs

Preuve : Lecture intégrale registre et helpers logiques ; sonde réelle quatre cellules budget4 : SUM=4, IFERROR/IFNA Unsupported demandent8, opérande évalué deux fois lignes15-16.

OFF-004 : double évaluation du premier argument IFERROR/IFNA.


## src/OmniEurope.Documents/Excel/Formulas/MathFunctions.cs

Preuve : Lecture intégrale agrégats/critères/produit/arrondis/multiples ; CEILING/FLOOR(1E100,1) réels lèvent OverflowException et bloquent cellule indépendante, preuve JSON.

OFF-005 : conversion decimal non bornée dans CEILING/FLOOR arrête le recalcul.


## src/OmniEurope.Documents/Excel/Formulas/TextFunctions.cs

Preuve : Lecture intégrale des fonctions texte et bornes caractères/indices ; limite explicite : sonde REPT vide interrompue, aucun résultat complet ni exception revendiqués, office-probe-limits.md.

RAS retenu ; coût du cas REPT interrompu non confirmé, limite métrique explicite.


## src/OmniEurope.Documents/Excel/Formulas/FormulaShifter.cs

Preuve : Lecture intégrale déplacement références relatives/absolues, chaînes et noms cités, formes regex à délai et limites feuille contrôlées.

RAS


## src/OmniEurope.Documents/Excel/Formulas/SharedFormulas.cs

Preuve : Lecture intégrale groupes de formules partagées : mémorisation première formule et translation lignes/colonnes des cellules suivantes via FormulaShifter.

RAS


## src/OmniEurope.Documents/Excel/FormatTokens.cs

Preuve : Lecture intégrale split sections/tokenizer : citations/échappements/brackets, formats dates/elapsed/numériques et fractions de seconde examinés.

RAS


## src/OmniEurope.Documents/Excel/DateFormatter.cs

Preuve : Lecture intégrale dates/temps : arrondi avant contrôle plage, mois/minutes contextuels, AM/PM, elapsed et fractions de seconde, culture explicite.

RAS


## src/OmniEurope.Documents/Excel/FractionSection.cs

Preuve : Lecture intégrale FractionSection/FractionLayout : fractions continues et dénominateurs bornés, padding, fractions propres/impropres et formats reconnus.

RAS


## src/OmniEurope.Documents/Excel/NumberFormatter.cs

Preuve : Lecture intégrale en deux sorties : Format/Pick/General et NumberSection scaling/grouping/rounding/placeholders/exponents ; culture et section texte suivis.

RAS


## src/OmniEurope.Documents/Excel/XlsxChartReader.cs

Preuve : Lecture intégrale dessins/relations/placeholders/ancrages/CellSource : limites géométriques et points, caches versus références cellules et erreurs signalées analysés.

RAS


## src/OmniEurope.Documents/Charts/ChartReader.cs

Preuve : Lecture intégrale types/axes/series/points/titres : taille tenue versus déclarée, nombres finis, budgets série et matrice, fallback Unsupported vérifiés sur source.

RAS


## src/OmniEurope.Documents/Word/WordBlocks.cs

Preuve : Lecture intégrale blocs/tableaux/révisions/TextBuilder : texte visible exclut lignes et segments supprimés, helpers et contrôles de contenu transparents examinés.

RAS


## src/OmniEurope.Documents/Word/WordContentControlMark.cs

Preuve : Lecture intégrale identité de marque et propriétés tag/alias/id/kind/placeholder, création interne et contrat instance partagée entre blocs suivis.

RAS


## src/OmniEurope.Documents/Word/WordDocument.cs

Preuve : Lecture intégrale modèle, sections/notes/gaps/chargement/sauvegarde/helpers ; distinction modèle reconstruit versus éditeur conservateur explicitement vérifiée.

RAS


## src/OmniEurope.Documents/Word/WordInk.cs

Preuve : Lecture intégrale WordInkStroke/WordInk : points, couleur normalisée, opacité bornée et contrat InkML/fallback VML documentés.

RAS


## src/OmniEurope.Documents/Word/WordValues.cs

Preuve : Lecture intégrale tables correspondances formats/alignements/soulignements/tabulations/régions/lookbits et recherches de noms enum.

RAS


## src/OmniEurope.Documents/Word/WordInlines.cs

Preuve : Lecture intégrale texte/champs/liens/notes/révisions/formes/images : AppendText vide intentionnel pour marqueurs et formes, MIME et fallback documentés.

RAS


## src/OmniEurope.Documents/Word/WordProperties.cs

Preuve : Lecture intégrale relue séparément après sortie tronquée : enums, propriétés run/paragraphe et Overlay/MergeTabs, héritage nullable et origine explicite des fontes examinés.

RAS


## src/OmniEurope.Documents/Word/WordSection.cs

Preuve : Lecture intégrale sections/page setup/headers/notes/settings/core props ; unités points, héritage headers, modes compatibilité et alignement page documentés.

RAS


## src/OmniEurope.Documents/Word/WordTableProperties.cs

Preuve : Lecture intégrale largeurs/bordures/marges/règles lignes/cellules/régions/overlays ; Overflow interne réservé conversions, distinctions nullable/None explicites.

RAS


## src/OmniEurope.Documents/Word/WordXml.cs

Preuve : Lecture intégrale namespaces/types OPC/MIME, bool/int/mesures unités, conversions Twips/EMU et texte XML préservant espaces.

RAS


## src/OmniEurope.Documents/Word/WordStyles.cs

Preuve : Lecture intégrale styles/resolve/Chain/defaults : héritage borné32 avec cycles, overlay run/paragraphe/table, thème et styles prédéfinis examinés.

RAS


## src/OmniEurope.Documents/Word/WordNumbering.cs

Preuve : Lecture intégrale définitions/instances/formats/listcounter : neuf niveaux, starts/restarts, labels et clone des compteurs suivis ; aucune sonde de valeurs à coût excessif.

RAS


## src/OmniEurope.Documents/Word/Reading/WordDocumentReader.cs

Preuve : Lecture intégrale MainPart/Read/Theme/body/sections/headers/notes/comments : macros refusées et Word Strict explicitement hors périmètre, sans fetch externe.

RAS


## src/OmniEurope.Documents/Word/Reading/WordPartsReader.cs

Preuve : Lecture intégrale styles/numérotation/settings/thème/core : defaults absents, styles liés, formats de notes, compatibilité et dates invariantes suivis.

RAS


## src/OmniEurope.Documents/Word/Reading/ReadGapCodes.cs

Preuve : Lecture intégrale catalogue des codes image/charts/drawing/ink/theme et partage déclaré avec Excel ; codes stables nommés par ressources.

RAS


## src/OmniEurope.Documents/Word/Reading/WordPropertyReader.cs

Preuve : Lecture intégrale propriétés run/paragraphe, spacing/indentation/tabs/borders/shading : unités, null/héritage, onoff et tables de valeurs examinés.

RAS


## src/OmniEurope.Documents/Word/Reading/WordContentReader.cs

Preuve : Lecture intégrale par sorties complémentaires : context/adresses/gaps, blocs et controls, tables/révisions, run contents, champs empilés et AlternateContent examinés.

RAS


## src/OmniEurope.Documents/Word/Reading/WordDrawingReader.cs

Preuve : Lecture intégrale DrawingML/VML et helpers CSS : liens externes non chargés, gaps optionnels, ancrages/EMUs/alignement/wrap et cadres examinés.

RAS


## src/OmniEurope.Documents/Word/Reading/WordInkReader.cs

Preuve : Lecture intégrale InkML et fallback image : relations internes, XML protégé, erreurs signalées, dimensionnement et échelle des tracés suivis.

RAS


## src/OmniEurope.Documents/Word/Reading/InkMlReader.cs

Preuve : Lecture intégrale contextes/brushes/unités/traces et TraceDecoder/ChannelState : différences compactes, points et magnitude bornés, refus propres examinés.

RAS


## src/OmniEurope.Documents/Word/Reading/WordStructureReader.cs

Preuve : Lecture intégrale tables/lignes/cellules/pages : largeur pct/Twips, merges/lookbits, marges/colonnes/inégalité, notes et alignement vertical examinés.

RAS


## src/OmniEurope.Documents/Word/Writing/WordDocumentWriter.cs

Preuve : Lecture intégrale Body/References/auxiliary/Package : sections, identité headers, parties ressources, relations OPC et XML déterministe examinés.

RAS


## src/OmniEurope.Documents/Word/Writing/WordPropertyWriter.cs

Preuve : Lecture intégrale propriétés runs/paragraphe : ordre XSD, fonts/langues, tabs, espacements, bool nullable, bordures et shading comparés au reader.

RAS


## src/OmniEurope.Documents/Word/Writing/WordStructureWriter.cs

Preuve : Lecture intégrale propriétés tables/lignes/cellules/sections : merges, marges/lookbits, largeurs, pages/colonnes/notes et alignements, ordreXSD suivi.

RAS


## src/OmniEurope.Documents/Word/Writing/WordContentWriter.cs

Preuve : Lecture intégrale relations/blocs/conteneurs/runs/liens/champs/révisions/Clean ; wrappers et TextElement XML, terminaison paragraphes et unsupported légitimes suivis.

RAS


## src/OmniEurope.Documents/Word/Writing/WordDrawingWriter.cs

Preuve : Lecture intégrale context/dédup média/charts et formes : relations chart externes retirées, frames inline/anchor, positions/wrap/polygones et unitésEMU examinés.

RAS


## src/OmniEurope.Documents/Word/Writing/WordInkWriter.cs

Preuve : Lecture intégrale dessin Ink et InkML : définitions channels/résolutions/brushes, tracés entiers et références contextes, absence VML annoncée.

RAS


## src/OmniEurope.Documents/Word/Writing/WordPartsWriter.cs

Preuve : Lecture intégrale styles/numérotation/settings/notes/comments/core/namespaces : séparateurs, ordre propriétés, dates UTC invariantes et XML Clean suivis.

RAS


## src/OmniEurope.Documents/Word/Writing/WordThemeWriter.cs

Preuve : Lecture intégrale thème original copié avec Latin fonts mises à jour et thème par défaut avec formats XSD, aucune mutation du thème chargé.

RAS


## src/OmniEurope.Documents/Word/Editing/WordEditContext.cs

Preuve : Lecture intégrale contexte partagé : styles/numérotation chargés à la demande depuis relations internes et contexte de lecture commun.

RAS


## src/OmniEurope.Documents/Word/Editing/WordPackageParts.cs

Preuve : Lecture intégrale Release/IsTarget : ressources référencées conservées, retrait partie et descendants devenus orphelins, relations externes exclues.

RAS


## src/OmniEurope.Documents/Word/Editing/WordCoreEditor.cs

Preuve : Lecture intégrale mise à jour élémentaire métadonnées et dates, champs non modélisés conservés, création partie et relation OPC explicites.

RAS


## src/OmniEurope.Documents/Word/Editing/WordRevisionEditor.cs

Preuve : Lecture intégrale scan/apply révisions texte/styles/numbering ; fallback inclus dans application globale, identité des changements vérifiée avant application ciblée.

RAS


## src/OmniEurope.Documents/Word/Editing/WordEditor.cs

Preuve : Lecture intégrale façade publique : Open/Paragraphs/Stories/Replace/Controls/Revisions/Information/Append/Save/template ; contrats de conservation et save-as explicites.

RAS


## src/OmniEurope.Documents/Word/Editing/WordTrackedChange.cs

Preuve : Lecture intégrale enum/record publics : contenu, mise en forme, lignes/cellules et numérotation distingués, index instable après modification documenté.

RAS


## src/OmniEurope.Documents/Word/Editing/WordTrackedChangeScanner.cs

Preuve : Lecture intégrale ScanState/ranges/Kind/Describe/Text : ordre, exclusion fallback, regroupement moves et descriptions de changements examinés.

RAS


## src/OmniEurope.Documents/Word/Editing/WordTrackedChangeApplier.cs

Preuve : Lecture intégrale application contenu/marks/structures/merge/propriétés : accept/reject, préservation wrappers requis et ordre XSD examinés.

RAS


## src/OmniEurope.Documents/Word/Editing/WordMerger.cs

Preuve : Lecture intégrale fusion, copie parties/notes/styles/numérotation/bookmarks/drawings ; cible DOCX valide sans sectPr final provoque XNode.Remove sans parent, preuve JSON.

OFF-006 : Append échoue sur section finale facultative absente.


## src/OmniEurope.Documents/Word/Editing/WordContentControl.cs

Preuve : Lecture intégrale enums et records publics controls/date/items/gaps/fill : snapshots, unités résultats, ReasonDetail et contrat valeurs refusées inchangées examinés.

RAS


## src/OmniEurope.Documents/Word/Editing/ContentControlGapCodes.cs

Preuve : Lecture intégrale codes stables de refus type/date/liste/structure/contenu ; noms corrélés aux chemins des contrôles de contenu.

RAS


## src/OmniEurope.Documents/Word/Editing/WordContentControlFiller.cs

Preuve : Lecture intégrale fill/run/list/date/checkbox : sonde DOCX valide DropDownList vide accepte valeur arbitraire, Filled1 et zéro gap, condition items.Count>0 ligne83.

OFF-007 : liste fermée vide traitée comme texte libre.


## src/OmniEurope.Documents/Word/Editing/WordContentControlScanner.cs

Preuve : Lecture intégrale scan/read/mark/kinds/levels/dates/items/text/runs : snapshots et exclusion fallback/révisions/nested ownership suivis.

RAS


## src/OmniEurope.Documents/Word/Editing/WordContentControlText.cs

Preuve : Lecture intégrale Replace/Inline/Block/Template/SetFont : refus structure/contenu non textuel, conservation format premier run et retrait placeholder suivis.

RAS


## src/OmniEurope.Documents/Word/Editing/WordEditableParagraph.cs

Preuve : Lecture intégrale façade paragraphe/runs/propriétés/settext/setcontent/controls/drawings : snapshots, adresses, ressources et délégations aux éditeurs examinés.

RAS


## src/OmniEurope.Documents/Word/Editing/WordEditableRun.cs

Preuve : Lecture intégrale RunKind/EditableRun/ContentPiece/TextPiece/KeptRun : distinction champs/texte/contenus spéciaux et indices snapshots explicite.

RAS


## src/OmniEurope.Documents/Word/Editing/WordRunScanner.cs

Preuve : Lecture intégrale own runs/classification/fields/alternate/text : champs groupés par unité, références et breaks distingués, descendants de zones texte exclus.

RAS


## src/OmniEurope.Documents/Word/Editing/WordRunBuilder.cs

Preuve : Lecture intégrale Template/NewRun/RemoveIfEmpty/Insert : propriétés héritées puis overlay/langue, ordre XSD, tab/break et Clean des textes examinés.

RAS


## src/OmniEurope.Documents/Word/Editing/WordTextReplacer.cs

Preuve : Lecture intégrale Stretches/Replace/Apply/Segment ; sonde publique CurrentCulture sur a+soft-hyphen+b recherche ab => Xb et count1, longueur réelle non respectée.

OFF-008 : longueur de correspondance linguistique confondue avec search.Length.


## src/OmniEurope.Documents/Word/Editing/WordContentRewriter.cs

Preuve : Lecture intégrale validation pièces, signature/noop, unités gardées, marqueur transitoire et wrappers déplacés ; contenus non textuels conservés.

RAS


## src/OmniEurope.Documents/Word/Editing/WordParagraphLocation.cs

Preuve : Lecture intégrale enums/étapes/localisation, copie du path, égalité séquentielle et comparaison partnames insensible casse ; note/commentaire distincts.

RAS


## src/OmniEurope.Documents/Word/Editing/WordParagraphLocator.cs

Preuve : Lecture intégrale Locate/Steps/BlockStep/TableStep/InlineStep et cache index : wrappers transparents, notes/texteboîtes et indices locaux suivis.

RAS


## src/OmniEurope.Documents/Word/Editing/WordDrawingContent.cs

Preuve : Lecture intégrale contenu/placement : Ink points finis et boîte par défaut, PNG réellement décodé et refus explicites, données/marges en points.

RAS


## src/OmniEurope.Documents/Word/Editing/WordDrawingInfo.cs

Preuve : Lecture intégrale enum et snapshot dessin : tailles/offsets en points, références ancrage, wrap/layer et texte alternatif documentés.

RAS


## src/OmniEurope.Documents/Word/Editing/WordDrawingEditor.cs

Preuve : Lecture intégrale Insert/Replace/Remove/Build/Release : tailles finies, parties et relations créées, identité/layer dessins, ressources orphelines et namespaces examinés.

RAS


## src/OmniEurope.Documents/Word/Editing/WordDrawingScanner.cs

Preuve : Lecture intégrale Scan/Content/Drawing/Vml/KindOf et ScannedDrawing.Info : owned runs, snapshots, AlternateContent, tailles/offsets et descriptions suivis.

RAS
