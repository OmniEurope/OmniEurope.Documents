<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-005 : Robustesse et fidélité

> Statut : **en cours** (2026-10-08 : lots 1 et 2 faits). Demande du propriétaire : corriger les défauts relevés
> par la revue complète du code, en trois lots, un commit par lot.

## Objectif

Qu'aucun fichier forgé de quelques octets ne bloque, n'épuise la mémoire ni ne termine le processus ; que
les rendus et les écritures suivent la spécification de leur format ; que chaque méthode tienne le contrat
qu'annonce sa documentation.

Règles :

- Chaque défaut reçoit un test qui échoue avant la correction et passe après.
- Un fichier invalide est refusé par une exception documentée (`InvalidDataException`,
  `DocumentFormatException`, `CsvFormatException`, `NotSupportedException`), jamais par une boucle sans
  fin, un épuisement de mémoire ni un dépassement de pile.
- Une limite est vérifiée avant l'allocation qu'elle protège, et calculée sans débordement.

Chaque lot se termine par un commit sur `develop` quand les deux suites (`.\ylaunch.ps1 -ta`) sont vertes.

## Lot 1 : fichiers forgés

- [x] Excel vers PDF : plafond public de cellules imprimées (`ExcelPdfOptions.MaxCells`, 250 000 par
  défaut, mesuré à environ 4 Ko par cellule pendant la mise en page).
- [x] EMF : comptes de points, de polygones et tailles d'images bornés par la taille réelle de
  l'enregistrement.
- [x] CSV : `MaxRecordLength` compte les caractères consommés, séparateurs compris.
- [x] PDF : prédicteur TIFF ou PNG aux dimensions qui débordent, `/N` d'un flux d'objets, sections XRef à
  largeurs nulles, échantillons d'un shading, sous-routines Type 1, espaces de codes vides d'une CMap.
- [x] Polices TrueType : segments `cmap` qui se chevauchent ou dont le total dépasse le domaine des codes.
- [x] Markdown et Word : profondeur d'imbrication bornée (citations, éléments enveloppants).
- [x] GIF : taille d'une image bornée avant le décodage LZW.
- Contrôle : un test par fichier forgé, refus contrôlé en moins d'une seconde ; deux suites vertes.
  Fait : 16 tests ajoutés. Les 14 qui compilent sur le code d'avant y échouent (exception de mémoire,
  assertion, boucle arrêtée après 45 s, dépassement de pile pour Markdown) ; les 2 autres visent
  `MaxCells`, qui n'existait pas. Tous passent après. Construire l'arbre XML coûte plus à
  chaque niveau : la profondeur d'une partie Word est donc contrôlée par une lecture simple avant le chargement.

## Lot 2 : rendu et données

- [x] EMF : modes isotrope (7) et anisotrope (8) remis à leur place.
- [x] CFF : la largeur n'est retirée des opérandes de stems que pour un nombre impair.
- [x] Extraction PDF : l'échelle horizontale (`Tz`) n'est appliquée qu'une fois à la largeur des lettres.
- [x] JPEG : sur-échantillonnage selon le rapport exact des facteurs (par exemple 2 sur 3).
- [x] Marquage PDF : un `Contents` indirect qui désigne un tableau est résolu avant d'être aplati.
- [x] Fusion Word : styles et listes des en-têtes et pieds copiés sont migrés.
- [x] Remplacement Word : une recherche ne franchit pas une tabulation, un saut ni un champ.
- [x] Écriture Word : une même instance en en-tête et en pied donne deux parties distinctes ; un lien
  hypertexte dans le résultat d'un champ s'écrit.
- Contrôle : un test par défaut dont l'attendu vient de la spécification ; deux suites vertes.
  Fait : 9 tests ajoutés (16 cas) ; chacun échoue sur le code d'avant, hors deux cas témoins qui y passaient
  déjà (`Tz` à 100 %, stems avec largeur). Un champ suivi (w:ins, w:del) ne peut contenir de lien hypertexte :
  son résultat est alors écrit en simples runs.

## Lot 3 : contrats

- [ ] CSV : `Open` et `OpenAsync` libèrent le flux possédé quand l'initialisation échoue.
- [ ] `TryParseDuration` renvoie false hors des bornes de `TimeSpan`.
- [ ] Dates Excel : numéros de série hors de `DateTime` refusés selon l'époque (1900 ou 1904).
- [ ] `PdfFlowLayout` : tout ajout après `Finish` est refusé.
- [ ] Excel vers CSV : plafond de cellules écrites, comme pour le PDF.
- Contrôle : un test par contrat ; deux suites vertes.
