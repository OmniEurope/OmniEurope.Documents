# Sécurité : portée des preuves

État provisoire du prévol, à compléter après les revues et les requêtes de dépendances.

- Compilation Release réelle : zéro avertissement et zéro erreur, voir build-release.log. Aucun diagnostic de sécurité du compilateur n'a été émis dans cette compilation.
- Navigation Roslyn réelle disponible et diagnostics sémantiques conservés dans semantic-preflight.json. Ce résultat ne constitue pas un scan SAST général.
- Gitleaks natif absent du poste ; alternative réelle exécutée dans son image officielle déjà locale, version 8.30.1 et digest vérifiés. Conteneur sans réseau, 1 CPU/512 MiB, dépôt en lecture seule, secrets masqués : historique --all (118 commits, 49,6 s) et arbre courant suivi (archive HEAD dans /tmp, 3,01 s), sorties 0, aucun secret détecté. JSON vides et logs gitleaks-history.* / gitleaks-current.* conservés. L’arbre Git était propre ; fichiers ignorés et sorties de build hors scan courant. Aucun SAST général revendiqué.
- Revue de sécurité par module : contrôles d'entrée, limites d'allocation et récursion, archives/XML, HTML/URI, caviardage, chiffrement et signature. Les conclusions attendent les preuves par fichier ; l'absence de scan n'est pas un défaut produit.
- Dépendances : requêtes NuGet natives réelles, sorties 0 après levée de la limite DNS du bac. Aucun paquet vulnérable rapporté, xunit.v3 4.0.2 en retard sur 4.0.3 stable (DEP-001 faible). Manifestes, graphes et licences déclarées lus ; preuves dans AUDIT_DEPENDENCIES.md et dependencies-*.json. Aucune mise à jour appliquée.
- Une sonde de formule REPT vide avec un grand compteur a démarré une itération longue sur le PC et a été interrompue après dix secondes, sans répétition. Elle ne prouve ni exception ni résultat terminé ; voir office-probe-limits.md. Les autres sondes restent de petite taille ou à coût constant ; tout approfondissement de coût exige un conteneur strictement limité.
- Aucun outil Python, installation, modification de configuration, exfiltration ou publication.
