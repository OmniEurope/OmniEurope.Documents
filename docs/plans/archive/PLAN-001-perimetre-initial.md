<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-001 : Périmètre initial, zéro dépendance

> Statut : **terminé** (2026-10-07). Décision du propriétaire du 2026-10-06 : aucune dépendance tierce dans
> le moteur (Word, Excel, PDF, Markdown, CSV, HTML, diff, rendu), tout est écrit dans ce dépôt. Le projet de
> tests peut utiliser xUnit (jamais livré).

## Objectif

Un paquet `OmniEurope.Documents` qui lit, écrit, convertit et rend les documents Word, Excel, PDF, Markdown,
CSV et HTML, en n'utilisant que la bibliothèque de base .NET. Seules données tierces
livrées : les polices Liberation, Carlito et Caladea (SIL OFL 1.1, licence jointe).

Chaque lot se termine par un commit sur `develop` quand la suite de tests est verte.

## Lot 0 : socle

- [x] Politique zéro dépendance dans le README, projet `tests/OmniEurope.Documents.Tests`
 .
- Contrôle : la suite de tests exécute au moins un test.

## Lot 1 : CSV

- [x] Lecteur en flux (synchrone et asynchrone), séparateur explicite ou détecté (tabulation, `|`, `;`, `,`),
  guillemets RFC 4180 ou désactivés (FEC), enregistrements sans limite de taille, accès par position et par
  nom d'en-tête insensible à la casse, lignes courtes tolérées.
- [x] Encodage : BOM UTF-8/16/32 détecté, Windows-1252 intégré (sans `CodePagesEncodingProvider`), UTF-8
  sans BOM détecté.
- [x] Analyse typée : décimaux fr-FR (espaces, U+00A0, U+202F) ou invariants, dates `yyyyMMdd`, durées.
- [x] Écrivain : guillemets RFC 4180, séparateur, BOM optionnel, neutralisation des formules (y compris
  après espace ou caractère de contrôle).
- Contrôle : tests sur un FEC tabulé UTF-8 avec BOM, un fichier `;` en Windows-1252, champs multilignes,
  un enregistrement de plus de 1 Mo.

## Lot 2 : Markdown

- [x] Analyseur CommonMark (blocs et en-ligne), rendu HTML avec HTML brut échappé par défaut, schémas
  d'URL filtrés (`javascript:` refusé), extensions GFM en option (tableaux, listes de tâches, barré,
  liens automatiques).
- [x] Écriture Markdown : échappement du texte utilisateur, titres, listes, tableaux.
- Contrôle : jeu d'exemples CommonMark écrits pour le dépôt, `<script>` jamais rendu, compatible trimming.

## Lot 3 : diff texte

- [x] Myers par ligne et par mot (séparateurs configurables), blocs (début et longueur de chaque côté),
  modèle en ligne Inserted / Deleted / Unchanged.
- Contrôle : tests de propriétés (appliquer le diff reconstruit B) sur des entrées aléatoires.

## Lot 4 : HTML

- [x] Analyseur HTML5 tolérant (document et fragment), DOM (TextContent, attributs, enfants, parent),
  sélecteurs simples (type, classe, id, attribut `=`, `*=`, `^=`, `$=`, descendant, enfant).
- [x] Nettoyeur à liste d'autorisations (balises, attributs, schémas, crochet d'URL, CSS supprimé).
- Contrôle : vecteurs XSS classiques neutralisés, fragments mal formés réparés.

## Lot 5 : Excel

- [x] Écrivain XLSX : feuilles nommées, chaînes, nombres, dates, booléens, styles (gras, taille, couleur,
  remplissage, format numérique), largeurs, ajustement automatique, volets figés, filtre automatique,
  préfixe anti-formule.
- [x] Lecteur XLSX : feuilles, plage utilisée, valeurs brutes et valeurs affichées selon le format numérique.
- [x] XLSX vers CSV, CSV vers XLSX (fichier produit ouvert sans réparation par un tableur externe, valeurs relues).
- Contrôle : aller-retour écriture puis lecture, formats numériques usuels vérifiés.

## Lot 6 : images

- [x] PNG (décodage tous types et profondeurs, entrelacement, encodage), JPEG (lecture d'en-tête,
  décodage baseline et progressif, encodage baseline), GIF (décodage), BMP/DIB, TIFF (non compressé,
  PackBits, LZW, Deflate, CCITT G3/G4).
- Contrôle : images de référence produites par des codecs externes : décodage sans perte identique au pixel près (PNG, GIF, BMP, TIFF dont CCITT G3/G4), JPEG proche du décodage de référence, JPEG produits par l'encodeur relus par un décodeur externe. Non couvert par un fichier de référence : JPEG CMJN/YCCK.

## Lot 7 : polices

- [x] Lecteur TrueType (cmap 0/4/6/12, hmtx, OS/2, hhea, post, name, glyf/loca, collections), sous-ensemble de police,
  polices OFL intégrées et correspondance des familles (Arial vers Liberation Sans, Calibri vers Carlito...).
- Contrôle : un sous-ensemble se relit et garde les glyphes demandés (chargé aussi par un moteur de polices
  externe) ; couverture grec, cyrillique et latin étendu des 24 langues de l'UE. Constat : Caladea n'a ni grec,
  ni cyrillique, ni Ţ ; `ResolveForCharacter` passe alors à Liberation Serif.

## Lot 8 : écriture PDF

- [x] Objets, flux compressés, polices TrueType embarquées en Identity-H avec ToUnicode, texte positionné,
  mesure du texte, couleurs, traits, rectangles, polygones, images PNG (alpha) et JPEG, métadonnées.
- [x] Mise en page simple : retour à la ligne par largeur mesurée, sauts de page, tableaux (rendu vérifié par un lecteur PDF externe).
- Contrôle : le texte écrit se relit par le lecteur du lot 9, sortie déterministe.

## Lot 9 : lecture PDF

- [x] Analyseur (xref classique et en flux, flux d'objets, mises à jour incrémentales, réparation d'une
  xref cassée), filtres (Flate, LZW, ASCIIHex, ASCII85, RunLength, prédicteurs), chiffrement standard
  (RC4, AES-128/256) avec mot de passe vide, métadonnées, nombre et taille des pages.
- [x] Extraction de texte : lettres avec valeur, taille, police et boîte, encodages simples, CMap
  ToUnicode, polices Type0 ; extraction des images en PNG.
- Contrôle : relecture des PDF du lot 8 et d'un PDF imprimé par un navigateur (xref en flux). Non vérifié sur un
  fichier externe : le déchiffrement (seul RC4 est testé sur son vecteur de référence).

## Lot 10 : opérations PDF

- [x] Fusionner, découper (pages, plages), réordonner, supprimer des pages, tamponner du texte sur chaque
  page (mise à jour incrémentale), compresser (flux recompressés, objets inutilisés retirés).
- Contrôle : un test par opération, nombre de pages et texte vérifiés après relecture.

## Lot 11 : analyse de mise en page PDF

- [x] Mots par voisinage, blocs, ordre de lecture multi-colonnes, en-têtes et pieds répétés, PDF scanné
  détecté.
- Contrôle : PDF à deux colonnes générés, ordre de lecture vérifié.

## Lot 12 : Word

- [x] Modèle de lecture DOCX (styles, numérotation, sections, en-têtes et pieds, notes, tableaux, champs,
  images, révisions), ouverture sûre (bombe ZIP, macros refusées).
- [x] Écrivain DOCX (styles, paragraphes, tables, sections, notes, pieds avec champs, images, `w:lang`).
- [x] Édition en place (seules les parties touchées sont réécrites), fusion de documents.
- Contrôle : aller-retour sans modification identique au XML près ; documents produits relus.
  Fait : ouverture puis enregistrement sans modification identiques à l'octet près par partie (fichier externe
  et fichier produit) ; remplacement à travers plusieurs runs et réécriture de paragraphe ne touchent que la
  partie concernée. Lecture d'un DOCX produit par un convertisseur externe ; DOCX écrit,
  édité et fusionné relus par ce convertisseur (vers texte : titres, listes, tableau fusionné, note,
  section paysage). Non modélisé (signalé dans `WordDocument.Gaps`) : graphiques, SmartArt, groupes de formes,
  contenu importé (`altChunk`), équations (lues en texte brut). Les propriétés bascule (gras, italique) sont
  surchargées par couche de style, pas basculées.

## Lot 13 : Word vers PDF

- [x] Moteur de mise en page : styles, numérotation, coupure de ligne, tabulations, justification,
  pagination (veuves et orphelines, paragraphes liés, sections, colonnes équilibrées), tableaux, en-têtes et
  pieds, champs de page, notes de bas de page et de fin, images, EMF ; rapport des écarts.
  - [x] 13.1 Lignes : styles résolus, polices, coupure gloutonne, tabulations (points de suite, droite,
    centre, décimale), justification, soulignements, exposants, surlignage, libellés de liste.
  - [x] 13.2 Pagination : sections, sauts, lignes liées, veuves et orphelines, paragraphe lié au suivant,
    colonnes (équilibrées avant une section continue), en-têtes et pieds (première page, paires), champs
    PAGE, NUMPAGES, SECTIONPAGES.
  - [x] 13.3 Tableaux : grille, fusions horizontales et verticales, bordures, trames, marges de cellule,
    lignes d'en-tête répétées, coupure de ligne de tableau, mises en forme conditionnelles du style.
  - [x] 13.4 Notes de bas de page et de fin, images en ligne et flottantes, zones de texte, liens, signets.
  - [x] 13.5 EMF (tracés, remplissages, texte, images) ; rapport des écarts.
- Contrôle : tests de mise en page ciblés, texte du DOCX retrouvé dans le PDF.
  Fait : tests sur des pages calibrées (veuves, lignes liées, notes en bas de la bonne page, en-têtes de
  tableau répétés, ligne de tableau coupée avec tableau imbriqué, colonnes équilibrées, justification au
  millième, tabulations droite, centrée, décimale, positionnelle et pendante), texte du DOCX externe retrouvé
  dans le PDF, EMF dessiné en vectoriel ; rendu vérifié visuellement par un lecteur PDF externe. Écarts
  signalés dans `WordPdfResult.Gaps` : police inconnue remplacée, texte qui ne contourne pas les objets
  flottants, droite-à-gauche, colonnes inégales, redémarrage de numérotation des notes, contenu plus haut
  qu'une page, formats d'image non dessinables, texte EMF pivoté. Non interprété : régions de découpe, opérations
  raster et enregistrements EMF+ des métafichiers (le repli GDI est dessiné).

## Lot 14 : conversions

- [x] Markdown vers HTML, Word et PDF ; HTML vers Word et PDF ; Excel vers PDF ; images vers PDF.
- Contrôle : un test par conversion, texte source retrouvé dans la sortie.
  Fait : HTML vers Word (titres, listes imbriquées avec départ, tableaux avec fusions de lignes et de
  colonnes, citations, code, règles, liens, styles en ligne ; images `data:` seulement, rien n'est téléchargé),
  Markdown par le rendu HTML sûr (HTML brut échappé), classeur vers un tableau par feuille (valeurs affichées
  selon la culture choisie, fusions, lignes figées répétées, paysage si large), images vers une page chacune
  (taille naturelle ou ajustées sur A4). Rendus Markdown et Excel vérifiés visuellement.

## Lot 15 : rendu PDF en image

- [x] Rastériseur : chemins (remplissage pair-impair et non nul, traits), anticrénelage, images, polices
  TrueType, CFF et Type 1, couleurs RGB, gris, CMJN, sortie PNG à la résolution demandée.
  - [x] 15.1 Cœur : surface, remplissage anticrénelé (non nul, pair-impair), traits (épaisseur, extrémités,
    jointures, pointillés), découpe, opacité, opérateurs de tracé, images (masques, masque doux), espaces de
    couleur, rotation et zone de recadrage, PNG.
  - [x] 15.2 Texte : TrueType embarqué (simple et CID avec CIDToGIDMap), polices non embarquées par les
    polices fournies, Type 3, modes de rendu.
  - [x] 15.3 CFF (Type1C, CIDFontType0C, OpenType CFF) et Type 1 (déchiffrement eexec, charstrings).
  - [x] 15.4 Apparences d'annotations, dégradés axiaux et radiaux, rapport des écarts, comparaison à un lecteur PDF
    externe.
- Contrôle : pages générées par le lot 8 rendues, pixels attendus vérifiés.
  Fait : tests au pixel (règles de remplissage, anticrénelage, traits, jointures, pointillés, découpe,
  opacité, images, masques, texte TrueType, CFF simple et CID, Type 1 chiffré, Type 3, police non embarquée,
  modes de rendu, rotation, annotations, dégradés). Comparaison à un lecteur PDF externe à 144 dpi (sondes
  locales, non versionnées) : écart moyen de 0,08 à 2,3 niveaux sur 255 sur un PDF de navigateur, nos PDF Word,
  Markdown et Excel, une police CFF brute embarquée et des dégradés CSS. Écarts signalés : motifs en mosaïque,
  dégradés autres qu'axiaux et radiaux, masques doux, modes de fusion, images JPEG 2000 et JBIG2.

## Lot 16 : publication

- [x] Paquet NuGet 0.1.0, couverture et contrôle CRAP verts.
- Contrôle : `dotnet pack` produit le paquet, `CRAP gate passed`.
- [x] 16.1 Violations CRAP à couverture nulle (chiffrement PDF, prédicteurs, RLE BMP, JPEG progressif,
  glyphes cyrilliques) : tests ajoutés. Contrôle : ces méthodes absentes du rapport.
  Fait : plus aucune méthode à couverture nulle au-dessus du seuil (les décomptes intermédiaires 84 puis 57,
  lus dans la sortie texte du lanceur, étaient inexacts ; seul le rapport CSV du contrôle fait foi). Fixtures validées par des lecteurs externes : PDF chiffrés
  RC4 40/128 et AES-128 (mots de passe utilisateur et propriétaire, mauvais mot de passe refusé ; le lecteur
  externe refuse tout PDF AES-256, testé seulement contre notre propre chiffreur écrit d'après ISO 32000-2), BMP
  RLE8/RLE4 et TIFF à prédicteur (8 et 16 bits, deux ordres d'octets) décodés à l'identique par un décodeur externe, JPEG
  à approximations successives produit sans perte par un transcodeur externe (pixels identiques au JPEG de base).
- [x] 16.2 Violations CRAP de complexité supérieure à 30 : méthodes découpées ou tabulées. Contrôle :
  `CRAP gate passed`.
  Fait : `CRAP gate passed` (2 300 méthodes, score maximal 30, aucune exception justifiée), 628 tests.
  Opérateurs de texte, filtres, xref, CMap et décodeur d'image PDF tabulés ou découpés ; interpréteurs
  Type 1 et Type 2 couverts par un assembleur de charstrings écrit d'après les spécifications (chaque
  opérateur, flex, sous-routines, seac, emballages PFB, hexadécimal et `lenIV -1`). Défauts trouvés en
  route : encres inversées des JPEG Adobe YCCK (comparées à un décodeur externe) et plantage sur les noms de glyphes
  `uD800`-`uDFFF`.
  `dotnet pack` : `OmniEurope.Documents.0.1.0.nupkg` sans dépendance.
