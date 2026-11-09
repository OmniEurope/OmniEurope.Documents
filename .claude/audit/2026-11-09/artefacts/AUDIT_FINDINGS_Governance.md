# Revue du module Governance

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

Synthèse du module : 30 fichiers lus intégralement, 30 preuves et index individuels enregistrés, un constat documentaire faible GOV-001. Le fichier de décisions ajouté à la livraison par le coordinateur est relu et indexé sans rouvrir les 29 fichiers déjà prouvés. Le retard de xunit (DEP-001 faible) relève du registre Dependencies ; deux écarts moyens de câblage STD-PARTIAL et STD-FILESIZE relèvent du registre Kit, sans violation source actuelle déduite de leur absence.

Preuves complémentaires réelles : vérificateur du kit sortie 0, parseur PowerShell sans erreur sur les cinq scripts, requêtes NuGet réseau sorties 0, gitleaks historique et arbre suivi sorties 0. Le journal verify-package réel a été relu. CI hébergée non exécutée par cette passe ; preuves de consommateur et suites consolidées par l’orchestrateur.

PROPORTIONALITY: NONE. Les scripts de contenu, TRX et provenance répondent aux contrôles actuels de livraison. L’alternative concrète de fusionner les scripts supprimerait leurs contrats distincts entre CI et publication sans supprimer de contrôle nécessaire.

## .config/verify-rules.json

Preuve : Lecture intégrale des 9 lignes ; exécution réelle du vérificateur du kit, sortie 0 : exclusion STD-SDKPIN activée une fois pour Dependabot, raison explicite, aucun filtre silencieux.

RAS


## .editorconfig

Preuve : Lecture intégrale des 35 lignes ; conventions UTF-8/LF, indentation XML/JSON et namespace file-scoped confrontees à coding-standards ; aucune règle abaissée ni suppression de diagnostic.

RAS


## .gitattributes

Preuve : Lecture intégrale des 25 lignes ; LF général, types binaires déclarés, exceptions -text Adobe AFM et schémas ECMA conformes aux ressources tierces autorisées dans AGENTS.md.

RAS


## .github/scripts/new-package-provenance.ps1

Preuve : Lecture intégrale des 40 lignes : validation SHA40 et identités positives, énumération stricte de deux paquets, SHA256 calculé sur fichiers et provenance écrite ; aucun secret intégré.

RAS


## .github/scripts/test-package-provenance.ps1

Preuve : Lecture intégrale des 46 lignes : repository/commit/workflow/run/attempt vérifiés, noms simples et liste exacte contrôlés, hash SHA256 réellement recalculé pour chacun des deux fichiers.

RAS


## .github/scripts/verify-package.ps1

Preuve : Lecture intégrale des 144 lignes ; contrôles nuspec sans dépendance, deux DLL/XML, versions assembly, PDB séparés et chemins déterministes, familles de polices et licences OFL. Exécution réelle du paquet suivie séparément par orchestration.

RAS


## .github/scripts/verify-trx.ps1

Preuve : Lecture intégrale des 15 lignes : absence de TRX ou executed=0 déclenche throw, failed/error/timeout/aborted contrôlés ; le script est effectivement appelé par les deux jobs CI (STD-TESTPROOF).

RAS


## .github/workflows/ci.yml

Preuve : Lecture intégrale des 85 lignes ; SDK dérivé global.json, actions SHA40, droits contents read, build warnings-as-errors, deux suites et vérification TRX, gate CRAP, contrôle du paquet et provenance liés à GITHUB_SHA/run.

RAS


## .github/workflows/publish-nuget.yml

Preuve : Lecture intégrale des 67 lignes ; tag résolu avant téléchargement, CI push successful pour SHA exact sélectionnée ; provenance repository/commit/run/attempt et version validés avant authentification OIDC et push. Publication non exécutée.

RAS


## .gitignore

Preuve : Lecture intégrale des 11 lignes ; sorties bin/obj, artifacts, TestResults et paquets ignorées, aucun masque sur src ou tests. Confrontation à inventaire Git, aucune source cachée par ces motifs.

RAS


## Directory.Build.props

Preuve : Lecture intégrale des 14 lignes : net10.0 réel, nullable actif, warnings erreurs, déterminisme et version commune aux deux DLL. Valeur TFM MCP netcoreapp1.0 exclue car contredite par ce manifeste.

RAS


## Directory.Packages.props

Preuve : Lecture intégrale des 11 lignes ; trois dépendances uniquement de test centralisées. Requête NuGet réelle --outdated --no-restore sortie0 : xunit.v3 4.0.2 résolu, 4.0.3 stable disponible.

DEP-001 : version xunit.v3 en retard dans les deux projets de tests, voir findings-Dependencies.json.


## global.json

Preuve : Lecture intégrale des 10 lignes ; floor SDK 10.0.403 latestFeature sans prerelease et runner Microsoft.Testing.Platform explicites. Les deux workflows CI et publication utilisent global-json-file.

RAS


## NuGet.Config

Preuve : Lecture intégrale des 7 lignes ; clear élimine sources héritées, source unique HTTPS NuGet v3 sans identifiant. Requête vulnérabilités réelle réseau autorisé sortie0 sur cette source.

RAS


## OmniEurope.Documents.slnx

Preuve : Lecture intégrale des 10 lignes ; quatre chemins projets existants : deux bibliothèques et deux suites de tests, arborescence /src /tests. Contrôle TFM par props et scans NuGet, quatre projets interrogés.

RAS


## README.md

Preuve : Lecture intégrale des 173 lignes ; périmètre sans UI ni OCR et restrictions explicites vérifiés contre plans et manifests ; statut 1.0.0 à la ligne7 contredit props1.1.0 et changelog daté1.1.0.

GOV-001 : statut de version documentaire obsolète, voir findings-Governance.json.


## CHANGELOG.md

Preuve : Lecture intégrale des 570 lignes en quatre tranches contiguës ; sections 1.1.0 du 7 novembre et 1.0.0 du 20 octobre distinguées ; limites explicites (OCR, glyphes, tableaux et rendu) traitées comme contrats acceptés, pas nouveaux défauts.

RAS


## docs/plans/README.md

Preuve : Lecture intégrale des 30 lignes ; registre dense 001 à010 hors réserve008, liens des plans actifs et archivés confrontés aux chemins présents. Aucun contenu de PLAN-008 consulté.

RAS


## docs/plans/archive/PLAN-006-ocr.md

Preuve : Lecture intégrale des 16 lignes ; statut abandonné et aucune implémentation annoncée, décision pas OCR cohérente avec README. Aucun faux finding sur une capacité volontairement absente.

RAS


## docs/plans/archive/PLAN-005-robustesse-et-fidelite.md

Preuve : Lecture intégrale des 67 lignes ; trois lots et différences avant/après identifiées, bornes mémoire et erreurs documentées, preuves historiques distinguées de la suite courante dont exécution relève orchestration.

RAS


## docs/plans/archive/PLAN-001-perimetre-initial.md

Preuve : Lecture intégrale des 198 lignes ; lots0 à16 et références aux limitations initiales relus comme historique, évolution changelog distinguée ; absence OCR et paquet zéro dépendance cohérents avec contrat actuel.

RAS


## docs/plans/archive/PLAN-002-couverture-95.md

Preuve : Lecture intégrale des 93 lignes ; sept lots, attentes indépendantes et références externes explicites, résultats historiques 96,81/87,21 et CRAP distingués des métriques en cours, aucune promesse de couverture actuelle déduite.

RAS


## docs/plans/archive/PLAN-003-tests-integration.md

Preuve : Lecture intégrale des 108 lignes ; sept lots, parcours publics et robustesse séparés ; emplacement historique de tests précisé comme déplacé vers StressTests en fin de plan, pas considéré lien produit actuel.

RAS


## docs/plans/archive/PLAN-004-fonctionnalites-suivantes.md

Preuve : Lecture intégrale des 436 lignes en quatre tranches contiguës ; quinze lots, limites et sources de preuves relus, notamment PDF/A/PAdES sans validation tierce et OCR transféré ; aucune de ces limites acceptées transformée en défaut nouveau.

RAS


## docs/plans/PLAN-007-corrections-audit-coherence.md

Preuve : Lecture intégrale des 43 lignes ; treize constats et validations locales distingués des CI/Office/AOT non exécutés ; masque JPEG/JPX et cellule exacte documentés comme limites acceptées, branche et version citées comme contexte historique.

RAS


## docs/plans/PLAN-009-controles-de-contenu-word.md

Preuve : Lecture intégrale des 29 lignes ; huit lots, critères et limites historiques lus ; deuxième DLL du même paquet, sans UI et chargement explicite des polices conformes aux manifests, validations passées non recyclées.

RAS


## docs/plans/PLAN-010-html-compatible-csp.md

Preuve : Lecture intégrale des 43 lignes ; six lots et contrats HTML/CSP relus, distinctions StyleSheetHref/Nonce et Images cohérentes avec README, preuves navigateur historiques explicites ; limites Append et CSS consommateur signalées sans nouvelle promesse.

RAS


## scripts/crap-gate.ps1

Preuve : Lecture intégrale des 222 lignes en deux tranches ; fusion des hits par chemin+ligne, refus Cobertura vide, exceptions plafond/raison et obsolescence contrôlés. Copie identique au kit par SHA256 9A097F5EEEE06CDBA038BD2CD14EDCC0FD4D01B356784243805E128FDD78C43A ; verify-rules sortie0.

RAS


## LICENSE

Preuve : Lecture intégrale des 287 lignes en deux tranches ; licence EUPL1.2 avec clauses et annexe complètes, cohérente avec PackageLicenseExpression. Les U2014 lignes116/278 appartiennent au texte juridique standard conservé intact, pas à une sortie rédigée.

RAS


## .claude/decisions.md

Preuve : Lecture intégrale des cinq lignes courantes : audit neuf sans correction ni publication, commit final de tout l'arbre explicitement autorisé sans push, puis livraison HTML acceptée sur contrôle statique avec affichage non vérifié après refus du protocole file: par le navigateur et sans contournement. Seule cette métadonnée de livraison est rafraîchie ; preuve antérieure conservée dans metrics/decisions-proof-before-html.json.

RAS
