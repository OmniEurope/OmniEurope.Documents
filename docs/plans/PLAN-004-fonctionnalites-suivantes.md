<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-004 : Fonctionnalités suivantes

> Statut : **en cours** (2026-10-08 : lot 1 édition Excel sans perte fait ; 2026-10-07 : Word vers HTML). Feuille de route proposée au
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

- [ ] Moteur de formules : opérateurs, références et plages entre feuilles, fonctions courantes
  (mathématiques, logiques, recherche, texte, dates), erreurs (`#DIV/0!`, `#REF!`, `#N/A`...), références
  circulaires refusées ; recalcul à la demande avant export PDF ou CSV.
- Contrôle : résultats attendus calculés à la main pour chaque fonction ; un modèle rempli donne ses totaux
  justes en PDF et en CSV.

## Lot 3 : formulaires PDF

- [ ] Lecture des champs (texte, cases, boutons radio, listes), remplissage, génération des apparences et
  aplatissement.
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
- [ ] Word vers Markdown : reste à faire.
- Contrôle : aller-retour Word, HTML, Word sans perte de texte ni de structure (fait pour le HTML :
  `WordToHtmlTests`, adresses vérifiées contre `WordEditor` dans le corps, les cellules, les en-têtes, les
  pieds de page, les notes et les zones de texte, document d'un autre producteur compris).

## Lot 6 : révisions, table des matières et champs Word

- [ ] Accepter ou refuser les révisions (toutes ou une par une), mettre à jour la table des matières et les
  champs simples (pages, dates, références) à partir de la mise en page.
- Contrôle : après acceptation, le texte relu est celui attendu ; les numéros de page de la table des
  matières correspondent au PDF produit.

## Lot 7 : protection PDF à l'écriture

- [ ] Chiffrement AES-256 avec mots de passe utilisateur et propriétaire, et permissions.
- Contrôle : le lecteur du paquet ouvre le fichier avec le bon mot de passe et le refuse sans ; les
  permissions se relisent.

## Lot 8 : caviardage PDF

- [ ] Suppression réelle d'un texte et des images sous une zone : contenu retiré du flux, pas seulement
  recouvert ; métadonnées nettoyées sur demande.
- Contrôle : le texte caviardé n'apparaît plus ni à l'extraction ni dans les octets décompressés du fichier.

## Lot 9 : Excel vers HTML

- [ ] Tableau HTML des valeurs affichées, avec styles, fusions et largeurs de colonnes.
- Contrôle : le HTML produit, relu par l'analyseur du paquet, donne les mêmes textes que l'export CSV.

## Lot 10 : fidélité de la mise en page Word

- [x] Numérotation des notes de bas de page qui recommence à chaque page ou à chaque section (réglage du
  document et `w:footnotePr` de chaque section, lu et écrit), dans l'appel en texte comme dans la note
  (2026-10-07).
- [ ] Texte qui contourne les objets flottants, écriture de droite à gauche, colonnes de largeurs inégales.
- Contrôle : positions des lignes vérifiées au point près sur des pages calibrées.

## Lot 11 : fidélité du rendu PDF

- [ ] Motifs en mosaïque, masques doux, modes de fusion, images JPEG 2000 et JBIG2.
  - JBIG2 fait (2026-10-09) : toutes les régions et tous les dictionnaires, segments globaux, filtre `JBIG2Decode`
    (modèles génériques étendus non pris en charge).
- Contrôle : pixels attendus calculés d'après la spécification sur des pages écrites à la main.

## Lot 12 : fractions Excel

- [ ] Formats `# ?/?`, `# ??/??` et dénominateurs fixes affichés en fraction comme Excel.
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
