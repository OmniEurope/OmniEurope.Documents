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
| [PLAN-007](PLAN-007-corrections-audit-coherence.md) | Treize corrections vérifiées localement, double contrôle ; CI distante non exécutée | Aucun |

## Plans archivés

| Plan | Travail | ADR |
|---|---|---|
| [PLAN-006](archive/PLAN-006-ocr.md) | OCR : abandonné par décision du propriétaire (2026-10-20), aucun code ; le paquet reste sans OCR | Aucun |
| [PLAN-005](archive/PLAN-005-robustesse-et-fidelite.md) | Robustesse et fidélité : fichiers forgés refusés (16 tests), rendus et écritures conformes (9), contrats tenus (6) | Aucun |
| [PLAN-004](archive/PLAN-004-fonctionnalites-suivantes.md) | Fonctionnalités suivantes : édition Excel sans perte, formules, formulaires PDF, PDF/A-2, signature PAdES B-B, conversions, fidélité du rendu, graphiques ; le lot OCR est devenu PLAN-006 | Aucun |
| [PLAN-003](archive/PLAN-003-tests-integration.md) | Tests d'intégration : parcours complets, robustesse, volume, parallélisme ; suite rapide à 96,95 % des lignes et 87,71 % des branches | Aucun |
| [PLAN-002](archive/PLAN-002-couverture-95.md) | Couverture des lignes portée de 91,66 % à 96,81 %, sans faux test | Aucun |
| [PLAN-001](archive/PLAN-001-perimetre-initial.md) | Périmètre initial zéro dépendance : CSV, Markdown, diff, HTML, Excel, images, polices, PDF (écriture, lecture, opérations, analyse, rendu), Word et Word vers PDF, conversions, publication | Aucun |
