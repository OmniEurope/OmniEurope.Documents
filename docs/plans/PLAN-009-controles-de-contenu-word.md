<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-009 : contrôles de contenu Word, encre et fidélité

Demande d'une application consommatrice, relayée le 4 novembre 2026 et acceptée par le propriétaire : remplir un modèle Word (contrôles de contenu), le sauver en .docx, le convertir en PDF au plus près de Word 365, éditer un modèle minimal sans interface, marquer les champs en HTML. Branche `feature/word-content-controls`, base `b7a9a52`. Version visée : 1.1.0 (ajouts, aucune rupture). Aucune publication de `main` sans ordre du propriétaire.

Les fichiers de référence de l'utilisateur contiennent des données personnelles : ils servent de mesure locale, jamais de fixture. Les tests utilisent des documents synthétiques.

## Mesure de départ

Modèle de référence (6 contrôles : 2 listes déroulantes modifiables, 4 dates `dd/MM/yyyy`, une encre `wpi` avec repli VML PNG) : 1 page comme Word 365 ; l'encre n'est pas dessinée (`drawing skipped`) ; avec Carlito seul comme avec Calibri enregistré, les titres en Calibri Light sont trop gras et une ligne justifiée se coupe un mot plus tôt que Word.

## Lots et critères d'acceptation

| Lot | Travail | Critère d'acceptation | État |
|---|---|---|---|
| A | Lire et remplir les contrôles de contenu, .dotx vers .docx | Les 6 contrôles d'un modèle synthétique équivalent sont listés avec genre, format, éléments et état d'espace réservé ; remplis par étiquette (date au format du contrôle, `w:fullDate` à jour, espace réservé retiré) ; un .dotx sauvé devient un document ; étiquette inconnue signalée | Vérifié localement : 25 tests ; fixture remplie, sauvée en .docx (`document.main`), rouverte et rendue en PDF avec les valeurs ; date invalide et étiquette inconnue signalées |
| B | Encre dans Word vers PDF | Traits InkML dessinés en vectoriel, repli sur l'image VML ; plus de gap pour l'encre du modèle | À faire |
| C | Marqueurs HTML des contrôles | Option désactivée par défaut ; chaque contrôle entouré d'un `span` (ou `div` bloc) `data-oe-field` | À faire |
| D | Comparaison de pages rendues | API publique : part de pixels différents après seuil et tolérance de voisinage, image de différence | Vérifié localement : 15 tests ; fixture Word 365 contre elle-même = 0 |
| E | Édition : contrôles par paragraphe, insertion et remplacement d'un dessin (traits ou PNG) | Un document édité puis sauvé donne le même PDF qu'un document généré depuis lui | À faire |
| F | Fidélité | Calibri Light résolu (police enregistrée) ou signalé ; mêmes coupures de ligne que Word 365 sur le modèle, avec Carlito seul ; substitution par Carlito signalée dans les Gaps (décision du 4 novembre : pas de fourniture de polices par l'hôte) | Vérifié localement : 10 tests (3 échouaient avant) ; fixture avec Carlito seul : mêmes lignes que Word 365, titres Calibri Light signalés dans les Gaps ; seuil de rétrécissement des espaces 25 % estimé (borné entre 23 et 26 %) |

Seuil annoncé au consommateur : mêmes coupures et même nombre de pages, moins de 1 % de pixels différents à 150 dpi.

## Validation et limites

À remplir lot par lot avec la commande ou l'observation qui le prouve.
