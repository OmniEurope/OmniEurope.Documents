<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-007 : corrections de l'audit et cohérence du paquet

Demande du propriétaire confirmée le 30 octobre 2026 : traiter les treize constats de l'audit neuf, commencer par les élevés puis les incohérences des API, sans commit ni publication. Branche `feature/audit-13-corrections`, base `17d9e29`.

Chaque point exige une reproduction avant correction, le même parcours réel après correction et une contrelecture indépendante du diff et de ses preuves. Aucun statut terminé sur la seule compilation. Les scénarios de sécurité déjà reproduits sont expressément autorisés dans des processus locaux isolés. Aucun autre élargissement.

## Lots et critères d'acceptation

| Lot | Point | Résultat attendu | État |
|---|---|---|---|
| 1 | PDF-001 | Une chaîne de résolutions imbriquées est refusée proprement ; lectures usuelles et cycles restent bornés | Vérifié localement, deux contrôles |
| 1 | PDF-003 | Aucun texte alternatif sensible ne subsiste après caviardage, y compris propriété nommée, partagée et forme | Vérifié localement, deux contrôles |
| 1 | GOV-001 | Couverture collectée et garde exécuté avant le paquet ; commandes locales réelles et refus d'une violation vérifiés | Vérifié localement, CI distante non exécutée |
| 1 | GOV-002 | Copie canonique du garde disponible au checkout ; rapport vide mélangé refusé et couverture normale acceptée | Vérifié sur export candidat, deux contrôles |
| 2 | CONV-001 | Détection identique en lectures courtes et normales, sync et async, lignes vides et limite préservées | Vérifié localement, deux contrôles |
| 2 | CONV-002 | Encodages stricts et fallbacks personnalisés conservés ; contrôle du BOM inchangé | Vérifié localement, deux contrôles |
| 2 | OFF-001 | Texte, HTML, Markdown et PDF excluent les lignes supprimées et gardent les insérées, après sauvegarde et relecture | Vérifié localement, deux contrôles |
| 2 | PDF-002 | Date structurelle identique dans les quatre cultures et cohérente avec XMP | Vérifié localement, deux contrôles |
| 2 | PDF-005 | Texte et positions hérités par une forme correspondent au rendu, sans fuite d'état vers l'appelant | Vérifié localement, deux contrôles |
| 2 | CONV-003 | Gaps décrit les pertes de formatage effectivement rencontrées sans doublons | Vérifié localement, deux contrôles |
| 3 | OFF-002 | Plages explicites et cellules vides conservent leurs dimensions ; références entières et budgets restent bornés | Vérifié localement, deux contrôles |
| 3 | OFF-003 | Arrondis extrêmes produisent une valeur ou erreur de formule contrôlée sans interrompre le recalcul | Vérifié localement, deux contrôles |
| 3 | PDF-004 | Masque de couleur appliqué avant composition ; masque explicite traité ou perte signalée, SMask conservé | Vérifié ; masque explicite appliqué, perte signalée pour le seul masque par couleur JPEG/JPX |

GOV-002 est couplé à GOV-001 : le garde local ignoré initialement doit aussi être disponible dans un checkout CI neuf. Aucun seuil ou exception n'est assoupli.

## Validation et limites

Les preuves détaillées et les trois contrelectures restent locales. L'audit initial reste le constat historique avant correction.

Validation de la première version du correctif :

- Suite complète avec couverture : 2 468 tests rapides et 50 tests de stress réussis, aucun ignoré ; 97,07 % des lignes, 88,92 % des branches ; garde CRAP, 3 780 méthodes, aucune exception.
- Commandes CI reproduites localement en Release : 2 468 tests réussis, 97,57 % des lignes et 87,53 % des branches ; garde CRAP réussi, puis paquet vérifié sans dépendance.
- Garde réexécuté indépendamment depuis un export candidat de 846 fichiers livrables : couverture normale acceptée, rapport vide mélangé et violation synthétique refusés (codes 0/1/1). Aucun seuil ni exception modifié.
- Consommateur réel `PackageReference` et cache neuf : 14 assertions générales et 36 assertions de correction réussies, puis seconde exécution indépendante. DLL identique au binaire Release validé ; signatures publiques comparées à l'audit initial, inchangées.

Reprise après revue du commit : les plages explicites gardent leurs dimensions sans allouer leurs cellules vides, et le masque explicite est appliqué. Suite complète avec couverture : 2 492 tests rapides et 50 tests de stress réussis, aucun ignoré ; 97,08 % des lignes ; garde CRAP, 3 789 méthodes, aucune exception. Commandes CI reproduites localement en Release sur cette version : 2 492 tests réussis, garde CRAP réussi, paquet vérifié sans dépendance. Consommateur réel `PackageReference` et cache neuf : 14 assertions générales et 43 assertions de correction réussies, DLL identique au binaire Release, signatures publiques inchangées. Aucun run hébergé.

Le complément autorisé couvre les labels Markdown échappés/incomplets et les segments JPEG d'application ignorés, sans modification métier. Validation effectuée avant tout commit ou staging ; commit local autorisé ensuite par le propriétaire le 30 octobre 2026. Aucun push ni publication. Version 1.0.0 conservée.

Limites : aucun run GitHub/Ubuntu hébergé, Office externe, AOT ou nouveau PublishTrimmed. Le masque par couleur JPEG/JPX reste non pris en charge et produit un gap ; JPX relu seulement. Piste adjacente non reproduite sur les styles conditionnels de première ligne HTML/PDF après suppression d'une ligne, non modifiée. Le prévol du lanceur couvre le suivi des versions du SDK en l'absence de Dependabot. Aucun nouvel archivage GitHub des rapports ; preuves locales conservées.
