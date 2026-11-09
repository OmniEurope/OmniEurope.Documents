# Audit 360 du 9 novembre 2026

Périmètre confirmé : dépôt entier depuis develop, commit de référence 2594b1d7cd6edceedd330e2e72377fe3b7c989af. main et develop locaux et distants identiques ; arbre initial propre, aucun commit préalable nécessaire. Ancien audit du 30 octobre terminé, conservé comme historique ; aucune preuve ancienne réutilisée pour cette nouvelle lecture.

## Lots et contrôle mesurable

1. Prévol, inventaire complet, signatures et capacités sémantiques réelles.
2. Architecture et règles applicables, graphe des projets et découpage.
3. Revue transversale dédiée des API publiques : noms, verbes, options, paramètres, résultats, flux, annulation, diagnostics, comportements entre formats.
4. Noyau PDF, registre et preuves par fichier.
5. Noyau Word/Excel, registre et preuves par fichier.
6. Noyau conversions/CSV/Markdown/HTML/Diff, registre et preuves par fichier.
7. Noyau images/fontes/texte/interne, registre et preuves par fichier.
8. Assemblage des fontes, ressources et licences, registre par fichier.
9. Tests rapides et fixtures, registre par fichier.
10. Tests de stress et utilitaires partagés, registre par fichier.
11. Gouvernance, manifests, CI, documentation, registre par fichier.
12. Une validation finale complète avec couverture et garde ; paquet et consommateur public réels.
13. Confrontation des constats aux preuves, doublons retirés et sévérités harmonisées.
14. Synthèse, index de preuves et tableau HTML ; contrôle statique réussi, affichage bloqué par la politique du navigateur, livraison avec cette limite acceptée par le propriétaire.
15. Contrôle final : 949 fichiers initiaux et le registre de décisions ajouté à la livraison, soit 950 preuves, zéro entrée non auditée, synthèse et HTML présents, aucune modification du code.

Le projet principal est subdivisé uniquement pour la revue, en registres distincts à rédacteur unique. Aucun changement de structure du produit. Les limites d'outillage et de preuve sont consignées dans metrics/PREFLIGHT.md et dans la synthèse finale. Aucun outil Python, installation sur le PC, publication ou correction. Une sonde de formule involontairement itérative a été interrompue, sans répétition sur le PC ; les autres scénarios lourds sont restés dans des conteneurs limités. Une seule exécution finale effective de chaque suite, sans neutralisation de test.
