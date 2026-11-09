# Conformité au kit

Référence : `develop`, `2594b1d7cd6edceedd330e2e72377fe3b7c989af`, lecture neuve du 9 novembre 2026. Aucune correction ni modification de politique.

## Type et structure

Bibliothèque .NET sans interface ni hôte, deux assemblages de production dans un seul NuGet, deux projets de tests. Le contrat propre au projet et le graphe réel priment les modèles Web/MAUI du kit. Les suffixes Back/Front/Shared, contrôleurs, DI, EF, UnitOfWork, Docker de déploiement et installateurs ne sont pas pertinents ici. Aucune structure supplémentaire n’est demandée pour reproduire un modèle d’application.

Sources lues : AGENTS.md et CLAUDE.md du projet, décision courante, solution et quatre csproj, props, global.json, README, registre des plans et plans enregistrés hors réserve 008. Références locales du kit : AGENTS.md, .editorconfig, docs/code-rules.md, coding-standards.md, architecture.md, project-architecture.md et roslyn-analyzers.md. L’outil MCP omnieurope n’étant pas exposé, ces sources locales ont été utilisées. Aucun contenu de PLAN-008 consulté.

## Documentation

AGENTS.md reste canonique, politique Git non vide, CLAUDE.md pointeur avec Session resume. README et plans décrivent les formats, leurs limites et les preuves historiques ; le code et la solution gouvernent leur structure. Les documents applicatifs du kit ne sont pas copiés artificiellement dans une bibliothèque. Aucun ADR ni overlay .claude/code-rules.md présent. L’absence de table documentaire formelle dans AGENTS.md ne cache pas les sources pertinentes : README et docs/plans/ y sont référencés explicitement. Le statut de version contradictoire du README est GOV-001, sans doublon ici.

## Conventions et câblage

| Règle | Résultat applicable et preuve |
|---|---|
| STD-PARTIAL | KIT-001 : couche analyzer GEN008 absente. Évaluation MSBuild réelle des deux productions, uniquement NetAnalyzers SDK. Le scan manuel ne trouve aucune violation actuelle. |
| STD-FILESIZE | KIT-002 : FileSizeAuditTests ou équivalent absent des suites ; scan manuel sans dépassement. |
| STD-CRAP | Copie scripts/crap-gate.ps1 identique au kit, SHA-256 `9a097f5eeee06cdba038bd2cd14edcc0fd4d01b356784243805e128fdd78c43a`. CI et launcher câblent le garde ; fichier exceptions vide, aucun seuil abaissé. Les scores actuels relèvent de metrics/crap_report.md et de la validation orchestrée. |
| STD-TESTPROOF | Les deux jobs CI réclament un TRX, executed>0 et aucun failed/error/timeout/aborted via verify-trx.ps1, relu intégralement. Les scripts sont analysés par le parseur PowerShell natif, zéro erreur de syntaxe. La CI hébergée n’a pas été exécutée par cette passe. |
| STD-TESTSCOPE | Aucune assertion de documentation ou définition de pipeline détectée par le vérificateur. Les tests PackageTests lus portent sur OPC et les flux, pas les conventions du dépôt. La revue complète des autres tests appartient au module Tests. |
| STD-SDKPIN | Floor 10.0.403, latestFeature, allowPrerelease=false. CI et publication dérivent global.json. Centralisation des trois versions de test ; exclusion Dependabot explicite et activée une fois, aucune exclusion muette. ILLink 10.0.14 est auto-référencé par le SDK, pas une dépendance du paquet ; pas de lockfile ni publication self-contained dans cette solution, la recette 4 de la règle ne s’applique pas. |
| STD-KITCOPY | verify-rules n’a détecté aucun écart de copie, hooks compris. Aucun défaut produit inventé sur un hook externe. |
| STD-CFGOVERRIDE | Aucun hôte ni endpoint runtime propre à la bibliothèque. NuGet.Config fixe une source de restauration, pas un endpoint runtime. |
| STD-OE, STD-STYLE, STD-I18N, STD-UIVERIFY, STD-FOCUS, STD-BTN, STD-DIALOG, STD-GRID, STD-FORM, STD-LABEL, STD-NAV, STD-GRIDTITLE, STD-TABS, STD-SPACE, STD-BUSY, STD-SHELL | Sans objet : aucune interface applicative. Le HTML généré comme document n’est pas un shell ou composant UI local à remplacer par OE. |
| STD-SWSCOPE, STD-IMMUTABLE, STD-BOOTFETCH, STD-PWAICONS, STD-OFFLINE, STD-WASMOPT | Sans objet : aucune application Web déployée dans le périmètre. La compatibilité WebAssembly d’une bibliothèque ne crée pas un manifeste PWA ou service worker à lui ajouter. |

GEN001/002 et GEN004 à007 correspondent à des couches EF, Razor ou API absentes. GEN003 vise le backend, absent ici. GEN008 reste explicitement applicable au code de production selon STD-PARTIAL. Aucun package d’analyse ni copie externe ajouté pendant cet audit.

Les conventions UTF-8/LF, indentation et namespace file-scoped de .editorconfig concordent avec le kit. Nullable, warnings-as-errors, XML public et déterminisme sont actifs. Les U2014 de LICENSE:116/278 appartiennent au texte juridique EUPL standard conservé intact, aucune correction typographique du texte tiers n’est prescrite.

## Vérification mécanisée réelle

Commande : `& C:/Dev/_Generic/verify-rules.ps1 -Root C:/Dev/OmniEurope/OmniEurope.Documents`, sortie 0, `RESULT: PASS`. Trace : metrics/verify-rules.log. Le script déclare 19 familles contrôlées et aucune occurrence restante ; il annonce l’unique exclusion STD-SDKPIN avec sa raison. Ce résultat prouve l’absence des motifs qu’il détecte, pas le respect sémantique de chaque règle ni un garde automatique de CI.

Évaluation sans build : `dotnet msbuild <production.csproj> -getItem:Analyzer -property:Configuration=Release -nologo`, sortie 0 pour les deux projets, traces metrics/evaluated-analyzers-core.json et evaluated-analyzers-fonts.json. Syntaxe des cinq scripts Governance : metrics/governance-syntax.json, zéro erreur.

## Écarts et limites

Deux écarts moyens de câblage, KIT-001 et KIT-002, aucun défaut source déduit de leur seule absence. Tout ajout d’analyseur exige une solution compatible avec la politique zéro dépendance livrée et sans copie de code externe ; tout changement de couche normative exige la décision du propriétaire. Le défaut de version du README et le retard de xunit restent dans leurs registres propres.

Anomalie d’instructions séparée du produit : docs/code-rules.md du kit mesure 86 325 octets, au-dessus de 20 Ko. Aucun hook divergent observé par le vérificateur.

PROPORTIONALITY: NONE. Le NuGet unique et la DLL de polices chargée explicitement répondent au besoin actuel de chargement différé ; l’alternative concrète d’une seule DLL chargerait ces données avec le cœur. Aucun ajout de couche applicative n’est nécessaire.
