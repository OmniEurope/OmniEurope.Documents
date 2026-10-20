<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-006 : OCR

> Statut : **à faire** (2026-10-20). Lot 15 de [PLAN-004](archive/PLAN-004-fonctionnalites-suivantes.md), transféré
> ici quand ce plan a été archivé. Aucun code avant la décision du propriétaire sur la forme.

## Objectif

Extraire le texte des PDF scannés (pages faites d'images seules), aujourd'hui sans texte. La règle zéro
dépendance de `src/` reste en vigueur : une reconnaissance qui demande une dépendance lourde va dans un paquet
séparé, pas dans le paquet principal.

## Lot 1 : reconnaissance de texte

- [ ] Reconnaissance de texte des PDF scannés, dans un paquet séparé si elle demande une dépendance lourde.
- Contrôle : décision du propriétaire sur la forme (paquet séparé ou non) avant tout code.
