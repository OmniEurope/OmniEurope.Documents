<!-- SPDX-License-Identifier: EUPL-1.2 -->
# Plans d'implémentation

Tous les plans numérotés du dépôt vivent ici. Le code et les tests restent les sources structurelles ;
un plan décrit un travail, pas l'état du produit.

## Nomenclature

- Format obligatoire : `PLAN-NNN-description-minimale.md`.
- `NNN` est dense et suit l'ordre du registre ci-dessous.
- Un plan terminé est déplacé dans `archive/` ; il garde son numéro.

## Plans actifs

| Plan | Travail | ADR |
|---|---|---|
| [PLAN-004](PLAN-004-fonctionnalites-suivantes.md) | Fonctionnalités suivantes : édition Excel sans perte, formules, formulaires PDF, PDF/A, conversions, fidélité du rendu | Aucun |

## Plans archivés

| Plan | Travail | ADR |
|---|---|---|
| [PLAN-003](archive/PLAN-003-tests-integration.md) | Tests d'intégration : parcours complets, robustesse, volume, parallélisme ; 97,69 % des lignes, 89,90 % des branches | Aucun |
| [PLAN-002](archive/PLAN-002-couverture-95.md) | Couverture des lignes portée de 91,66 % à 96,81 %, sans faux test | Aucun |
| [PLAN-001](archive/PLAN-001-perimetre-initial.md) | Périmètre initial zéro dépendance : CSV, Markdown, diff, HTML, Excel, images, polices, PDF (écriture, lecture, opérations, analyse, rendu), Word et Word vers PDF, conversions, publication | Aucun |
