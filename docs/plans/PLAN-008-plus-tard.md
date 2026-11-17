<!-- SPDX-License-Identifier: EUPL-1.2 -->
# PLAN-008 - Idées pour plus tard

Numéro réservé, hors numérotation dense : idées mises de côté, à ne traiter que sur demande explicite.

## Découplage des espaces de noms partagés

- **Polices vers conversion** : `Fonts/FontLibrary.cs` utilise `WordPdfGapCodes` (espace de noms
  `Conversion`) pour qualifier une substitution de police. Déplacer ces codes, ou la partie utilisée, dans
  `Fonts` pour que les polices ne dépendent plus des conversions.
- **Graphiques vers Excel** : `Charts/ChartReader.cs` utilise `NumberFormatter` (espace de noms `Excel`)
  pour mettre en forme les valeurs des axes et des étiquettes. Le couplage est léger et justifié ; à
  reconsidérer seulement si le formateur de nombres devient un module partagé.
- Critère d'acceptation : plus aucune directive `using` de `Fonts` vers `Conversion` ; suite rapide verte
  sur les deux cibles ; API publique inchangée.
