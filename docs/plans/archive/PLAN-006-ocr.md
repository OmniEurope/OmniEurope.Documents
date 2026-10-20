<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-006 : OCR

> Statut : **abandonné** (2026-10-20, décision du propriétaire : pas d'OCR). Lot 15 de [PLAN-004](PLAN-004-fonctionnalites-suivantes.md), transféré
> ici quand ce plan a été archivé. Aucun code écrit ; le paquet reste sans OCR (README, « What it does not do »).

## Objectif

Extraire le texte des PDF scannés (pages faites d'images seules), aujourd'hui sans texte. La règle zéro
dépendance de `src/` reste en vigueur : une reconnaissance qui demande une dépendance lourde va dans un paquet
séparé, pas dans le paquet principal.

## Lot 1 : reconnaissance de texte

- Reconnaissance de texte des PDF scannés : abandonnée, non faite.
- Décision du propriétaire (2026-10-20) : pas d'OCR, ni dans le paquet principal ni dans un paquet séparé.
