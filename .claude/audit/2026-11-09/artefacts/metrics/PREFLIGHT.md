# Prévol

Référence : develop 2594b1d7cd6edceedd330e2e72377fe3b7c989af. Arbre initial propre. main et develop locaux et distants identiques, vérifiés par Git le 9 novembre 2026. Aucun commit préalable nécessaire.

| Capacité | État | Preuve et conséquence |
|---|---|---|
| Inventaire Git | OK | 949 fichiers initiaux et le registre de décisions ajouté à la livraison : 950 preuves, zéro entrée non auditée, contrôle EVIDENCE_CHECK réussi |
| Écriture de l'état local | OK | Dossier du présent audit créé et relu |
| Audit précédent | OK | 30 octobre : six registres, zéro entrée en attente et synthèse présente ; historique uniquement, nouvelle passe Full |
| Navigation Roslyn | OK | Sous-agent : get_project_graph et find_symbol(WordDocument), réponses isError:false ; quatre projets et symbole du dépôt courant |
| Métadonnées Roslyn TFM | FAILURE | Le graphe affiche netcoreapp1.0 ; valeur écartée, manifests et compilations Windows/Linux prouvent net10.0 |
| MCP omnieurope | ABSENT | Aucun outil exposé ; lecture des règles du kit local C:/Dev/_Generic |
| Complexité Roslyn dédiée | ABSENT | Aucun endpoint exposé ; complexité et couverture seront prises conjointement dans Cobertura si la collecte réussit |
| Gitleaks | OK | CLI hôte absente, image locale vérifiée 8.30.1 exécutée sans réseau, 1 CPU/512 Mo ; historique 118 commits et arbre courant scannés, aucun secret détecté, journaux et JSON conservés |
| Outils Python | SKIPPED | Interdits par le propriétaire, aucune invocation ni recherche d'installation |
| Compilation | OK | Restore puis build Release réels, sortie 0, zéro avertissement et zéro erreur ; journaux restore.log et build-release.log |
| Tests et couverture | OK | Une seule exécution effective après tous les lots : 2809 rapides et50stress, zéro échec/ignoré ;deux DLL couvertes,97,44 % lignes et87,55 % branches, garde4035méthodes réussi |
| Paquet et consommateur | OK | Paquet 1.1.0 local, contrôle sans dépendance et deux assemblages réussi ; cache neuf et PackageReference réel, 14 assertions générales et 12 fontes réussies, aucun changement de version ni publication |
| Contrôle visuel HTML | FAILURE | Protocole file: refusé par le navigateur, contournement interdit ; contrôle statique seulement, livraison avec cette limite expressément acceptée |

Le workflow local audit.md fait 81 686 octets, taille d'instructions supérieure à 20 Ko signalée. Le prévol sémantique permet la passe complète ; aucune dégradation sémantique n'est acceptée silencieusement. Les éventuelles limites seront localisées dans les preuves.
