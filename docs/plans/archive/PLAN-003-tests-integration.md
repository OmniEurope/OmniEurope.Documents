<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-003 : Tests d'intégration

> Statut : **terminé** (2026-10-07). Demande du propriétaire : un paquet blindé dans tous les sens, par des
> parcours complets (créer, éditer, fusionner, convertir, relire) en plus des tests unitaires.

## Objectif

Vérifier le paquet comme l'utilise une application : chaque scénario enchaîne plusieurs fonctions publiques
et relit le résultat avec les lecteurs du paquet, sans accès aux types internes. Les tests vivent dans
`tests/OmniEurope.Documents.Tests/Integration/`.

Règles :

- Chaque étape d'un parcours est relue et vérifiée (contenu, ordre, nombre de pages, métadonnées), pas
  seulement exécutée.
- Les entrées abîmées sont générées de façon déterministe (graine fixe) : un échec se rejoue à l'identique.
- Un fichier invalide doit être refusé par une exception documentée (`InvalidDataException`,
  `DocumentFormatException`, `PdfPasswordException`, `CsvFormatException`, `NotSupportedException`), jamais
  par un plantage interne ni une boucle sans fin.
- Un défaut trouvé est corrigé dans le même lot et cité dans la note du lot.
- Seuils de couverture du projet : 95 % des lignes et 85 % des branches.

Chaque lot se termine par un commit sur `develop` quand la suite de tests et le contrôle CRAP sont verts.

## Lot 1 : Word

- [x] Document riche créé (titres, listes, tableau, notes, sections, en-têtes et pieds, image), enregistré,
  relu ; édité (remplacements, ajout d'un autre document), fusionné avec deux autres ; converti en PDF puis
  relu (texte dans l'ordre, pages, métadonnées) et rendu.
- Contrôle : chaque étape relue donne le contenu attendu ; tests verts.
  Fait : 5 parcours (`WordWorkflowTests`) : relecture complète, remplissage et ajout (notes renumérotées,
  image conservée), fusion de trois documents (orientation des pages suivie), image, en-tête et champs du
  PDF rendus, édition d'un paragraphe qui ne touche que `word/document.xml`. Aucun défaut trouvé.

## Lot 2 : Excel et CSV

- [x] Classeur à plusieurs feuilles (types, formats, dates, fusions, volets figés) enregistré et relu ;
  aller-retour CSV ; conversion en Word et en PDF relue.
- Contrôle : valeurs et textes affichés identiques après chaque aller-retour ; tests verts.
  Fait : 3 parcours (`ExcelWorkflowTests`), chaque cellule comparée après relecture (valeur, formule, style,
  texte affiché). Défauts corrigés : une durée (`[h]:mm`) relue devenait une date du 1er janvier 1900, elle
  reste un nombre de jours ; dans le PDF, un nombre trop large pour sa colonne était coupé sur deux lignes,
  il est maintenant affiché comme Excel (chiffres réduits sous le format Standard, sinon `#`).

## Lot 3 : Markdown et HTML

- [x] Markdown analysé, réécrit et réanalysé ; Markdown et HTML (nettoyé) convertis en Word puis en PDF,
  relus dans l'ordre.
- Contrôle : mêmes blocs après réécriture, texte complet et ordonné dans les PDF ; tests verts.
  Fait : 3 parcours (`MarkupWorkflowTests`), dont un contenu malveillant suivi jusqu'au PDF (aucun script,
  style ni lien `javascript:`, même en annotation). Défaut corrigé : nettoyer une page HTML complète
  (`<html><body>...`) rendait une chaîne vide ; un fragment ignore désormais `html`, `head` et `body` comme
  les navigateurs, et le contenu du corps est conservé.

## Lot 4 : PDF

- [x] PDF produits par le paquet (écrivain, Word, Excel, Markdown, images) fusionnés, découpés, extraits,
  tournés, élagués, tamponnés et compressés, chaque résultat relu et rendu.
- Contrôle : pages, texte par page, rotation et images conformes à chaque étape ; tests verts.
  Fait : 3 parcours (`PdfWorkflowTests`) sur un PDF de six pages venant de trois écrivains ; chaque page
  retrouvée par son texte et par sa couleur dans le rendu, le tampon vérifié comme mise à jour incrémentale
  (le fichier d'origine reste intact en tête). Défaut corrigé : `MaxImageSide` était ignoré pour une image
  de moins de 64 × 64 pixels ou quand le JPEG n'était pas plus petit ; l'image est désormais toujours
  réduite et reste sans perte si le JPEG ne paie pas.

## Lot 5 : robustesse

- [x] Fichiers produits par le paquet (DOCX, XLSX, PDF, PNG, JPEG, GIF, BMP, TIFF, CSV) abîmés par
  milliers (octets modifiés, troncatures, parties d'archive retirées) puis relus.
- Contrôle : aucune exception hors de la liste documentée, aucun dépassement de temps ; tests verts.
  Fait : `RobustnessTests`, plus de 10 000 entrées abîmées (graines fixes), plus 2 000 textes aléatoires
  pour Markdown, HTML et CSV ; Word, Excel, PDF, polices et texte balisé tenaient déjà. Défauts corrigés :
  les décodeurs JPEG, GIF et TIFF levaient des erreurs d'index internes sur un fichier tronqué ou corrompu
  (1 157 entrées), ils lèvent désormais `InvalidDataException` ; un TIFF sans répertoire lisible levait
  `ArgumentOutOfRangeException` sur le numéro de page ; `ImageInfo.TryIdentify` plantait au lieu de rendre
  false sur un TIFF abîmé.

## Lot 6 : volume, parallélisme et déterminisme

- [x] Gros documents (centaines de pages, dizaines de milliers de cellules ou de lignes) dans une limite de
  temps ; conversions en parallèle ; même entrée, mêmes octets.
- Contrôle : limites tenues, sorties parallèles identiques aux sorties séquentielles ; tests verts.
  Fait : `ScaleTests` : rapport Word de 144 pages, feuille de 48 000 cellules et impression avec en-tête
  répété sur chaque page, PDF de 1 000 pages fusionné en 2 000 puis découpé, 200 000 enregistrements CSV ;
  chaque cas sous la seconde, limites fixées à 30 ou 60 s pour attraper un blocage ou une dérive
  quadratique. Six productions refaites à l'identique à l'octet, et 24 en parallèle identiques aux
  séquentielles. Écart trouvé puis traité à la demande du propriétaire : en PDF, le texte d'une cellule
  Excel sans renvoi à la ligne passait à la ligne ; il reste désormais sur une ligne, déborde sur les
  cellules vides voisines (à droite, à gauche ou des deux côtés selon l'alignement) et est coupé au bord de
  la première cellule non vide (`ExcelTextOverflowTests`, chaque lettre contrôlée dans le rendu). Les tests
  longs sont dans `tests/OmniEurope.Documents.StressTests`, hors de la suite rapide.

## Lot 7 : mesure finale

- [x] Suite complète mesurée.
- Contrôle : au moins 95 % des lignes et 85 % des branches, `CRAP gate passed`.
  Fait : 97,69 % des lignes (530 non exécutées sur 22 918), 89,90 % des branches, 1 006 tests,
  `CRAP gate passed` (2 303 méthodes).
