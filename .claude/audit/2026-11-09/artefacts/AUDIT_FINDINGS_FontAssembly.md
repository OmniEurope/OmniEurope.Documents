# Revue du module FontAssembly

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

Revue Full terminée : 39/39 fichiers, RAS. Les deux sources/projet, le NOTICE et les sept licences ont été lus intégralement ; les 29 fontes ont été parsées avec le moteur Release, tous glyphes/contours, correspondances cmap, noms, styles, métriques et autorisations d'incorporation inspectés. Chaque ressource DLL correspond exactement au fichier inventorié ; les 29 empreintes du NOTICE et les sept empreintes de licences concordent (`metrics/font-data-native-proof.json`, `font-licence-native-proof.json`). Cette inspection utilise les données approuvées et leur provenance épinglée ; aucun nouvel audit distant des archives d'origine n'a été effectué.

Le contrat réel reste deux assemblages dans un NuGet. Les substitutions, l'ordre des styles, les replis Noto et l'exposition des licences ont été confrontés aux preuves de package et de consommateur fournies par l'orchestrateur. Qualité, conventions, architecture, sécurité, performance, fiabilité et contrats publics examinés ; aucune valeur de couverture/complexité inventée. Aucun changement de source, test, version, dépendance ou publication.

Proportionnalité : un assemblage facultatif évite d'embarquer les fontes supplémentaires dans chaque chargement initial ; le regroupement dans le même package et une seule API d'enregistrement répondent au contrat, sans couche superflue observée.

## src/OmniEurope.Documents.Fonts/OmniEurope.Documents.Fonts.csproj

Preuve : Lecture intégrale : projet non packable, référence unique au coeur, ressources TTF/licences/NOTICE embarquées et analyse trim/AOT activée.

RAS


## src/OmniEurope.Documents.Fonts/DocumentFonts.cs

Preuve : Lecture intégrale : 29 faces Lazy, ordre Regular avant variantes, substitutions annoncées avec écarts, familles/licences et repli Noto confrontés au consommateur réel.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NOTICE.txt

Preuve : Lecture intégrale : sept familles OFL, versions et origines épinglées, 29 empreintes TTF et sept licences ; Selawik renommage seul annoncé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Gelasio-Bold.ttf

Preuve : Inspection native intégrale de la fonte Gelasio Bold : 901 glyphes parcourus, 761 correspondances cmap, 1916 contours/52886 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Gelasio-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Gelasio Bold Italic : 900 glyphes parcourus, 765 correspondances cmap, 1916 contours/54637 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Gelasio-Italic.ttf

Preuve : Inspection native intégrale de la fonte Gelasio Italic : 900 glyphes parcourus, 765 correspondances cmap, 1916 contours/54542 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Gelasio-Regular.ttf

Preuve : Inspection native intégrale de la fonte Gelasio Regular : 901 glyphes parcourus, 761 correspondances cmap, 1915 contours/53158 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Lato-Bold.ttf

Preuve : Inspection native intégrale de la fonte Lato Bold : 277 glyphes parcourus, 263 correspondances cmap, 492 contours/10675 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Lato-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Lato Bold Italic : 277 glyphes parcourus, 263 correspondances cmap, 492 contours/10922 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Lato-Italic.ttf

Preuve : Inspection native intégrale de la fonte Lato Italic : 277 glyphes parcourus, 263 correspondances cmap, 492 contours/10824 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Lato-Regular.ttf

Preuve : Inspection native intégrale de la fonte Lato Regular : 277 glyphes parcourus, 263 correspondances cmap, 492 contours/10578 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSans-Bold.ttf

Preuve : Inspection native intégrale de la fonte Noto Sans Bold : 4702 glyphes parcourus, 3053 correspondances cmap, 9516 contours/170822 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSans-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Noto Sans Bold Italic : 3762 glyphes parcourus, 2842 correspondances cmap, 7571 contours/128162 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSans-Italic.ttf

Preuve : Inspection native intégrale de la fonte Noto Sans Italic : 3762 glyphes parcourus, 2842 correspondances cmap, 7577 contours/129906 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSans-Regular.ttf

Preuve : Inspection native intégrale de la fonte Noto Sans Regular : 4702 glyphes parcourus, 3053 correspondances cmap, 9535 contours/171141 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSerif-Bold.ttf

Preuve : Inspection native intégrale de la fonte Noto Serif Bold : 2414 glyphes parcourus, 2192 correspondances cmap, 5113 contours/120035 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSerif-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Noto Serif Bold Italic : 2414 glyphes parcourus, 2192 correspondances cmap, 4989 contours/130219 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSerif-Italic.ttf

Preuve : Inspection native intégrale de la fonte Noto Serif Italic : 2414 glyphes parcourus, 2192 correspondances cmap, 4981 contours/99218 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/NotoSerif-Regular.ttf

Preuve : Inspection native intégrale de la fonte Noto Serif : 2414 glyphes parcourus, 2192 correspondances cmap, 5006 contours/119561 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/OpenSans-Bold.ttf

Preuve : Inspection native intégrale de la fonte Open Sans Bold : 1150 glyphes parcourus, 1010 correspondances cmap, 2275 contours/37023 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/OpenSans-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Open Sans Bold Italic : 1162 glyphes parcourus, 1010 correspondances cmap, 2298 contours/40089 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/OpenSans-Italic.ttf

Preuve : Inspection native intégrale de la fonte Open Sans Italic : 1162 glyphes parcourus, 1010 correspondances cmap, 2298 contours/40300 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/OpenSans-Regular.ttf

Preuve : Inspection native intégrale de la fonte Open Sans Regular : 1150 glyphes parcourus, 1010 correspondances cmap, 2275 contours/36699 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Roboto-Bold.ttf

Preuve : Inspection native intégrale de la fonte Roboto Bold : 1326 glyphes parcourus, 927 correspondances cmap, 3635 contours/46024 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Roboto-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Roboto Bold Italic : 1326 glyphes parcourus, 927 correspondances cmap, 3635 contours/46024 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Roboto-Italic.ttf

Preuve : Inspection native intégrale de la fonte Roboto Italic : 1326 glyphes parcourus, 927 correspondances cmap, 3635 contours/46024 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Roboto-Regular.ttf

Preuve : Inspection native intégrale de la fonte Roboto : 1326 glyphes parcourus, 927 correspondances cmap, 3635 contours/46024 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Selawik-Bold.ttf

Preuve : Inspection native intégrale de la fonte Selawik Bold : 352 glyphes parcourus, 348 correspondances cmap, 663 contours/9524 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Selawik-Light.ttf

Preuve : Inspection native intégrale de la fonte Selawik Light : 352 glyphes parcourus, 348 correspondances cmap, 667 contours/9679 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Selawik-Regular.ttf

Preuve : Inspection native intégrale de la fonte Selawik : 352 glyphes parcourus, 348 correspondances cmap, 667 contours/9679 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Selawik-Semibold.ttf

Preuve : Inspection native intégrale de la fonte Selawik Semibold : 352 glyphes parcourus, 348 correspondances cmap, 663 contours/9612 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/Selawik-Semilight.ttf

Preuve : Inspection native intégrale de la fonte Selawik Semilight : 352 glyphes parcourus, 348 correspondances cmap, 665 contours/9652 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées. Empreinte conforme au NOTICE épinglé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/LICENSE-Gelasio.txt

Preuve : Lecture intégrale : copyright Gelasio 2022, cinq conditions OFL 1.1 et clause documents ; ressource livrée avec toutes les faces.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/LICENSE-Lato.txt

Preuve : Lecture intégrale : copyright tyPoland 2010..2014 et nom Lato réservé, OFL 1.1 complet autorisant incorporation et redistribution accompagnée.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/LICENSE-NotoSans.txt

Preuve : Lecture intégrale : Google LLC 2015..2021, OFL 1.1 autorisant revente avec logiciel et incorporation, notice présente au pack.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/LICENSE-NotoSerif.txt

Preuve : Lecture intégrale : Google 2012, conditions OFL 1.1 complètes et exemption des documents ; licence distincte des données et de EUPL.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/LICENSE-OpenSans.txt

Preuve : Lecture intégrale : copyright Open Sans 2020, OFL 1.1 complet autorisant incorporation et redistribution groupée avec copyright conservé.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/LICENSE-Roboto.txt

Preuve : Lecture intégrale : copyright Roboto 2011, texte OFL 1.1 complet et lien openfontlicense, ressource séparée de licence EUPL du code.

RAS


## src/OmniEurope.Documents.Fonts/Fonts/LICENSE-Selawik.txt

Preuve : Lecture intégrale : copyright Microsoft 2015, nom réservé et marque Selawik, OFL 1.1 complet ; renommage des fichiers seul annoncé.

RAS
