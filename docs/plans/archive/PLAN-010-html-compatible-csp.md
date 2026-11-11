<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-010 : HTML compatible avec une Content-Security-Policy stricte

Demande du propriétaire du 5 novembre 2026 (« option A ») : le HTML de Word vers HTML et d'Excel vers HTML doit s'afficher dans un navigateur sous une politique `default-src 'none'; style-src 'self'; img-src 'self'`, sans `'unsafe-inline'` ni `data:`. Base `develop` (`547000a`). Ajouts seulement, API publique existante compatible au niveau source ; le rendu reste visuellement identique.

## Mesure de départ

- Word vers HTML : un élément `<style>` dans `head`, un attribut `style` sur presque chaque élément (paragraphes, segments, sections, en-tête et pied, notes, tableaux, lignes, colonnes, cellules, zones de texte, étiquettes de liste, images, soulignements et barrés), images en URI `data:`. Sous `style-src 'self'`, la feuille embarquée et tous les attributs `style` sont bloqués ; sous `img-src 'self'`, les images aussi.
- Excel vers HTML : aucun attribut `style` (déjà des classes), mais la feuille est dans un `<style>` embarqué, bloqué de la même façon ; pas d'image.

## API retenue

| Élément | Ajout |
|---|---|
| `WordHtmlOptions.StyleSheetHref`, `ExcelHtmlOptions.StyleSheetHref` | `null` par défaut : feuille embarquée dans `<style>`. Renseigné : la page porte `<link rel="stylesheet" href="…">` et aucun `<style>` ; le consommateur sert la feuille comme fichier (`style-src 'self'`). |
| `WordHtmlOptions.StyleSheetNonce`, `ExcelHtmlOptions.StyleSheetNonce` | `null` par défaut. Renseigné : attribut `nonce` sur `<style>` (ou sur `<link>`). Valeur vérifiée à l'affectation (grammaire `base64-value` de CSP : lettres ASCII, chiffres, `+ / - _`, au plus deux `=` finaux) ; sinon `ArgumentException`, donc aucune injection de balisage. |
| `WordHtmlOptions.ImageUrlPrefix` | `null` par défaut : images en URI `data:` comme avant (choix explicite du consommateur de garder une page autonome). Renseigné : chaque image devient une ressource séparée nommée `image1.png`, `image2.jpg`… (ordre de première apparition), `src` = préfixe + nom. |
| `WordHtmlResult.StyleSheet`, `WordHtmlResult.Images` | Propriétés `init` ajoutées au record existant (constructeur positionnel inchangé) : la feuille complète et la liste des `WordHtmlImage(Name, MediaType, Data)`, vide sans préfixe. |
| `ExcelToHtml.ConvertWithStyleSheet` et `ExcelHtmlResult(Html, StyleSheet)` | Nouvelle méthode (deux surcharges, classeur chargé ou octets) ; `ExcelToHtml.Convert` garde sa signature et rend `ConvertWithStyleSheet(...).Html`. |

Dans tous les modes, la sortie ne contient plus aucun attribut `style` : chaque mise en forme propre à un élément devient une classe générée `omni-sN`, dédupliquée (des déclarations identiques partagent une classe), écrite après les règles de base dans la même feuille. Les sélecteurs de base ont une spécificité au plus égale à celle d'une classe, ou ne visent pas les mêmes propriétés : l'ordre de la feuille garde le résultat des attributs `style` d'avant ; la règle de surlignage reste `!important`.

## Lots et critères d'acceptation

| Lot | Travail | Critère d'acceptation | État |
|---|---|---|---|
| A | Classes générées pour Word | Aucun ` style=` dans le HTML de `rent-receipt-template.docx`, de `external.docx` et des documents de test ; deux éléments de mêmes déclarations ont la même classe ; les tests existants vérifient les mêmes déclarations à travers la classe | Vérifié localement : 3 documents sans `style` (avec et sans options), 16 tests existants adaptés aux mêmes déclarations lues par la classe ; 0 différence élément par élément entre le HTML d'avant et d'après des 2 fixtures Word |
| B | Feuille séparée et nonce (Word, Excel) | `StyleSheetHref` donne un `<link>` et aucun `<style>` ; `StyleSheetNonce` porte le nonce ; `"><script>` refusé | Vérifié localement : tests Word et Excel (lien, nonce, 7 nonces refusés) |
| C | Images séparées | Avec `ImageUrlPrefix`, aucun `data:` dans la page, `src` = préfixe + nom, octets et type dans `Images` | Vérifié localement : PNG, JPEG, TIFF rendu en PNG, image partagée servie une fois |
| D | Preuve navigateur | Sous `artifacts/csp-proof/` (ignoré) : HTML d'avant produit par le code inchangé, HTML d'après avec `.css` et images séparés sous la politique stricte en `meta` ; rendu comparé dans un vrai navigateur | Vérifié dans le navigateur intégré (HTTP local) : 6 pages d'après sans violation CSP, styles calculés et boîtes identiques à la page d'avant pour chaque élément ; pages d'avant sous la même politique : feuille et image bloquées |
| E | Documentation | CHANGELOG (Unreleased) et README (conversion HTML) | Fait |
| F | HTML vers Word lit les classes de la page | `HtmlToWord.Convert` lit les règles à classe unique des éléments `style` de la page (ordre de la feuille, l'attribut `style` l'emporte) ; aller-retour Word vers HTML vers Word identique à celui de la page à attributs `style` ; feuille liée signalée, jamais lue | Vérifié localement : 7 tests (3 allers-retours sur les fixtures, ordre et priorité, feuille liée, bloc non fermé, surlignage) ; 5 des 6 premiers échouent sans le câblage des règles, le 7e (ajouté après la porte) passe dans le filtre `Html`, 189 sur 189. Choix : toute règle à classe unique est lue, règles de base comprises ; aucun test existant ne change |

## Validation

5 novembre 2026, `ylaunch.ps1 -t -c -s` : 2 638 tests, 0 échec, 97,05 % des lignes, porte CRAP passée (3 965 méthodes). Mutations ciblées du code de production (attribut `style` réécrit, nonce non vérifié, lien ignoré, préfixe ignoré) : 14 des 16 nouveaux cas échouent ; les 2 autres échouent aussi sous leur propre mutation, reproduite par l'orchestrateur le 5 novembre (nonce Excel retiré : le test du nonce Excel échoue ; règles générées non écrites : 14 tests HTML échouent). Après le lot F : 2 644 tests, 0 échec, 97,05 % des lignes, porte CRAP passée (3 974 méthodes).

## Limites connues

- Un consommateur qui n'insérait que le `body` de la page doit maintenant servir aussi la feuille (`StyleSheet`) : la mise en forme n'est plus portée par les éléments.
- HTML vers Word ne lit que les règles à classe unique des éléments `style` de la page (lot F) ; une feuille liée (`StyleSheetHref`) n'est jamais lue et le signale dans les gaps. `HtmlToWord.Append` (un arbre, pas une page) ne lit que les attributs `style`, comme avant.
- Les règles de base sont lues aussi : sur une page convertie avec `HighlightAddress`, le fond de surlignage (`.omni-highlight`) devient l'ombrage des segments du paragraphe surligné, ce que la page à attributs `style` ne faisait pas.
- Le rendu est identique dans la page autonome ; une feuille du consommateur de spécificité supérieure peut désormais l'emporter sur une mise en forme qui était auparavant en attribut `style`.
