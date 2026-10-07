<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-002 : Couverture de tests à 95 %

> Statut : **terminé** (2026-10-07). Demande du propriétaire : au moins 95 % de couverture, sans faux test.

## Objectif

Porter la couverture des lignes du paquet à 95 % au moins (rapport Cobertura de la suite complète), en
relevant celle des branches au passage. Point de départ : 91,66 % des lignes (1 907 lignes non exécutées
sur 22 871), 80,8 % des branches.

Règles :

- Chaque test vérifie un résultat attendu établi indépendamment du code testé : valeur calculée à la main
  d'après la spécification, fichier relu par un logiciel tiers ou propriété
  observable (aller-retour, invariant). Un test qui ne fait qu'exécuter du code ne compte pas.
- Aucun code n'est exclu de la mesure pour atteindre le chiffre.
- Une branche prouvée inatteignable (aucune entrée ne peut y mener) est retirée et citée dans la note du
  lot ; une fonctionnalité entière jamais appelée est signalée au propriétaire, pas supprimée d'office.
- Un défaut trouvé en écrivant un test est corrigé dans le même lot et cité dans la note du lot.

Chaque lot se termine par un commit sur `develop` quand la suite de tests et le contrôle CRAP sont verts.

## Lot 1 : images

- [x] BMP, TIFF, CCITT, informations d'image et décodeurs restants (276 lignes non exécutées).
- Contrôle : couverture des lignes de `Imaging/` au moins 95 %.
  Fait : 98,27 % (49 lignes restantes, dont 48 dans des chemins JPEG rares : flux endommagés, longues suites de zéros). Fichiers écrits octet par octet d'après
  les spécifications (BMP, TIFF, T.4/T.6, GIF89a, PNG, en-têtes WebP et JFIF) ; un décodeur externe lit les BMP, GIF et PNG
  produits à l'identique, sauf trois écarts documentés dans les tests (masque vide et BI_ALPHABITFIELDS refusés,
  quatrième octet des BMP 32 bits ignoré). Branches inatteignables retirées : alpha des BMP RLE, repli de
  `CcittRows.FindB`. Couverture globale : 92,65 %.

## Lot 2 : PDF, lecture et texte

- [x] Filtres, analyseur, magasin d'objets, espaces de couleur, décodage des polices, extraction du texte.
- Contrôle : couverture des lignes de `Pdf/Reading/` et `Pdf/Text/` au moins 95 %.
  Fait : 98,56 %. LZW comparé aux bandes LZW écrites par un encodeur TIFF externe ; syntaxe, xref en flux et
  hybrides, flux d'objets, tables abîmées, chiffrement AES-256 révision 5 et chaînes en clair, écrits d'après
  ISO 32000. Défauts corrigés : un flux Flate abîmé perdait tout ce qui était décodé dans la même lecture
  (seul le dernier octet avant le dommage est désormais perdu) ; l'espace Lab retransformait a* et b* déjà
  dans leur plage (`scn`, tableau Decode explicite) et ignorait `/Range` (rouge sRGB rendu (228, 70, 73)).
  Code mort retiré : `PdfParser.Lexer`, `PdfObjectStore.Data`, `PdfCMap.IsEmpty` et `HasUnicode`, une
  branche inatteignable du dictionnaire. Couverture globale : 93,64 %.

## Lot 3 : PDF, rendu, écriture et opérations

- [x] Rastériseur, polices de rendu, canevas d'écriture, édition.
- Contrôle : couverture des lignes de `Pdf/` au moins 95 %.
  Fait : 97,21 %. Courbes, extrémités et pointillés, CMJN, opérateurs de texte, formes, rotations, apparences
  d'annotation, dégradés radiaux, polygones, chemins à trous, mise en page en flux, images de chaque type,
  modèle d'objets. Dégradés radiaux vérifiés avec un rendu PDF externe : mêmes couleurs. Aucun défaut trouvé. Couverture globale : 94,42 %.

## Lot 4 : conversions

- [x] EMF, mise en page Word et HTML.
- Contrôle : couverture des lignes de `Conversion/` au moins 95 %.
  Fait : 96,88 %. EMF : modes d'échelle fixes, transformations du monde, enregistrements 32 bits, alignement
  du texte, bitmaps illisibles ; un lecteur EMF externe place les formes aux mêmes positions. Word :
  régions de style de tableau, alignement et largeur des tableaux, fusions verticales, colonnes de largeur
  imposée, sections sur page paire ou impaire, notes de fin, zones de texte, points de suite, symboles, champs,
  images flottantes. HTML, Excel et TIFF multipage vers PDF. Défaut corrigé : une image de moins de 4 pixels ou
  de plus de 14 400 points convertie à sa taille naturelle faisait échouer `ImagesToPdf` (page hors des limites
  de 3 à 14 400 points). Couverture globale : 95,29 %.

## Lot 5 : Word

- [x] Fusion, lecture, écriture, dessins.
- Contrôle : couverture des lignes de `Word/` au moins 95 %.
  Fait : 95,02 %. Fusion sur des paquets écrits à la main (commentaires retirés, parties partagées copiées une
  fois, notes et listes absentes laissées, signets renumérotés, section close sur un paragraphe, changement de
  paragraphe suivi) ; lecture des lignes dans les contrôles de contenu et le XML personnalisé, lignes supprimées,
  contenu alternatif, équations, sauts, images partagées ou absentes. Aucun défaut trouvé. Couverture globale :
  95,63 %.

## Lot 6 : HTML, Markdown, Excel, CSV, polices et utilitaires

- [x] Sélecteurs et nœuds HTML, liens et blocs Markdown, références de cellules, CSV, polices TrueType,
  `Internal/`, `Text/`, `Diff/`.
- Contrôle : couverture des lignes de chacun de ces dossiers au moins 95 %.
  Fait : HTML 98,18 %, Markdown 95,23 %, Excel 96,29 %, CSV 99,13 %, polices 95,44 %, `Internal/` 100 %,
  `Text/` 97,56 %, `Diff/` 96,39 %. Formats Excel comparés à l'affichage d'un tableur (seuls les cas
  indépendants de la langue). Défauts corrigés : `<?...>` perdait
  son point d'interrogation dans le commentaire (`<!--?php ?-->` dans les navigateurs) ; `am/pm` s'affichait
  en minuscules alors qu'Excel affiche `PM` ; au-delà de 7,9e27 un nombre montrait 31 chiffres exacts au lieu
  des 15 chiffres significatifs d'Excel. Code mort retiré : `HtmlNode.ClearChildren`.

## Lot 7 : mesure finale

- [x] Suite complète mesurée.
- Contrôle : couverture des lignes du paquet au moins 95 %, `CRAP gate passed`.
  Fait : 96,81 % des lignes (730 non exécutées sur 22 860), 87,21 % des branches, 952 tests, `CRAP gate passed`
  (2 296 méthodes).
