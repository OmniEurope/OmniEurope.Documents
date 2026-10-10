<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-004 : Fonctionnalités suivantes

> Statut : **en cours** (2026-10-10 : lot 3 formulaires PDF, lot 8 caviardage PDF, lot 7 chiffrement PDF AES-256, lot 6 révisions, table des matières et champs Word, lot 5 Word vers Markdown, lots 9 Excel vers HTML et 12 fractions Excel faits ; 2026-10-08 : lot 1 édition Excel sans perte fait ; 2026-10-07 : Word vers HTML). Feuille de route proposée au
> propriétaire ; l'ordre des lots est celui de la valeur attendue. Un lot ne commence qu'après accord du
> propriétaire.

## Objectif

Combler les manques constatés dans le code : modifier un classeur sans perte, calculer les formules,
remplir des formulaires PDF, produire des PDF d'archivage, puis élargir les conversions et la fidélité du
rendu. La règle zéro dépendance de `src/` reste en vigueur pour chaque lot.

Règles communes :

- Chaque fonctionnalité est écrite d'après la spécification du format (ECMA-376, ISO 32000, ISO 19005) et
  vérifiée par des tests dont l'attendu est établi indépendamment du code testé.
- Un parcours de bout en bout est ajouté à la suite rapide ; les cas volumineux ou par milliers vont dans
  la suite longue.
- Ce que le paquet ne sait pas faire reste signalé (`Gaps`, exception documentée), jamais ignoré en silence.

Chaque lot se termine par un commit sur `develop` quand les deux suites et le contrôle CRAP sont verts.

## Lot 1 : modifier un classeur sans perte

- [x] Éditeur XLSX qui ne réécrit que les feuilles et cellules modifiées : graphiques, images, mises en
  forme conditionnelles, validations, commentaires et tableaux croisés restent intacts (aujourd'hui,
  enregistrer un classeur chargé les supprime).
- Contrôle : un classeur riche ouvert, modifié puis enregistré garde toutes ses parties non touchées
  identiques à l'octet ; les cellules modifiées se relisent.
  Fait (2026-10-08) : `XlsxEditor` et `XlsxEditableSheet`, 8 tests sur un classeur écrit à la main (graphique,
  image, mises en forme conditionnelles, validations, commentaires, tableau, cache de tableau croisé, chaîne
  de calcul). Une modification de cellule ne réécrit que sa feuille et `xl/workbook.xml` (recalcul demandé à
  l'ouverture) ; la chaîne de calcul est retirée quand une formule change. Refusés : tête de formule partagée,
  cellule de formule matricielle, en-tête de tableau.

## Lot 2 : calcul des formules

- [x] Moteur de formules : opérateurs, références et plages entre feuilles, fonctions courantes
  (mathématiques, logiques, recherche, texte, dates), erreurs (`#DIV/0!`, `#REF!`, `#N/A`...), références
  circulaires refusées ; recalcul à la demande avant export PDF ou CSV.
  - Fait (2026-10-10) : `XlsxWorkbook.Recalculate`, 101 fonctions, opérateurs avec la précédence d'Excel,
    plages et constantes matricielles calculées élément par élément, références absolues, lignes et colonnes
    entières, autres feuilles ; ordre de dépendance sans récursion (chaîne de 100 000 cellules), cycles refusés
    (`XlsxCircularReferenceException`) ; formules partagées relues cellule par cellule. 326 tests aux résultats
    calculés à la main, une facture remplie relue en PDF et en CSV. Non calculés, gardent leur résultat stocké
    et sont listés dans `Unsupported` : noms définis, références de tableau, références sur plusieurs feuilles
    (`Jan:Mar!A1`), opérateurs d'union et d'intersection, `INDIRECT`, `OFFSET`, toute fonction hors liste ;
    une formule matricielle ne remplit que sa propre cellule (premier élément).
- Contrôle : résultats attendus calculés à la main pour chaque fonction ; un modèle rempli donne ses totaux
  justes en PDF et en CSV.

## Lot 3 : formulaires PDF

- [x] Lecture des champs (texte, cases, boutons radio, listes), remplissage, génération des apparences et
  aplatissement.
  - Fait (2026-10-10) : `PdfForm.Read`, `PdfForm.Fill`, `PdfForm.Flatten` (ISO 32000-1 §12.7). Arbre des champs avec
    attributs hérités, nom complet, sorte, valeur, options, états, drapeaux, longueur maximale, widgets (page,
    rectangle, état actif, texte de l'apparence courante). Remplissage en mise à jour incrémentale (octets d'origine
    gardés) : `V`, `AS`, `I`, apparence normale neuve par widget (fond et bordure `MK`, valeur entre `/Tx BMC` et
    `EMC`, police, taille automatique et couleur de `DA`, sous-ensembles de polices embarqués : un visualiseur qui ne
    régénère pas les apparences montre la valeur Unicode) ; une ligne alignée par `Q`, plusieurs lignes coupées et
    réduites, cases de peigne, mot de passe masqué, liste depuis `TI` avec sélection surlignée, coche ou point dessinés
    pour un bouton sans apparence. Valeurs contrôlées (champ inconnu, bouton poussoir, signature, état ou choix absent,
    trop de choix, texte plus long que `MaxLen`), `XFA` retiré. Aplatissement : apparence de chaque widget non masqué
    dessinée sur la page, widgets et formulaire retirés. 16 tests sur un formulaire écrit à la main (neuf champs, noms
    hiérarchiques, widgets fusionnés ou enfants) : valeurs relues, texte des apparences extrait égal à la valeur
    (accents et `€`, plusieurs lignes, peigne), rendu par `PdfRenderer` (texte bleu dans la zone du champ, case cochée,
    coche dessinée), après aplatissement texte sur la page, aucun widget ni `AcroForm`. Suite rapide 1977 tests,
    couverture des lignes 97,14 %.
  - Limites : rotation de widget (`MK R`), styles de bordure (biseau, incrusté, tirets) et défilement d'un texte plus
    long que sa zone (coupé) non rendus ; `NeedAppearances` laissé tel quel ; seuls les champs remplis reçoivent une
    apparence neuve ; l'aplatissement est une mise à jour incrémentale (la révision précédente garde le formulaire dans
    les octets) ; fichiers chiffrés ou endommagés et objets directs refusés ; actions et calculs JavaScript ignorés.
- Contrôle : un formulaire rempli se relit avec ses valeurs ; après aplatissement, le texte est sur la page
  et le formulaire a disparu.

## Lot 4 : PDF/A

- [ ] Production de PDF/A-2b (et A-2u) depuis l'écrivain et les conversions : polices embarquées, profil de
  couleur de sortie, métadonnées XMP, aucune fonction interdite (chiffrement, JavaScript, transparence non
  conforme).
- Contrôle : chaque règle de la norme vérifiée par un test sur les fichiers produits.

## Lot 5 : Word vers HTML et Markdown

- [x] Word vers HTML (2026-10-07) : `WordToHtml.Convert` donne une page autonome (styles embarqués, rien
  n'est chargé) : sections à leur largeur de page et à leurs marges, en-tête et pied de page par défaut de
  la première section, titres, paragraphes et runs avec leur mise en forme résolue, listes avec leurs
  libellés calculés, tableaux avec fusions horizontales et verticales, notes de bas de page puis de fin
  reliées dans les deux sens, images en `data:` (TIFF et EMF convertis en PNG), zones de texte en blocs,
  liens `http`, `https` et `mailto` seulement, révisions acceptées ou marquées. Chaque paragraphe lu
  d'un paquet porte `data-address`, l'adresse que lui donne `WordEditor` (`WordParagraph.SourceAddress`),
  et `HighlightAddress` le met en évidence. Ce qui n'est qu'approché est listé dans `Gaps`.
- [x] Word vers Markdown (2026-10-10) : `WordToMarkdown.Convert` donne du Markdown GitHub : titres (niveaux de plan
  1 à 6, libellé des titres numérotés gardé, gras ou italique du style non marqués), paragraphes, retours à la ligne
  forcés, gras, italique et barré (espaces hors des marqueurs), listes à puces et numérotées imbriquées (chaque
  élément indenté sous le contenu de son parent, numéros comptés depuis le départ du niveau), tableaux GFM
  (première ligne en en-tête, fusions étalées en cellules vides), liens `http`, `https` et `mailto`, notes de bas de
  page et de fin en notes `[^1]` et `[^e1]` définies à la fin, images en références (`![alt][image1]`, octets et
  chemins rendus dans `Images`), zones de texte après leur paragraphe ; révisions acceptées, texte masqué omis, texte
  échappé. 12 tests dans `WordToMarkdownTests`, dont un aller-retour Word, Markdown, Word par la conversion Markdown
  du paquet (mêmes textes, niveaux de titre, profondeurs de liste, cellules et passages en emphase) et un relu
  littéral des textes qui ressemblent à de la syntaxe. Non pris en charge, listé dans `Gaps` : souligné, exposant,
  indice et autres mises en forme, en-têtes, pieds de page, commentaires, sauts de page et de colonne, paragraphes
  vides, signets et autres schémas de lien, numéros en lettres ou en chiffres romains (écrits en nombres), plusieurs
  paragraphes, listes ou tableaux imbriqués dans une cellule ou une note (joints sur une ligne). Limite connue : un
  marqueur d'emphase placé entre une ponctuation et une lettre peut ne pas être lu comme emphase.
- Contrôle : aller-retour Word, HTML, Word sans perte de texte ni de structure (fait pour le HTML :
  `WordToHtmlTests`, adresses vérifiées contre `WordEditor` dans le corps, les cellules, les en-têtes, les
  pieds de page, les notes et les zones de texte, document d'un autre producteur compris ; pour le Markdown :
  aller-retour Word, Markdown, Word dans `WordToMarkdownTests`).

## Lot 6 : révisions, table des matières et champs Word

- [x] Accepter ou refuser les révisions (toutes ou une par une), mettre à jour la table des matières et les
  champs simples (pages, dates, références) à partir de la mise en page.
  - Fait (2026-10-10) : `WordEditor.TrackedChanges`, `AcceptAllChanges`, `RejectAllChanges`, `AcceptChange`,
    `RejectChange` (ECMA-376 Part 1 §17.13.5) : insertions, suppressions, déplacements nommés (une seule révision
    pour la source, la destination et les bornes), marques de paragraphe insérées ou supprimées (le paragraphe
    rejoint le suivant), changements de mise en forme de caractère, de paragraphe, de section, de tableau, de
    ligne, de cellule et de grille, lignes et cellules insérées ou supprimées, fusion de cellules, numérotation
    suivie ; corps, en-têtes, pieds, notes, commentaires, styles et numérotation. `WordFieldUpdater.Update`
    reconstruit les `TOC` (`\o`, `\u`, `\t`, `\h`, `\n`) avec signets `_Toc` et calcule `PAGE`, `NUMPAGES`,
    `SECTIONPAGES`, `PAGEREF`, `REF`, `DATE`, `TIME`, `CREATEDATE`, `SAVEDATE` depuis la mise en page de
    `WordToPdf`, refaite jusqu'à stabilité (quatre fois au plus). 15 tests : chaque type de révision accepté puis
    refusé, texte relu attendu écrit à la main et paquet validé par `WordSchemaValidator` ; table des matières
    dont chaque numéro de page est vérifié sur la page du PDF produit ; formats de numéros de section, images de
    date en français, champs non gérés listés. Suite rapide 1930 tests, couverture des lignes 97,29 %.
  - Limites : révisions des propriétés de contrôle mathématiques et plages XML personnalisées non traitées ;
    retirer une cellule n'ajuste pas la grille ; une révision acceptée seule ne touche pas sa copie de repli
    (`mc:Fallback`) ; l'étiquette de liste d'une entrée est suivie d'une espace (Word met une tabulation) ;
    `TOC` avec `\a`, `\b`, `\c`, `\s`, `\d` laissé tel quel, entrées `TC` (`\f`, `\l`) non collectées, `\o` et `\u`
    lisent tous deux le niveau hiérarchique résolu ; `REF \n \r \w`, `PAGEREF \p`, champs des zones de texte et
    autres champs laissés tels quels et listés dans `Gaps` ; les champs de page des en-têtes et pieds gardent leur
    résultat (chaque page dessine le sien) ; les numéros sont ceux de la mise en page du paquet.
- Contrôle : après acceptation, le texte relu est celui attendu ; les numéros de page de la table des
  matières correspondent au PDF produit.

## Lot 7 : protection PDF à l'écriture

- [x] Chiffrement AES-256 avec mots de passe utilisateur et propriétaire, et permissions.
  - Fait (2026-10-10) : `PdfDocumentBuilder.Encryption`, `PdfEditor.Encrypt` (`PdfEncryption`, `PdfPermissions`),
    gestionnaire standard ISO 32000-2 V 5 R 6 AESV3, fichier en PDF 2.0 ; mots de passe préparés par SASLprep
    (RFC 4013) et coupés à 127 octets UTF-8 ; U, UE, O, OE, Perms selon les algorithmes 2.B, 8, 9 et 10 ; clé,
    sels et vecteurs d'initialisation tirés de `RandomNumberGenerator`, seulement AES et SHA-2 de la bibliothèque
    de base. Le lecteur essaie d'abord le mot de passe propriétaire, retombe sur l'UTF-8 brut, vérifie Perms
    contre P et donne `Permissions` et `OpenedAsOwner`. 23 tests : relecture avec chaque mot de passe, mauvais
    mot de passe et absence refusés, permissions relues, P altéré détecté, vecteurs connus de U, UE, O, OE et Perms
    auto-dérivés (aucun vecteur publié de révision 6 disponible hors ligne : calculés une fois par
    l'implémentation indépendante des tests et figés), exemples de la RFC 4013 §3. Suite rapide 1953 tests,
    couverture des lignes 97,28 %.
  - Limites : normalisation NFKC à la version Unicode du runtime (pas 3.2), table D.2 approchée par les
    catégories lettre et marque d'espacement, points de code non attribués acceptés ; sortie chiffrée non
    déterministe ; `PdfEditor.Encrypt` ne reporte pas les structures de document (signets, formulaires).
- Contrôle : le lecteur du paquet ouvre le fichier avec le bon mot de passe et le refuse sans ; les
  permissions se relisent.

## Lot 8 : caviardage PDF

- [x] Suppression réelle d'un texte et des images sous une zone : contenu retiré du flux, pas seulement
  recouvert ; métadonnées nettoyées sur demande.
  - Fait (2026-10-10) : `PdfRedactor.Redact` écrit un nouveau fichier : glyphe touché par une zone retiré en
    entier de sa chaîne (la suite garde sa place par un ajustement `TJ`), image couverte retirée, image touchée
    redessinée avec les pixels sous la zone à la couleur de remplissage, images en ligne touchées, masques et
    images illisibles retirés, formulaires XObject réécrits, annotations touchées retirées, `ActualText`, `Alt` et
    `E` du contenu marqué retirés, zones peintes ; métadonnées (Info et XMP des pages) retirées sur demande.
    8 tests : texte retiré absent de l'extraction et de toutes les chaînes et de tous les flux décompressés du
    fichier (originaux d'images et de formulaires compris), positions des lettres restantes inchangées (avec
    `Tc`, `Tw`, `Tz`, `Ts`, `TL`, `'`, `"`), pixels de l'image sous la zone noirs et les autres intacts, zone peinte au
    rendu, annotation, texte de remplacement et métadonnées retirés. Suite rapide 1961 tests, couverture des
    lignes 97,19 %.
  - Limites : tracés vectoriels et dégradés sous une zone recouverts, non retirés (signalé dans `Gaps`) ;
    propriétés de contenu marqué nommées non nettoyées ; structures de document (signets, formulaires, structure
    balisée) non reportées, comme pour `PdfEditor`.
- Contrôle : le texte caviardé n'apparaît plus ni à l'extraction ni dans les octets décompressés du fichier.

## Lot 9 : Excel vers HTML

- [x] Tableau HTML des valeurs affichées, avec styles, fusions et largeurs de colonnes.
  - Fait (2026-10-10) : `ExcelToHtml.Convert` (`ExcelHtmlOptions`), une page autonome, un tableau par feuille
    (plage utilisée) : valeurs affichées, largeurs de colonnes (`7w + 5` pixels), fusions en `colspan` et
    `rowspan` (coupées à la plage, une fusion chevauchante d'un fichier endommagé ignorée), police, taille, gras,
    italique, souligné, barré, couleur, remplissage, alignements, retour à la ligne et bordure en classes d'une
    feuille de style intégrée. Texte du classeur encodé par l'encodeur HTML du paquet, noms de police réduits aux
    lettres, chiffres, espaces, tirets et soulignés, couleurs acceptées en `RRGGBB` seulement ; le corps sort
    inchangé du `HtmlSanitizer` par défaut. 14 tests dans `ExcelToHtmlTests` sur des classeurs écrits par le
    paquet, enregistrés puis relus : chaque tableau relu par `HtmlParser`, fusions étalées sur la grille, donne
    les enregistrements de `XlsxCsvConverter.ToCsv` (culture fr-FR, deux feuilles). Non rendus : hauteurs de
    lignes, lignes et colonnes masquées, texte qui déborde sur les cellules vides voisines (coupé à sa cellule),
    couleurs des formats de nombre, mises en forme conditionnelles, images et graphiques.
- Contrôle : le HTML produit, relu par l'analyseur du paquet, donne les mêmes textes que l'export CSV.

## Lot 10 : fidélité de la mise en page Word

- [x] Numérotation des notes de bas de page qui recommence à chaque page ou à chaque section (réglage du
  document et `w:footnotePr` de chaque section, lu et écrit), dans l'appel en texte comme dans la note
  (2026-10-07).
- [x] Polices Symbol et Wingdings (2026-10-10, décision du propriétaire) : le texte Symbol, dessiné avec
  Liberation Sans, avance des largeurs du fichier de métriques Core 14 d'Adobe (`Symbol.afm`, livré non
  modifié avec sa licence `MustRead.html`) ; le texte Wingdings et Webdings est dessiné avec Noto Sans
  Symbols 2 (OFL 1.1, repli Liberation Sans) sur la hauteur de ligne de Liberation Sans. 7 tests (avances
  relues dans le PDF, glyphes présents, texte extrait). Non fait : table Wingdings complète (aucune
  correspondance publiée sous licence Unicode : les documents WG2 en sont exclus), elle garde ses 21
  entrées, un code inconnu reste dessiné en puce ; largeurs et hauteur de ligne propres à Wingdings
  inconnues (données Microsoft, exclues).
- [ ] Texte qui contourne les objets flottants, écriture de droite à gauche, colonnes de largeurs inégales.
- Contrôle : positions des lignes vérifiées au point près sur des pages calibrées.

## Lot 11 : fidélité du rendu PDF

- [ ] Motifs en mosaïque, masques doux, modes de fusion, images JPEG 2000 et JBIG2.
  - JBIG2 fait (2026-10-09) : toutes les régions et tous les dictionnaires, segments globaux, filtre `JBIG2Decode`
    (modèles génériques étendus non pris en charge).
  - JPEG 2000 fait (2026-10-10) : flux bruts et fichiers JP2/JPX, ondelettes 5-3 et 9-7, RCT et ICT, toutes les
    progressions et les POC, tuiles et parties de tuile, précincts, en-têtes regroupés PPM/PPT, tous les styles de
    blocs, ROI, sous-échantillonnage, composantes signées de 1 à 30 bits, palettes et définitions de canaux, filtre
    `JPXDecode` avec `SMaskInData`. Mesure : 69 échantillons (fixtures du paquet, licences dans leur `LICENSE.txt`)
    comparés plan par plan à des références indépendantes : 64 réversibles identiques à l'échantillon près (60 à un
    décodeur de référence, 2 à un rendu PDF de référence, 1 à l'image source, 1 contrôlé indirectement : couleurs
    réécrites sans compression rendues à l'identique, opacité égale à la source),
    5 irréversibles à un niveau au plus (au plus 4 échantillons sur 48 076 écartés d'un niveau, erreur
    quadratique moyenne inférieure à 1e-4) ; image de 4096 x 3072 en 12 tuiles identique au motif encodé ; 1 200
    flux endommagés décodés ou refusés proprement. Non pris en charge (refusés proprement) : codeur de blocs haut
    débit (15444-15), extensions de la partie 2, composantes de plus de 30 bits ; profils ICC non appliqués
    (espace de leurs données retenu), décalages CRG ignorés, première spécification de couleur retenue.
- Contrôle : pixels attendus calculés d'après la spécification sur des pages écrites à la main.

## Lot 12 : fractions Excel

- [x] Formats `# ?/?`, `# ??/??` et dénominateurs fixes affichés en fraction comme Excel.
  - Fait (2026-10-10) : `FractionSection`, appelé par `NumberFormatter.Format`. Fraction la plus proche dont le
    dénominateur a au plus autant de chiffres que de signes (fractions continues, réduites et dernière fraction
    intermédiaire, la plus petite en cas d'égalité ; neuf chiffres au plus), dénominateur écrit gardé et
    numérateur arrondi, fraction impropre sans partie entière, nombres mixtes, signe, partie entière nulle laissée
    aux signes, fraction nulle remplacée par des espaces de sa largeur, `?` complété d'espaces (numérateur à gauche,
    dénominateur à droite), groupement, littéraux, sections. 61 cas dans `FractionFormatTests`, attendus établis à
    la main d'après les règles des codes de format (ECMA-376 partie 1, 18.8.30 et 18.8.31, formats intégrés 12 et
    13) et les fractions continues des valeurs, dont un classeur enregistré puis relu. Limites : attendus non
    confrontés à Excel lui-même (pas d'Excel disponible) ; l'égalité entre deux fractions également proches, le
    signe devant une partie entière nulle (`- 1/2`) et le `0` d'un dénominateur sont des choix déduits, non
    documentés ; une section avec point décimal ou exposant n'est pas lue comme une fraction.
- Contrôle : table de valeurs et de textes attendus pour chaque forme de format.

## Lot 13 : signature électronique PDF

- [ ] Signature et vérification (PAdES de base), certificats fournis par l'application.
- Contrôle : une signature valide se vérifie ; toute modification après signature est détectée.

## Lot 14 : graphiques

- [ ] Lecture des graphiques Excel et Word (barres, lignes, secteurs) et rendu dans le PDF.
- Contrôle : valeurs et légendes retrouvées dans le PDF, formes vérifiées au pixel sur des cas simples.

## Lot 15 : OCR

- [ ] Reconnaissance de texte des PDF scannés, dans un paquet séparé si elle demande une dépendance lourde.
- Contrôle : décision du propriétaire sur la forme (paquet séparé ou non) avant tout code.
