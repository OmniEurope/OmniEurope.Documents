# Décisions du projet

- 2026-11-09 : audit 360 : nouvelle lecture complète depuis develop, priorité à l'homogénéité des noms, options et comportements publics entre formats ; commit préalable uniquement si nécessaire ; aucune correction de code ni publication pendant l'audit.
- 2026-11-09 : livraison : committer automatiquement le travail terminé, tout l'arbre de travail inclus, sans demander confirmation ; aucun push ni publication implicite.
- 2026-11-09 : rapport HTML de l'audit : livraison avec contrôle statique acceptée, affichage explicitement non vérifié après refus du protocole file: par le navigateur ; aucun contournement.
- 2026-11-09 : corrections de l'audit 360 : constats utiles implémentés ; Paginator.cs sous 600 lignes accepté tel quel ; KIT-001 (analyseur GEN008) non câblé, en attente d'une décision de politique ; noms d'API publics non harmonisés (aucun défaut, alias additifs seulement si besoin futur).
- 2026-11-09 : classes partial : interdites sauf utilité réelle (Entity Framework, générateurs de source comme GeneratedRegex) ; vérifié conforme, aucun garde supplémentaire.
- 2026-11-09 : test JPEG 2000 à limite de temps : mesurer le temps processeur du thread au lieu du temps écoulé, test réactivé.
- 2026-11-09 : OOXML Strict (Excel) : refusé pour l'instant avec une erreur explicite ; à implémenter le jour où un besoin réel apparaît.
- 2026-11-09 : noms d'API publics : WordHtmlImage reçoit l'alias ContentType, MediaType marqué obsolète (retrait à une version majeure) ; Worksheet (modèle) et Sheet (éditeur) restent distincts, rôles différents.
- 2026-11-11 : SDK : global.json sur le SDK 11.0.100 (dernier installé), cible net10.0 inchangée.
- 2026-11-11 : version : 1.2.0 (mineure incrémentée, patch à 0) pour la prochaine publication.
- 2026-11-11 : cibles : le paquet cible net10.0 et net11.0 (remplace « cible net10.0 inchangée »).
- 2026-11-11 : plans : PLAN-007, PLAN-009 et PLAN-010 terminés, archivés.
