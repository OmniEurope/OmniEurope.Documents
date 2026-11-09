# Revue du module Stress

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

Synthèse : les dix sources et manifestes du module sont lus intégralement et indexés ; aucun défaut pertinent confirmé. Les fixtures binaires partagées ont une inspection native propre sous Tests, dans metrics/fixture-inspection.json. Les générateurs à graines fixes et les assertions de contenu, de pixels et de succès sont réels, sans mocks ni retours constants de remplacement.

Cette revue ne prétend pas avoir exécuté la suite de stress. Son unique exécution finale appartient à l’orchestrateur. Les WaitAsync de RobustnessTests et Jpeg2000ScaleTests bornent l’attente sans interrompre le Task.Run sous-jacent ; les chronomètres de ScaleTests vérifient le temps après le calcul. Pour cette raison, l’isolation doit aussi porter un délai global qui termine le processus du conteneur. Cette limite du harnais n’est pas promue en défaut produit sans scénario défaillant actuel.

Invocation finale recommandée : image SDK locale confirmée par l’orchestrateur, sans pull, `--network none --cpus 4 --memory 4g --pids-limit 512`, sortie des tests montée en lecture seule et dossier de résultats monté en écriture sous le présent audit. Dans ce conteneur, `timeout 900s dotnet /tests/OmniEurope.Documents.StressTests.dll --report-trx --report-trx-filename stress.trx --results-directory /results`. Contrôler le code de sortie, l’absence de terminaison OOM/timeout, puis le TRX réel via verify-trx.ps1. Ne pas modifier les limites de temps des assertions pour obtenir un résultat vert. Les 24 tâches internes du test de déterminisme restent bornées par le conteneur, sans réglage de priorité ou d’affinité du poste.

PROPORTIONALITY: NONE. Les helpers de dommage et corpus partagés servent deux suites actuelles. L’alternative concrète de dupliquer leurs écritures de formats dans chaque test augmenterait le coût de maintenance sans rendre les attentes plus indépendantes ; leurs motifs et références restent explicitement séparés du code de production.

## tests/Shared/Jbig2Conformance.cs

Preuve : Lecture intégrale des 207 lignes ; manifeste déterministe, extraction des pages/globals, comparaison PBM indépendante par pixels et constructeur PDF lus ; les lectures sans borne concernent les fixtures de confiance, pas un chemin de production.

RAS


## tests/Shared/Jpeg2000Conformance.cs

Preuve : Lecture intégrale des 333 lignes en deux tranches ; parsing PGM plans16bits, contrôle échantillon par tolérance, extraction marqueurs de features et wrapper PDF relus ; parsers réservés aux fixtures de confiance.

RAS


## tests/Shared/Samples.cs

Preuve : Lecture intégrale des 57 lignes ; pixels synthétiques calculés depuis RGB indépendants, InOrder vérifie la position croissante et Pixels inspecte un rendu réel avec tolérance40 ; aucune assertion sur un retour mocké.

RAS


## tests/Shared/SampleDocuments.cs

Preuve : Lecture intégrale des 179 lignes ; documents synthétiques riches, valeurs de cellule et métadonnées déterministes, PDF six pages issu de trois écrivains, couleurs attendues indépendantes ; chaîne formule dangereuse utilisée comme donnée non exécutée.

RAS


## tests/OmniEurope.Documents.StressTests/Damage.cs

Preuve : Lecture intégrale des 123 lignes ; générateurs à graine fixe, allocations clonées bornées par entrée, mutations et troncatures réelles, ZIP de nouveau valide pour atteindre parseurs ; les entrées utilisées sont non vides.

RAS


## tests/OmniEurope.Documents.StressTests/Jbig2ScaleTests.cs

Preuve : Lecture intégrale des 30 lignes ; chaque gros dictionnaire passe le parcours PDF image Decode, puis comparaison indépendante de chaque pixel PBM et limite10s ; exécution gourmande réservée au conteneur limité.

RAS


## tests/OmniEurope.Documents.StressTests/Jpeg2000ScaleTests.cs

Preuve : Lecture intégrale des 66 lignes ; motif4096x3072 comparé intégralement, huit sources mutées150fois à graine fixe, null accepté mais décodage non nul exigé. WaitAsync borne attente et non tâche sous-jacente : exécution requiert conteneur et plafond global.

RAS


## tests/OmniEurope.Documents.StressTests/RobustnessTests.cs

Preuve : Lecture intégrale des 199 lignes ; sources saines exercées avant mutations, exceptions documentées limitées, succès réel exigé, parcours lecture/conversion réels sans mocks et graines fixes ; Timeouts ne tuent pas Task.Run, plafond conteneur global requis.

RAS


## tests/OmniEurope.Documents.StressTests/ScaleTests.cs

Preuve : Lecture intégrale des 165 lignes ; nombres48008 cellules/2000 pages/200000 CSV et contenu précis vérifiés, sept conversions comparées en série puis24 tâches, attentes indépendantes ; timings après exécution ne tuent pas un blocage, conteneur global requis.

RAS


## tests/OmniEurope.Documents.StressTests/OmniEurope.Documents.StressTests.csproj

Preuve : Lecture intégrale des 26 lignes ; exécutable MTP non empaquetable, trois dépendances test centralisées, helpers partagés et fixtures copiés hors scripts, seule référence core. La suite finale sera exécutée isolément en conteneur.

RAS
