# Revue du module Tests

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

Synthèse : 423/423 fichiers relus ou inspectés et indexés en mode Full, soit 154 sources C#, un projet, sept fichiers texte de corpus et licences, et 261 fixtures binaires. Aucun défaut pertinent nouveau confirmé dans les tests eux-mêmes. Les défauts produit déjà portés par les autres modules ne sont pas redoublés en manque de test.

Les 31 sources PDF ont été relues intégralement par /root/nov_architecture et les 30 sources Conversion restantes par /root/nov_coherence. Leurs rapports JSONL ont été lus ici, chaque réservation et chaque empreinte actuelle contrôlées avant Complete-AuditFile. Les preuves par fichier indiquent cette provenance déléguée ; aucune lecture personnelle des 61 sources n'est prétendue.

Les 261 binaires ont parcouru un inspecteur C# natif isolé, compilé contre la DLL Release existante sans reconstruire le produit. Les deux exécutions Docker bornées sont sorties à zéro, avec 260 puis une inspection réussie et aucune erreur (metrics/fixture-inspection.json et fixture-inspection-additional.json). JBIG2 et JPX sont décodés et comparés aux références PBM/PGM indépendantes ; images couleur comparées aux références de pixels et documents ouverts, lus et rendus. Chaque résultat conserve le hash du fichier inventorié. Les corruptions intentionnelles des corpus ne constituent pas un défaut sans assertion contradictoire.

La revue des sources établit la présence d'assertions de contenu, coordonnées, pixels, erreurs et budgets sur les parcours du produit. Les fabriques de formats et exemples à données fixes ne sont pas présentés comme des preuves Office, navigateur, validateur PDF/A complet ou confiance PKI externe. Les tests eux-mêmes n'ont pas été exécutés par les réviseurs de ce lot ; l'unique suite finale relève de l'orchestrateur. La suite dite rapide contient aussi des cas adversariaux volumineux ; la faire tourner dans le conteneur strictement limité préconisé pour Stress, avec délai global et contrôle du TRX réel, sans charge sur le poste.

PROPORTIONALITY: NONE. Les assembleurs binaires et XML indépendants servent actuellement les entrées limites que les auteurs du produit ne savent pas générer. L'alternative concrète d'employer uniquement ces auteurs pour les fixtures supprimerait cette indépendance et plusieurs témoins invalides ; les helpers partagés existants évitent déjà les répétitions simples.


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2ConformanceTests.cs

Preuve : Lecture intégrale des 133 lignes ; 111 pages classées par segments effectivement présents, décodage direct et chemin PDF globals, dimensions et pixels indépendants PBM contrôlés. Pas de doubles ni de résultat constant remplaçant décodage.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jpeg2000/Jpeg2000ConformanceTests.cs

Preuve : Lecture intégrale des 130 lignes ; 69 échantillons et tolérances0/1, chaque feature vérifiée dans flux réel, plans signés et alpha comparés aux références indépendantes ; mutations négatives prouvent que Mismatch refuse pixel ou plan erroné.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/LICENSE.txt

Preuve : Lecture intégrale des 36 lignes ; origine et commit du corpus, statut données de test non livrées, référence annexH et licence BSD2 avec copyright/disclaimer complets. Aucun code tiers incorporé dans production par cette notice.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/manifest.txt

Preuve : Lecture intégrale des 118 lignes ; association explicite sample/page/PBM, annexH trois pages et mêmes deux gros dictionnaires référencés au bitmap indépendant, exceptions de référence documentées ; chaque ligne sera confrontée au décodage natif du corpus.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/ImageFixtures.cs

Preuve : Lecture intégrale des 50 lignes ; dimensions37x23 et motifs RGB/alpha/gris/palette calculés indépendamment, référence BGR convertie par ordrecanaux avant MAE/max, chaque pixel est effectivement pris en compte.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/JpegDecoderTests.cs

Preuve : Lecture intégrale des 114 lignes ; six références BGR indépendantes, coefficients progressifs comparés, CMYK Windows avec MAE<2,5/max<48, troncature acceptée et en-têtes refusés précisément ; motif2pour3 dérivé deT81.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/LICENSE.txt

Preuve : Lecture intégrale des 50 lignes ; provenance des corpus, transformations de marqueurs et créations synthétiques séparées, BSD2 et EUPL explicites, PGM présentés comme références et absence de livraison des données dans NuGet.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/manifest.txt

Preuve : Lecture intégrale des 93 lignes ; 69 associations groupe/flux/plans/tolérance0ou1 avec provenance et écarts références décrits, signed/subsampled/alpha explicités ; fichiers RGB de contrôle couleur distingués des plans bruts.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/successive-approximation.scans

Preuve : Lecture intégrale des 12 lignes ; script de scans progressifs DC/AC avec raffinementAh/Al successif cohérent avec le scénario Same_coefficients_decode_to_the_same_pixels sur progressive-refine.jpg ; fichier de données, pas un exécutable.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/LosslessDecoderTests.cs

Preuve : Lecture intégrale des 135 lignes ; fixtures externes RGB/palette/alpha/gris/bilevel comparées pixel par pixel aux motifs calculés, corruption PNG refusée, préfixes CCITT uniques ; allerretour encode/decode ne remplace pas preuves externes.

RAS


## tests/OmniEurope.Documents.Tests/Csv/CsvWriterTests.cs

Preuve : Lecture intégrale des 101 lignes ; sorties RFC4180 littérales, neutralisation formule, BOM exact une fois par encodage, culture invariante et refus Never couverts ; allerretour complété par attendus byte/texte indépendants.

RAS


## tests/OmniEurope.Documents.Tests/Csv/CsvReaderTests.cs

Preuve : Lecture intégrale des 246 lignes ; FEC réel mémoire BOM/1252/UTF8, quotes/CRLF/chunks1caractère, limites inclusives, asynchrone et propriété des flux après échec vérifiés par assertions précises ; doubles ciblés ne simulent pas HTTP.

RAS


## tests/OmniEurope.Documents.Tests/Csv/CsvDetailTests.cs

Preuve : Lecture intégrale des 111 lignes ; cas blanklines/délimiteursquotes, BOMtexte/encodageforcé, écriture flush réelle, UTF8tail et bornes Windows1252 testés par valeurs exactes ; aucun succès décoratif sans assertion.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jpeg2000/Jpeg2000PdfTests.cs

Preuve : Lecture intégrale des 170 lignes ; chemin JPX PDF réel, espacecouleur/alpha/prémultiplié/Indexed, références RGB souséchantillonnées tolérance1, rendu et refusHT avec gap vérifiés ; plans indépendants complètent tests directs de codec.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/annex-h.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 64x56, reference=annex-h-page-1.pbm, direct+PDF pixels exacts; page=2, 64x56, reference=annex-h-page-1.pbm, direct+PDF pixels exacts; page=3, 37x8, reference=annex-h-page-3.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-and-xnor-halftone.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-and-xnor-refine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-and-xnor-text.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-and-xnor.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-or-xor-replace-halftone.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-or-xor-replace-refine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-or-xor-replace-text.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-composite-or-xor-replace.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-customat-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-customat.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-10bpp-mmr.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-10bpp.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-composite.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-global.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-grid.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-refine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-skip-dummy.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-skip-grid-template1.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-skip-grid-template2.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-skip-grid-template3.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-skip-grid.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-template1.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-template2.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone-template3.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-halftone.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-initially-unknown-size.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-mmr.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-p32-eof.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-randomaccess.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-customat-tpgron.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-customat.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-lossless.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-page-subrect.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-page.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-refine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-template1-tpgron.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-template1.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine-tpgron.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-refine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-stripe-initially-unknown-height.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-stripe-last-implicit.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-stripe-single-no-end-of-stripe.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-stripe-single.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-stripe.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-32bit-arithint.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-big-segmentid.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-context-reuse-huffman-refagg.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-context-reuse-refagg.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-context-reuse.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-empty.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-global.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-manyrefs.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-negative-sbdsoffset.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-refine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symbolrefine-textrefine-export.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symbolrefine-textrefine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symbolrefineone-customat.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symbolrefineone-template1.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symbolrefineone.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symbolrefineseveral.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuff-texthuff.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuff-texthuffB10B13.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuffB5B3-texthuffB7B9B12.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuffcustom-texthuffcustom.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuffrefine-textrefine-export.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuffrefine-textrefine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuffrefineone.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuffrefineseveral.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-symhuffuncompressed-texthuff.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textbottomleft.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textbottomlefttranspose.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textbottomright.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textbottomrighttranspose.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textcomposite.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuff-runcodes32-34.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuff-trailingsymbols.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefineB15.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefinecustom.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefinecustomdims.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefinecustompos-global.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefinecustompos.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefinecustomposdims.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texthuffrefinecustomsize.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textrefine-customat.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textrefine-negative-delta-width.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-textrefine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texttopright.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texttoprighttranspose.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol-texttranspose.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-symbol.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template1-customat-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template1-customat.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template1-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template1.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template2-customat-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template2-customat.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template2-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template2.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template3-customat-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template3-customat.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template3-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-template3.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-tpgdon.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-trailing-7fff-stripped-harder-refine.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-trailing-7fff-stripped-harder.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap-trailing-7fff-stripped.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Samples/bitmap.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Large/bitmap-symbol-manysyms.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Large/bitmap-symbol-symhuff-texthuff-manysyms.jbig2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; page=1, 399x400, reference=bitmap.pbm, direct+PDF pixels exacts ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Expected/annex-h-page-1.pbm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PBM P4 lu en entier, 64x56, 2 comparaison(s) de pages associees ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Expected/annex-h-page-3.pbm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PBM P4 lu en entier, 37x8, 1 comparaison(s) de pages associees ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jbig2/Expected/bitmap.pbm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PBM P4 lu en entier, 399x400, 110 comparaison(s) de pages associees ; Contexte assertions Jbig2ConformanceTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-01-bypass-finer-layers.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-01-bypass-layers.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-01-bypass.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-02-resetprob.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-04-termall-layers.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-04-termall.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-05-bypass-termall.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-06-resetprob-termall.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-08-vcausal.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-16-pterm.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-32-segsym.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-36-termall-segsym.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-59-all-but-termall.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-cbstyle-63-all.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/rgba-u8-solid-alpha-cbstyle-04-termall.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba-u8-solid-alpha-cbstyle-04-termall.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/cmyk-small.jpf

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=cmyk-small.pgm, 32 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/indexed-small.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=indexed-small.pgm, 18 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-cmyk-u8-prog1-layers1-res6.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=lossless-cmyk-u8-prog1-layers1-res6.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-cmyka-u8-prog1-layers1-res6.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 5 plans, tolerance=0, reference=lossless-cmyka-u8-prog1-layers1-res6.pgm, 60095 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-gray-alpha-u8-prog1-layers1-res6.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 2 plans, tolerance=0, reference=lossless-gray-alpha-u8-prog1-layers1-res6.pgm, 24038 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-gray-u8-prog1-layers1-res6-icc.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 1 plans, tolerance=0, reference=gray.pgm, 12019 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-gray-u8-prog1-layers1-res6.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 1 plans, tolerance=0, reference=gray.pgm, 12019 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-lab-alpha-u8-prog1-layers1-res6.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=lossless-lab-alpha-u8-prog1-layers1-res6.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-lab-u8-prog1-layers1-res6.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=lossless-lab-u8-prog1-layers1-res6.pgm, 36057 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgb-u8-prog1-layers1-res6-mct.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=lossless-rgb-u8-prog1-layers1-res6-mct.pgm, 36057 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u16-prog1-layers1-res6.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=lossless-rgba-u16-prog1-layers1-res6.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog1-layers1-res6-mct.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-bgra-u8.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-indexed-u8-rgb-u8.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=lossless-indexed-u8-rgb-u8.pgm, 36057 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u4.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=lossless-rgba-u4.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/gray16-a.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 1 plans, tolerance=0, reference=gray16-a.pgm, 14352 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/palette-b.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=palette-b.pgm, 34191 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/subsampled-410-a.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=subsampled-410-a.pgm, 16146 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/subsampled-420-cprl.j2k

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=pattern-420.pgm, 21528 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/subsampled-420-b.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=subsampled-420-b.pgm, 17205 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/subsampled-420-a.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=pattern-420.pgm, 21528 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/subsampled-422-a.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=subsampled-422-a.pgm, 28704 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/subsampled-440-b.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=subsampled-440-b.pgm, 22925 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/yuva420-a.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=yuva420-a.pgm, 35880 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/signed-gray.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 1 plans, tolerance=0, reference=gray.pgm, 12019 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/buggie-gray.jpf

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 1 plans, tolerance=1, reference=buggie-gray.pgm, 8832 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossy-rgba-u8-prog0-layers1-res6-mct.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=1, reference=lossy-rgba-u8-prog0-layers1-res6-mct.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossy-quantization-scalar-derived.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=1, reference=lossy-quantization-scalar-derived.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/irreversible-rgb-b.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=1, reference=irreversible-rgb-b.pgm, 34191 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/irreversible-rgb-a.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=1, reference=irreversible-rgb-a.pgm, 43056 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-PLT.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog0-EPH-empty-packets.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog0-EPH-SOP.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog0-EPH.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog0-SOP.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-TLM.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-RGN.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/packed-ppm-tiles.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=fractal-rgb.pgm, 34191 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/packed-ppm.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/packed-ppt-tiles.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=fractal-rgb.pgm, 34191 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/packed-ppt.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/tile4x2-res5.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog0-tile-part-index-overflow.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog0-tile4x2-cblk4x16-tp3-layers3-res2.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog1-tile4x2-cblk4x16-tp3-layers3-res2.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog2-tile4x2-cblk4x16-tp3-layers3-res2.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog3-tile4x2-cblk4x16-tp3-layers3-res2.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/lossless-rgba-u8-prog4-tile4x2-cblk4x16-tp3-layers3-res2.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/layers-b.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=layers-b.pgm, 34191 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/tiles-sop-eph-b.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=fractal-rgb.pgm, 34191 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/offset-origin.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/poc-main.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/poc-tile.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 4 plans, tolerance=0, reference=rgba.pgm, 48076 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Samples/tile-cod.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; JPX decode, 3 plans, tolerance=0, reference=fractal-rgb.pgm, 34191 echantillons compares ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/buggie-gray.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 64x138/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/cmyk-small.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 4x2/max255, 4x2/max255, 4x2/max255, 4x2/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/fractal-rgb.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 131x87/max255, 131x87/max255, 131x87/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/gray.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/gray16-a.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max65535 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/indexed-small.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 3x2/max255, 3x2/max255, 3x2/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/irreversible-rgb-a.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 156x92/max255, 156x92/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/irreversible-rgb-b.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 131x87/max255, 131x87/max255, 131x87/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/layers-b.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 131x87/max255, 131x87/max255, 131x87/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-cmyk-u8-prog1-layers1-res6.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-cmyka-u8-prog1-layers1-res6.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-gray-alpha-u8-prog1-layers1-res6.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-indexed-u8-rgb-u8.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-lab-alpha-u8-prog1-layers1-res6.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-lab-u8-prog1-layers1-res6.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-rgb-u8-prog1-layers1-res6-mct.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-rgba-u16-prog1-layers1-res6.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max65535, 119x101/max65535, 119x101/max65535, 119x101/max65535 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossless-rgba-u4.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max15, 119x101/max15, 119x101/max15, 119x101/max15 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossy-quantization-scalar-derived.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/lossy-rgba-u8-prog0-layers1-res6-mct.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/palette-b.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 131x87/max255, 131x87/max255, 131x87/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/pattern-420.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 78x46/max255, 78x46/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/rgba-u8-solid-alpha-cbstyle-04-termall.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/rgba.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 119x101/max255, 119x101/max255, 119x101/max255, 119x101/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-410-a.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 39x23/max255, 39x23/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-420-a.rgb.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 156x92/max255, 156x92/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-420-b.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 131x87/max255, 66x44/max255, 66x44/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-420-b.rgb.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 131x87/max255, 131x87/max255, 131x87/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-420-cprl.rgb.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 156x92/max255, 156x92/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-422-a.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 78x92/max255, 78x92/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-422-a.rgb.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 156x92/max255, 156x92/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/subsampled-440-b.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 131x87/max255, 131x44/max255, 131x44/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/yuva420-a.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 78x46/max255, 78x46/max255, 156x92/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Expected/yuva420-a.rgb.pgm

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PGM concatene lu en entier : 156x92/max255, 156x92/max255, 156x92/max255 ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Jpeg2000/Large/large-4096x3072.jp2

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; PDF JPX 4096x3072, 37748736 composantes comparees au motif independant ; Contexte assertions Jpeg2000ConformanceTests/PdfTests et helpers relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/baseline.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/bilevel-ccitt3.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 1800x22, Gray, 39600 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/bilevel-ccitt4.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 1800x22, Gray, 39600 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/bilevel-none.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 1800x22, Gray, 39600 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/bilevel-rle.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 1800x22, Gray, 39600 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/bilevel.png

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 1800x22, Gray, 39600 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/cmyk-plain.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Cmyk, 3404 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/cmyk-ycck.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Cmyk, 3404 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/gray.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Gray, 851 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/gray.png

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Gray, 851 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/indexed-lzw.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/indexed.bmp

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/indexed.gif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgba, 3404 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/indexed.png

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/plain.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/progressive-refine.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/progressive-restart.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/progressive.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/restart.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgb-lzw.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgb-none.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgb-rle.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgb-zip.tif

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgb.bmp

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgb.png

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgba-interlaced.png

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgba, 3404 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/rgba.png

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgba, 3404 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/s422-progressive.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/s444.jpg

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; Decode natif 37x23, Rgb, 2553 octets pixels ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/baseline.jpg.reference.bgr

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; BGR24 integral, 37x23, MAE=0.103016, max=3, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/cmyk-plain.jpg.reference.cmyk

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; CMYK integral, 3404 octets, MAE=0.033196, max=1, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/cmyk-ycck.jpg.reference.cmyk

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; CMYK integral, 3404 octets, MAE=0.072855, max=2, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/gray.jpg.reference.bgr

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; BGR24 integral, 37x23, MAE=0.000000, max=0, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/progressive-restart.jpg.reference.bgr

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; BGR24 integral, 37x23, MAE=0.030161, max=2, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/restart.jpg.reference.bgr

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; BGR24 integral, 37x23, MAE=0.030161, max=2, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/s422-progressive.jpg.reference.bgr

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; BGR24 integral, 37x23, MAE=0.303564, max=2, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Images/s444.jpg.reference.bgr

Preuve : Inspection C# native en conteneur 2CPU/2Go, sortie0 ; BGR24 integral, 37x23, MAE=0.028202, max=2, compare au JPEG associe ; Contexte assertions LosslessDecoderTests/JpegDecoderTests et ImageFixtures relus integralement. Trace fixture-inspection.json, empreinte identique inventaire.

RAS


## tests/OmniEurope.Documents.Tests/Csv/TextValueParserTests.cs

Preuve : Lecture intégrale des 68 lignes ; montants séparateursfr/en et signes calculés indépendamment, dates invalides refusées, durées exactes et extrêmes horsTimeSpan ciblés ; valeurs attendues ne dépendent pas du parseur testé.

RAS


## tests/OmniEurope.Documents.Tests/Conversion/WordToPdfTests.cs

Preuve : Lecture intégrale des 358 lignes en deux tranches ; headers/fields/veuves/notes/tableaux/colonnes et tabulations vérifiés par texte et positions PDF réels ; external.docx apporte phrases attendues indépendantes, gaps limités explicitement.

RAS


## tests/OmniEurope.Documents.Tests/Pdf/PdfReaderTests.cs

Preuve : Lecture intégrale des 262 lignes en deux tranches ; browser.pdf externe deuxpages et images relus, filtres/vecteurRC4/objstreams incrémentaux et réparation couverts ; U2014 est une donnée Unicode volontaire de métadonnées, aucune anomalie fonctionnelle.

RAS

## tests/OmniEurope.Documents.Tests/Fixtures/Pdf/source.html

Preuve : Lecture intégrale des 17 lignes ; source synthétique française du PDF navigateur avec accents, grec/cyrillique, tableau et secondepage explicites, image2x2 embarquée. Correspond aux attentes indépendantes de PdfReaderTests relues.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Word/source.html

Preuve : Lecture intégrale des 20 lignes ; source synthétique du DOCX externe avec miseenforme/listes/fusion/table/image et phrase multilingue attendues dans WordToPdfTests ; lien exemple non sensible, aucun accès réseau exécuté.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordDocumentTests.cs

Preuve : Lecture intégrale des 272 lignes en deux tranches ; structure/format/listes/notes/images et source DOCX externe vérifiés par valeurs indépendantes, macro/DTD/VBA et profondeur100000 refusés ; aucun skip ou assertion décorative.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Word/external.docx

Preuve : Inspection native reelle conteneur borne, sortie0 : Word charge : 1 section(s), 242 caracteres ; ZIP integral 12 parties, 11 XML parses. Contextes WordDocumentTests/WordToPdfTests/PdfReaderTests et sources HTML lus integralement. Trace fixture-inspection*.json.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Pdf/browser.pdf

Preuve : Inspection native reelle conteneur borne, sortie0 : PDF ouvert, 2 pages, 413 caracteres extraits, images decodees et chaque page rendue a18dpi. Contextes WordDocumentTests/WordToPdfTests/PdfReaderTests et sources HTML lus integralement. Trace fixture-inspection*.json.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Word/picture.png

Preuve : Inspection native reelle conteneur borne, sortie0 : Image decodee integralement : 37x23, Rgb, 2553 octets pixels. Contextes WordDocumentTests/WordToPdfTests/PdfReaderTests et sources HTML lus integralement. Trace fixture-inspection*.json.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordContentControlTests.cs

Preuve : Lecture intégrale des 474 lignes en trois tranches ; dix contrôles de fixture, dates/langue/placeholder/format, listes libres vs fermées, checkbox et dotxversdocx sauvegardés puis relus en PDF ; contrôles anonymisés externes vérifiés par6/2/4 et dates.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Word/rent-receipt-template.dotx

Preuve : Inspection C# conteneur, sortie0 : Word charge : 1 section(s), 621 caracteres ; ZIP integral 22 parties, 21 XML parses. Contexte integral WordContentControlTests : sixcontroles anonymises listes/dates, remplissage puis ToDocumentArray et conversionPDF, sans testconstant.

RAS


## tests/OmniEurope.Documents.Tests/Diff/TextDiffTests.cs

Preuve : Lecture intégrale des 130 lignes ; 400 couples à grainefixe reconstruisent B et distance minimale comparée à LCS indépendant, entrées60000 et cas lignes/mots/vides ; le nom mémoire nest pas interprété comme mesure de consommation.

RAS


## tests/OmniEurope.Documents.Tests/Html/HtmlDomTests.cs

Preuve : Lecture intégrale des 107 lignes ; sélecteurs opérateurs/fratrie, hiérarchie parent/cycles, échappement texteattribut versus script brut et réparations ciblées donnent résultats littéraux ; limites des sélecteurs non étendues au-delà contrat actuel.

RAS


## tests/OmniEurope.Documents.Tests/Html/HtmlParserTests.cs

Preuve : Lecture intégrale des 111 lignes ; document head/body, réparations de fragments et rawtext, hiérarchieDOM et sélecteurs ciblés vérifiés par sorties exactes ; entréeprofonde20000 vérifie texte préservé, pas preuve navigateur générale.

RAS


## tests/OmniEurope.Documents.Tests/Html/HtmlSanitizerTests.cs

Preuve : Lecture intégrale des 61 lignes ; vecteurs URI obscurcis, scripts/styles/SVG/iframe/form et encodage sont comparés à HTML exact, policydataURL limitée àimgsrc et conservationenfants ciblée ; aucune preuve navigateur revendiquée.

RAS


## tests/OmniEurope.Documents.Tests/Markdown/MarkdownWriterTests.cs

Preuve : Lecture intégrale des 61 lignes ; textes échappés relus en MarkdownText uniquement et égalité complète, listes/tableaux comparés auHTML attendu, sautsdanscellule testent structure conservée ; aucun accès documents du dépôt.

RAS


## tests/OmniEurope.Documents.Tests/Markdown/CommonMarkTests.cs

Preuve : Lecture intégrale des 155 lignes ; tables entrée/sortie indépendantes couvrent blocs/enligne/références/emphases/échappements et HTMLbrut seulement avec option explicite ; chaque théorie compare chaîneHTML complète, sans lecture de documentation.

RAS


## tests/OmniEurope.Documents.Tests/Markdown/MarkdownSafetyAndGfmTests.cs

Preuve : Lecture intégrale des 138 lignes ; HTMLbrut échappé par défaut, schémasURI dangereux retirés, tableaux/tâches/liensGFM et arbreSyntaxe attendus littéralement, limitesprofondeur128 exercées sans ignorer contenu ; pas de preuve navigateur implicite.

RAS


## tests/OmniEurope.Documents.Tests/Text/EncodingTests.cs

Preuve : Lecture intégrale des 64 lignes ; allerretour256octets1252 complété de caractères normatifs explicites, fallback horsrépertoire attendu, BOM/détection et UTF8surrogates/overlong/horsUnicode/incomplet refusés selon états distincts.

RAS


## tests/OmniEurope.Documents.Tests/Pdf/PdfPageComparisonTests.cs

Preuve : Lecture intégrale des 172 lignes ; rectangles indépendants donnent0/60/800pixels, ratioencre/blanc, taillesdifférentes, diffPNGrouge, compositingalpha et refus options/pixellimit couverts ; PDFWord365 autocohérence complété par contrôles négatifs réels.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Word/rent-receipt-template.word365.pdf

Preuve : Lecture native en conteneur borne sortie0 : PDF ouvert, 1 pages, 642 caracteres extraits, images decodees et chaque page rendue a18dpi. Contexte complet PdfPageComparisonTests : autocohérence export Word365 avec encre nonvide, plus différences négatives de formes indépendantes. Trace fixture-inspection.json.

RAS


## tests/OmniEurope.Documents.Tests/Conversion/WordInkTests.cs

Preuve : Lecture intégrale des 288 lignes en deux tranches ; unités/prefixesInkML calculés, pixels vectoriels aux positions attendues, fallback/millionpoints/valeurinfinie et sauvegarde vérifiés. DOCXréférence comparé au PDFWord365 sur zoneencre avec tolérance2points/2pourcent.

RAS


## tests/OmniEurope.Documents.Tests/Fixtures/Word/rent-receipt-template.docx

Preuve : Inspection native en conteneur sortie0 : Word charge : 1 section(s), 621 caracteres ; ZIP integral 22 parties, 21 XML parses. Contexte complet WordInkTests : referenceDOCX convertie, encre rendue et comparée aux pixels du PDFWord365, sans confusion avec simple compile. Trace fixture-inspection.json.

RAS


## tests/OmniEurope.Documents.Tests/Fonts/FontFileTests.cs

Preuve : Lecture intégrale des 95 lignes ; modifications ciblées du répertoire OpenType (TTC offsets, UPEMzero, postabsent, fsTypeembedding) suivies de lecture/subset et résultats exacts, ressourcefontstream correctement libérée.

RAS


## tests/OmniEurope.Documents.Tests/Fonts/DocumentFontsTests.cs

Preuve : Lecture intégrale des 209 lignes en deux tranches ; 29faces réelles, glyphes/outlines/widths, conversionPDF et usages de police inspectés, licences/notice et SHA256 testés comme données publiques embarquées, direction référencescore-extra vérifiée.

RAS


## tests/OmniEurope.Documents.Tests/Fonts/FontTests.cs

Preuve : Lecture intégrale des 256 lignes en deux tranches ; glyphesUE/fallback/mesuresArial, sousensemblechecksum/outlines et cmapformats0/6/12, chevauchementsbornés prouvent budgets réels. U2014 appartient volontairement au corpus de glyphes de ponctuation.

RAS

## tests/OmniEurope.Documents.Tests/OmniEurope.Documents.Tests.csproj

Preuve : Lecture intégrale des 29 lignes ; executableMTP nonpackable, trois packages de test sans version locale, ressources copiées horsPS1, helpers partagés et deux refsproduction explicites. Aucune suppressionwarnings/test ou package livré ajouté.

RAS


## tests/OmniEurope.Documents.Tests/Internal/PackageTests.cs

Preuve : Lecture intégrale des 116 lignes effectuée dans cette même passe Governance puis revérifiée : OPC contenu/rels/chemins/Officeescapes et plafonds ZIP testés, préfixe lu sync/async avec dispose ; ce nest pas un garde de documents du dépôt.

RAS


## tests/OmniEurope.Documents.Tests/Word/DocxFactory.cs

Preuve : Lecture intégrale des 58 lignes ; packagesDOCX minimaux construits directement en XML/ZIP sans écrivain testé, extra/binaryparts et contenttypes paramétrés pour cas normatifs ou invalides explicites ; extraction lit tous les flux et les libère.

RAS


## tests/OmniEurope.Documents.Tests/Word/ContentControlTemplate.cs

Preuve : Lecture intégrale des 73 lignes ; template synthétique sans données privées, XML minimal indépendant des écrivains, quatre dates et listes/tagspartagés/header/checkbox/textebloc préparent assertions WordContentControlTests ; valeurs toujours déterministes.

RAS


## tests/OmniEurope.Documents.Tests/Integration/CoveragePathsTests.cs

Preuve : Lecture intégrale des 34 lignes ; labelséchappés/incomplets calculés attendu, JPEGAPP0/1/14 inconnu injecté réellement puis pixels/dimensions/DPI comparés au source. Le nom CoveragePaths ne remplace pas assertions de comportement.

RAS


## tests/OmniEurope.Documents.Tests/Integration/ExcelWorkflowTests.cs

Preuve : Lecture intégrale des 101 lignes ; chaque cellule/style/settings sauvegardésrelus, CSVfr et importvaleurs exactes, Word/PDF publics relus avec ordre/dates/totaux et débordementnombres ; chaineCMD demeure donnée, aucun processus exécuté.

RAS


## tests/OmniEurope.Documents.Tests/Integration/MarkupWorkflowTests.cs

Preuve : Lecture intégrale des 141 lignes ; Markdownécrit puis AST/HTML/Word/PDF publics relus avec texte/structure/ordre, vecteurs malveillants nettoyés et destinationsscriptabsentes. Contrôle PDFbrut limité aux sorties produites, pas un scanner universel.

RAS


## tests/OmniEurope.Documents.Tests/Integration/PdfWorkflowTests.cs

Preuve : Lecture intégrale des 92 lignes ; troisécrivains fusionnés puis extraction/split/reconstruction/rotation/tampon/compression réels, texte/pages/images/pixels et préfixe incrémental vérifiés ; couleurs et ordre indépendants du lecteur.

RAS


## tests/OmniEurope.Documents.Tests/Integration/WordWorkflowTests.cs

Preuve : Lecture intégrale des 167 lignes ; rapport public complet sauvegarde/remplacement/append/fusion puis PDFextraction et pixels réels, ordre/notes/listes/orientations/ChangedParts et adresse retrouvée vérifiés ; fixtures synthétiques indépendantes des lecteurs.

RAS


## tests/OmniEurope.Documents.Tests/Integration/PublicApiCoherenceTests.cs

Preuve : Lecture intégrale des 217 lignes ; régressions CSVshortreads/fallbacks, quatre vues lignesWord supprimées, datesPDFcalendriers, héritageétatForm et gapsMarkdown attendus précisément. Culture restaurée enfin, lecteurs ciblés sans fakeHTTP.

RAS


## tests/OmniEurope.Documents.Tests/Integration/RemainingAuditRegressionTests.cs

Preuve : Lecture intégrale des 212 lignes ; plagesvides/dimensions Excel, arrondisextrêmes/totauxindépendants, allocationsactuelles bornées64MiB, masquescouleur/stencils/axes et caviardage vérifiés par résultat/pixels/gaps, aucune exception avalée pour verdir.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/RasterImageTests.cs

Preuve : Lecture intégrale des 51 lignes ; taille/payload invalides refusés, alphaopaquevs254, RGBA et lumaBT601 attendus exacts, CMYKconversion explicitementunsupported et limiteJPEG65535 ; aucun grosbuffer alloué sur cheminrefus attendu.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/EncoderAndInfoTests.cs

Preuve : Lecture intégrale des 81 lignes ; JPEGsourcegradient indépendant comparé MAEselonqualité, taillesqualité/gris, identification cinqformats versusdimensions connues et refusdonnéesinconnues ; allerretour complété par références externes des autres tests.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/ImageInfoTests.cs

Preuve : Lecture intégrale des 91 lignes ; headersWebP/PNGpHYs/JFIF construitsdprèsformats avec dimensionsDPI calculés, bornesheadersabîmés ; CRCfictifs déclarés explicitement car cheminidentify ne décodepas, aucun faux fichiervalide présenté.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/PngFixture.cs

Preuve : Lecture intégrale des 80 lignes ; constructeurPNG indépendant IHDR/IDATzlib/IEND et chunksCRC32 normatif, dispose correct avantlecturebuffer, paramètres permettent profondeurs/interlace/filtrages sans réutiliser encodeurproduction.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/PngDecoderTests.cs

Preuve : Lecture intégrale des 119 lignes ; predictorsAverage/Paeth attendus calculés, 16bitsHighbyte, tRNSgris/RGB/palette, Adam7single et CMYKsortieRGB exacts ; donnéesforgées refusées par type/message, hasard fixé7 complémente référencesexternes.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/GifFixture.cs

Preuve : Lecture intégrale des 78 lignes ; GIF89a synthétique palette locale quatreRGB, descripteur/interlace explicites, LZWclear/littéraux/end largeur3à12 bits et sousblocs255octets écrits indépendamment, terminaisonpartielle flushée.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/GifDecoderTests.cs

Preuve : Lecture intégrale des 99 lignes ; GIF89a bytesnormatifs, GCEtransparence, passinterlace et largeurLZWpleine testés par chaque pixel, gigantisme refuséavantalloc, troncatures et codesinvalides refusés par messages distincts.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/BmpRleTests.cs

Preuve : Lecture intégrale des 106 lignes ; RLE8/RLE4 bytesindépendants avec littéraux/padding/delta/terminaisons, pixels attendus explicites sauf positionsnormativementindéfinies, troncatures conservent pixel connu ; pas de données abîmées présentées comme complètes.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/BmpDecoderTests.cs

Preuve : Lecture intégrale des 170 lignes ; BMPcore/555/565/V5alphamasks/topdown/DIB/RLE et donnéescourtes décrits avec pixelsattendus exacts, divergencesréférence déclarées ; fichiers invalides refusés par exceptions précises, pas de fauxcodec.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/PredictorAndFaxTests.cs

Preuve : Lecture intégrale des 80 lignes ; prédicteurTIFF RGB8 et gris16 deuxendian comparés aux échantillonsindépendants, tagsnonprisensupport refusés, faxG3 EOLaligné avec compteligne/complet et octets connus ; aucun succèsconstant.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/TiffFixture.cs

Preuve : Lecture intégrale des 140 lignes ; prédicteurhorizontal construit depuis samplesoriginaux, zlibBCL et IFD trié/tagoverrides avec endian explicite, offsets/padding et bitsparsample contrôlés pour usages1/3canaux présents dans PredictorAndFaxTests.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/TiffWriter.cs

Preuve : Lecture intégrale des 127 lignes ; TIFFclassic indépendant chaînesIFD, strips et champshorsligne alignés, rationalcount/endianness explicites, nullstrip volontairementhorsfichier. Les données invalides sont des scénarios annoncés de test.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/TiffDecoderTests.cs

Preuve : Lecture intégrale des 121 lignes ; pagesendian/index, taillesstrips/tuileszero, fillorder2, CMYK/grayalpha et LZWcodes/EOI vérifiés par pixels exacts, donnéesendommagées gardéesourefusées selon contrat explicite, aucun formatnonimplémenté masqué.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/CcittFaxTests.cs

Preuve : Lecture intégrale des 189 lignes ; codesT4/T6 indépendants, modesG3/G4/alignment/RTC et troncaturecomplet testés par bits/pixels exacts, plafondsdimensions/output et croissance1,6Mrows ciblés ; ces cas ne sont pas exécutés sur hôte par cette revue.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2ArithmeticTests.cs

Preuve : Lecture intégrale des 65 lignes ; entiersarithmétiques couvrent préfixes/signes/OOB etsymbolID, flux5000bits plus marqueur0xAC relus ; lecture au-delàdata doit lever InvalidDataException, boucleadversariale non lancée sur hôte.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2Images.cs

Preuve : Lecture intégrale des 79 lignes ; motifs deterministicseed/rowstypiques et glyphes, ParseASCII et ComposeOR manuels donnent attentes indépendantes, AssertSame compare dimensions et touspixels ; utilitaireDecode appelle réellement le codec.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2PdfTests.cs

Preuve : Lecture intégrale des 94 lignes ; globalsdictionnaire réel puis PDFDecode noir0, padding/crop dimensions attendues, malformedflux non dessinéetgap, stencil bleu/white pixelréel ; RawPdf écrit des objets effectifs, aucun mock.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2Writer.cs

Preuve : Lecture intégrale des 520 lignes en trois tranches ; encodeurMQ/entiers/ID/templates/refinement et bitsHuffman/segments écrivent les flux synthétiques depuis T88, états et flush marqueur explicites. Les références externes de conformance restent une preuve indépendante distincte.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2GenericTests.cs

Preuve : Lecture intégrale des 353 lignes en deux tranches ; quatretemplates/prédiction/adaptifs, MMRcodesindépendants, refinementsréférencés, combinaisonOR/AND/XOR/XNOR/replace et stripes/pageheight sont comparés aux pixelsattendus ; refusnormatifs explicites.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2HalftoneTests.cs

Preuve : Lecture intégrale des 196 lignes ; dictionnairespatterns et bitplanesGray écrits indépendamment, quatretemplates/gridtournée/skip/XORfondnoir/MMR comparés à compositionmanuelle via positionsT88 ; grillegéante et patternmanquant refusés.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2HuffmanTests.cs

Preuve : Lecture intégrale des 279 lignes en deux tranches ; dictionnaires23symboles/runlengthcodes32à34, collectifsbruts/MMR, aggregation/refinement/customtables et IDcodes comparés aux placementsindépendants ; quinze tables cache/codescanoniques et refusinvalides ciblés.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jbig2/Jbig2SymbolTests.cs

Preuve : Lecture intégrale des 328 lignes en deux tranches ; placementstrips huitcoins/transposition/offsets et raffinement/aggregation/retenuecontexts/globals comparés à glyphescomposés manuellement, listeslonguesetIDs32bits exercés ; malformeddictionnaires/textesrefusés.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jpeg2000/Jpeg2000StructureTests.cs

Preuve : Lecture intégrale des 164 lignes ; bitstuffingFF/overrun, paquetsprogressions sansdoublons et niveauxmixtes, ondelettesoddvalue20 calculée, composantsincompatibles/bitplanes31 refusés, fallbackcolorspace et coupures testés avec succèsminimal8 exigé.

RAS


## tests/OmniEurope.Documents.Tests/Imaging/Jpeg2000/Jpeg2000DecoderTests.cs

Preuve : Lecture intégrale des 323 lignes en deux tranches ; plus30dégâts ponctuels typeInvalidData, partiesnonprisesencharge distinctes, palettes signées12bits/mappings/color/ICC/boîteslongues et troncatures comparés à plansindépendants ; conversioncouleur calculeattendus.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/FormulaFixture.cs

Preuve : Lecture intégrale des 52 lignes ; valeursA/B/C/D et instantfixe2026 indépendants, formule enZ100 horsplages lues, Recalculatepublic puis Unsupportedvide et valeurfinale renvoyée ; helper ne fabrique pas résultat attendu.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/FormulaOperatorTests.cs

Preuve : Lecture intégrale des 97 lignes ; précédences/coercions/erreurs concat15chiffres et ordretypedifférents ont attendus explicites indépendants, tableauxarithmétiques et refsabsolute/autresfeuilles réels ; aucun double de moteur.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/FormulaSyntaxTests.cs

Preuve : Lecture intégrale des 109 lignes ; référencesabsolues/plagesinverses/feuillesquotes/prefixes/littéraux calculés, erreursyntaxe/nonprisensupport gardent cache42 et raison explicite, limitesXFD/1048576 distinguées ; aucune formuleignorée silencieusement acceptée.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/FormulaBudgetTests.cs

Preuve : Lecture intégrale des 117 lignes ; budgets défaut1/8colonnes, mêmesplages1,2M refuséespuiscalculées à17 avecbudgetexplicite, cumul deuxmatrices et budgetsnonpositifs/incohérents refusés ; caches123 préservés sur refus.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/FormulaLimitTests.cs

Preuve : Lecture intégrale des 243 lignes en deux tranches ; bornes256 inclusif/exclusif et stack1MiB, chaîneslongues/arrays/matricesgéantes refusées avec cachepropagé et autresformulescalculées ; bornesusages connues et testpasexécuté sur hôte.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/SharedFormulaTests.cs

Preuve : Lecture intégrale des 69 lignes ; décalagesrelatifs/absolus/littéraux/feuilles/tablerefs et bords#REF attendus précisément, groupeXLSXécritXML indépendant puis recalcul4valeurs3/5/7/7 contre caches0, ordreheadless déclaré.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/FormulaFunctionTests.cs

Preuve : Lecture intégrale des 268 lignes en deux tranches ; fonctionsmath/logique/recherche/texte/dates ont attendus explicites travaillés survaleursfixes, erreurs coerces distinctes, tolérance numérique12/9décimales et horloge2026 injectée, système1904 testé.

RAS


## tests/OmniEurope.Documents.Tests/Excel/Formulas/XlsxRecalculationTests.cs

Preuve : Lecture intégrale des 159 lignes ; dépendancesinterfeuilles ordreinverse et chaîne100000, cyclesexactscellules, cachepropagéunsupported, typesdates/texte/bool/erreurs conservés ; facture calculéeàmain66/13,20/79,20 relue en CSVetPDF publics.

RAS


## tests/OmniEurope.Documents.Tests/Excel/NumberFormatterTests.cs

Preuve : Lecture intégrale des 98 lignes ; chaînesnumériques/date/heure/sections/texte et culturesfr/invariant attendues explicitement, formatsdates distinguésdeslittéraux, serials59/61 testent saut1900 ; culture passée explicitement, pas horlogeduposte.

RAS


## tests/OmniEurope.Documents.Tests/Excel/FractionFormatTests.cs

Preuve : Lecture intégrale des 153 lignes ; fractionscontinuesattendues détaillées, masques1/2/3chiffres et dénominateursfixes, impropres/signes/zéros/sections/limites9chiffres, workbookréel sauvegardérelue. Aucun résultatExcelexterne contemporain revendiqué.

RAS


## tests/OmniEurope.Documents.Tests/Excel/ExcelDetailTests.cs

Preuve : Lecture intégrale des 147 lignes ; formatsdates/chiffres15digits et refsA1 calculés, bords1900/1904 dernierjour9999, XLSXréel réécrit pourflagdate/malformed/viderefusés ; cultureexplicitanglais/invariant et fluxZIP libérés.

RAS


## tests/OmniEurope.Documents.Tests/Excel/XlsxFactory.cs

Preuve : Lecture intégrale des 64 lignes, longues lignesXML incluses ; packageXLSX écrit indépendamment avec styles/sharedstrings/chart/comments/table/pivot/cache/rels et image, données enrichies hors écrivainproduction pour tester préservationdesparties.

RAS


## tests/OmniEurope.Documents.Tests/Excel/XlsxEditorTests.cs

Preuve : Lecture intégrale des 176 lignes ; XLSXmanuel édité puis chaque partiehorsscope comparée octetpour octet, XML autourcellules conservé et relu publiquement, calculchain suppriméeproprement, formats/date1904/positionsimplicites et refus sansmutation observables.

RAS


## tests/OmniEurope.Documents.Tests/Excel/XlsxWorkbookTests.cs

Preuve : Lecture intégrale des 231 lignes en deux tranches ; valeurs/styles/ranges sauvegardés et XMLZIP parse, XMLmanuel autreécrivain avec chemins/relsdifférents et positionsimplicites, CSVfr et bornesgéantes avantécriture, noms/merges/objetsinvalides refusés précisément.

RAS


## tests/OmniEurope.Documents.Tests/Text/BidiTests.cs

Preuve : Lecture intégrale des 247 lignes : niveaux UAX9 dérivés à la main, ordre visuel explicite, classes faibles et neutres, paires de crochets, embeddings et isolates limités, L1, miroir et paires de substitution ; assertions déterministes sans oracle circulaire.

RAS


## tests/OmniEurope.Documents.Tests/Charts/ChartFactory.cs

Preuve : Lecture intégrale : fabriques XML et ZIP indépendantes des sérialiseurs produit, séries en culture invariante avec valeurs absentes, types et axes ECMA-376, relations Word et Excel, thèmes et ancres en EMU ; les assertions de lecture et schéma ont été relues avec ces entrées.

RAS


## tests/OmniEurope.Documents.Tests/Charts/ChartGeometryTests.cs

Preuve : Lecture intégrale : graduations calculées à la main, sommes positives et négatives, pourcentages et valeurs absentes, sommets et arcs de secteurs, arrondi pair des couleurs, frontière exacte 100000 et 100001 cellules avec valeurs début et fin.

RAS


## tests/OmniEurope.Documents.Tests/Charts/ChartReadingTests.cs

Preuve : Lecture intégrale : DOCX indépendants validés XSD, modèle séries et thèmes comparé à valeurs explicites, relecture après sauvegarde et suppression externalData, gaps HTML et Markdown, cache Excel absent et ancres calculées, budgets de formules dégradées.

RAS


## tests/OmniEurope.Documents.Tests/Charts/ChartRenderingTests.cs

Preuve : Lecture intégrale : conversions Word et Excel vers PDF par API publiques, textes et graduations attendus à la main, pixels 72 dpi aux points géométriques hors bord, secteurs et empilements et absence de données, taille et placement Excel comparés aux cellules ; renderer interne assumé, pas oracle externe revendiqué.

RAS


## tests/OmniEurope.Documents.Tests/Charts/ChartRobustnessTests.cs

Preuve : Lecture intégrale : caches forgés et budgets exacts de points, séries vides sans expansion, corruption indépendante chart/drawing/theme avec gaps observables, formules par cellule, bornes numériques et couleurs non finies ; gros scénarios réservés à la suite finale en conteneur.

RAS


## tests/OmniEurope.Documents.Tests/Word/ReadingGapDetailsTests.cs

Preuve : Lecture integrale : codes et messages anglais et francais des gaps issus de WordDocument, XlsxWorkbook, conversion, champs et controles reels ; arguments et propagation compares explicitement sans culture globale modifiee.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordContentEditingTests.cs

Preuve : Lecture integrale : Runs et SetContent par API publiques, conservation champs complets et notes et wrappers, relecture ZIP/XML apres traduction, no-op compare toutes les entrees octet par octet, refus arguments et resolution cascade de styles sans ChangedParts.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordDrawingEditingTests.cs

Preuve : Lecture integrale : edition PNG et encre dans vraie fixture DOCX, parties relations types et ids controles, ressources partagees conservees, relecture et rendu PDF avec pixels calcules et comparaison avec/sans insertion ; controle XSD relatif au modele initial explicite.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordEditorTests.cs

Preuve : Lecture integrale : fixture externe et document synthetique verifies sans modification entree par entree, remplacement traverse runs mais pas champs et notes, SetText et langue relus, proprietes core limitees au bon part, fusion notes listes styles images et sections comparee au modele explicite.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordFidelityTests.cs

Preuve : Lecture integrale : VML et wrappers XML construits independamment, conversion unites et revisions lues, tables commentaires tabulations et numerotation relus apres serialization, metadonnees dates fixes, suppression XML invalide et conservation emoji verifiees explicitement.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordFloatingTests.cs

Preuve : Lecture integrale : ancres ECMA independantes, distances EMU et alignements et chaque wrap relus apres serialization, simplePos prioritaire, VML et colonnes inegales avec espacement par defaut, FitsText sur DrawingML et VML ; attentes numeriques explicites.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordMergeTests.cs

Preuve : Lecture integrale : deux paquets XML independants avec conflits ids et references absentes, unicite copie images, remappage signets listes styles et headers, commentaires supprimes selon contrat, ordre sectPr/pPrChange controle ; ressources reelles deja decodees.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordParagraphLocationTests.cs

Preuve : Lecture integrale : chemins de blocs listes explicitement sur tableaux imbriques, controles, XML et textboxes ; parties et identifiants notes commentaires headers, stabilite apres sauvegarde et traduction, recherche insensible casse, egalite defensive et arguments invalides controles.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordReadingTests.cs

Preuve : Lecture integrale : WordprocessingML manuel pour wrappers, equations et gaps, identite image partagee et partie absente, sections et notes avec relecture, defauts page/styles distincts, VML flottant et inline, adresses source comparees au WordEditor public.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordSchemaValidatorTests.cs

Preuve : Lecture integrale : auteurs multiples et XML manuel, erreurs XSD avec part chemin ligne et attribut, strict/transitional et compatibilite markup avec temoins invalides, relations pendantes et types XML, notices et schemas embarques, validation parallele ; aucune validation externe Office annoncee.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordThemeWritingTests.cs

Preuve : Lecture integrale : theme XML personnalise compare par DeepEquals sur trois schemes, changement MajorFont controle sans perdre couleurs ni MinorFont, fixture reelle relue avec types relations et validation XSD, cas sans polices sans theme ; attentes non circulaires sur contenu essentiel.

RAS


## tests/OmniEurope.Documents.Tests/Word/WordTrackedChangeTests.cs

Preuve : Lecture integrale : XML manuel couvre chaque revision et decisions accept/reject opposees, auteur et date fixes, moves complets et paragraphes joints, retablissement champs et proprietes tables/sections, sauvegarde puis modele et XSD controles, refus revision perimee et traitement headers/styles/fallbacks.

RAS


## tests/OmniEurope.Documents.Tests/Pdf/CharStringAssembler.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complete : operateurs Type1/Type2 explicites, assembleur de donnees de test avec entier compact/16/32bits et masque brut, culture invariante ; auxiliaire borne par les petits programmes des tests, aucune assertion factice.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/FontPrograms.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : fixtures CFF/name/CID et Type1 eexec binaire/hex/PFB, chiffrement réel et longueurs réelles, carré à coordonnées indépendantes, données synthétiques assumées et petites, aucun résultat produit simulé.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/CharStringTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : assertions exactes des commandes et coordonnées Type1/2, flex/seac/subrs et masques, emballages chiffrés réels ; limites testées sans allocation géante, 65536 entrées finies. Tests non exécutés ici.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfAChecker.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète en deux tranches : contrôles des octets xref/obj/stream, XMP et ICC, fontes incorporées/cid/Unicode, actions et annotations ; vérifie réellement 27 clauses annoncées avec limites explicites (pas un validateur complet), récursion bornée et assertions portées par appelants. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/RawPdf.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : auxiliaire construit de vrais PDF minimaux avec offsets xref calculés et longueurs de flux exactes, Latin1 et nombres invariants, utile pour cas que le writer produit ne génère pas ; pas de mock ni assertion triviale.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfArchiveTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : PDF/A B/U relu par checker partiel explicite, test négatif plain/encrypted, XMP/Info/ICC à valeurs indépendantes, glyphes omis et positions, conversions Word/Excel/Markdown/images réelles en mémoire ; fichiers petits et assertions non vacues. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfCMapTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : bfchar/bfrange/cidchar/cidrange et Identity-H attendus exacts, noms glyphes invalides/surrogates, espaces code0/5octets ignorés et sortie Take10 anti-boucle ; bornes et octet final testés réellement. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/EncryptedPdf.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : générateur indépendant de PDF chiffrés RC4 R2/R3, AES128 R4, AES256 R5/R6, crypt-filters et métadonnées en clair ; algorithmes cryptographiques BCL réels, aléas attendus et hash2B borné par octet/compteur, petits documents. Auxiliaire, non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfEncryptionTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : réponses figées auto-dérivées explicitement signalées puis comparées au produit, ouverture réelle propriétaire/utilisateur, refus mauvais mot de passe et permissions altérées, SASLprep RFC4013 et troncature127octets, chiffrement éditeur. Pas de mocks ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfCanvasTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : rendu réel en mémoire à72dpi, pixels attendus des polygones/trous/opacité, coordonnées texte tourné et flux multipage, refus après Finish, images masque/CMYK et syntaxe sérialisée. Quelques égalités de valeur simples accompagnent des scénarios effectifs, aucun faux vert global. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfResolutionBudgetTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : chaînes de longueurs indirectes64/128 bornées, exception InvalidDataException attendue avant pile ; chaîne8 et cycle de références vérifient acceptation/terminaison normale. Petites fixtures, aucun stress exécuté dans cette revue.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfEditingTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : merge/extract/split/rotate, plages invalides, tampons incrémentaux avec préfixe original intact, xref-streams, compression/images masquées et graphes cycliques ; fixtures navigateur et fichiers réels en mémoire, aléas de bruit semés et dimensions400x300 bornées. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfLayoutTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : colonnes fixture navigateur, ordre de lecture sur trois pages et décorations répétées, mots/style/bounding-box, détection scan contre texte réel ; assertions présence et ordre/absence utiles, images200x280 finies. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfFilterTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : cinq prédicteurs PNG avec encodeur indépendant et TIFF/LZW fixture Windows, données endommagées/filtre inconnu, plafonds100..1000octets sur chaque filtre et chaîne, limite exacte acceptée ; faux bombes limitées à5000octets, message spécifique vérifié. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfSecurityTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : ouverture réelle RC4/AES R2..R6 et Identity strings, titres/textes attendus, refus mot de passe absent/incorrect et handler PubSec, chaînes AES courtes/padding altéré avec texte page préservé. Aucun mock ni secret réel ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfFormTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : fixture AcroForm indépendante champs/options/états, Fill relu et préfixe préservé, apparences et vrais pixels avant/après, Flatten enlève champs/XFA garde annotation Texte, mauvais types/valeurs/direct/encrypted/damaged refusés. Rendu200x200 et parcours fini, non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfImageColorTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : Lab vers rouge/blanc avec tolérance physique, Separation/DeviceN en gris explicitement attendus, ICC par N sans prétendre appliquer profil, espaces nommés/cycliques, palette flux,16bits et inline/form/images indécodables. Petites images et assertions pixels précises ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/ConversionDetailTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : vrais HTML→Word et Excel→Word, assertions structure/listes/alignements/fusions, imagesTIFF→PDF réouvert et RTL par positions ; sondes Conversion couvrent autres cas distincts sans doublonner CVT.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/ConvertersTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : vrais convertisseurs HTML/Markdown/Excel/images, PDF réouvert et texte/tailles/tableaux vérifiés, limite sparse MaxCells et erreurs ; aucune assertion fondée sur mock.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/DocumentGapTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : codes GapCode réfléchis et ressources EN/FR, placeholders, vraie conversion arabe avec GapDetails et culture restaurée finally ; assertion codesDistinct redondante mais autres gardes utiles.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/EmfFixture.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale fixture : vrais enregistrements EMF binaires avec offsets/tailles/header/EOF, formes16/32bits, transforms et DIB ; aide déterministe, pas faux moteur ni mock.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/EmfPlaybackTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : EMF réel→Word→PDF→pixels, comparaisons équivalences plus seuil encre non vide, géométrie texte et gaps, enregistrements forgés bornés ; joueur externe décrit mais non exécuté ici.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/ExcelTextOverflowTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : Excel→Word→PDF réouvert/rendu72dpi ; assertions overflow cellules, chiffresGeneral et clips par absence pixels hors bord, minimum10lettres masquées évite test vide ; helpers lus.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/ExcelToHtmlTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : XLSX sauvegardé/rechargé→HTML parsé et grille comparée au CSV, CSS/merges/nonce/href, injections markup et limitesMaxCells ; tests utiles distincts COH-001 déjà reproduit.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/HtmlMarkdownGapCodeTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : conversions réelles pour image inconnue, commentaires, CSS liée et imagesURI ; codes/arguments/messages EN/FR et déduplication assertSingle vérifiés.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/HtmlStyleRules.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale aide CSS : absence attributstyle et classe générée unique, extraction règles via GeneratedRegex nécessitant partial, assertions auxiliaires réellement consommées.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/HtmlToWordClassRulesTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : DOCX→HTML→Word signature comparée au témoin inline et contrôle négatif sans règles, cascade/classes/media/noncehighlight, cas40000règles avec borne20s ; pas exécuté sur hôte.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/LineBreakerTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : algorithme interne réel avec tokens mesurés/déterministes, coupures ASCII texte intégral et largeurs, tabs/justification/spacing/breaks ; CVT-007 astral reste constat distinct déjà porté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/RenderingDetailsTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : mappingCSS/unités et images, DOCX/PDF réels avec positions/tailles/textes, cellules fusionnées et editor sauvegardé/rouvert, numérotation liée aux styles ; pas de mocks.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordCalibriGapTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : vraie conversion Calibri/CalibriLight et fonte enregistrée, fichierDOCX réel, codes/arguments/messages EN/FR distincts ; assertions présence et absence ciblées.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordColumnLayoutTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : colonnes largeur/espacement/separator par positions et pixelsPDF, tableau dans colonne large, équilibre continuous et règles finpage via DOCX rechargé ; CVT-008 NextColumn distinct déjà porté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordFieldUpdaterTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : DOCX édité puis XSD validé et modèle/PDF rechargés, TOC/PAGEREF/pages/dates fixées, idempotence XML et bookmarks ; gaps champs non supportés vérifiés sans mocks.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordFloatClipTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : vrais Word→PDF→rendu72dpi, comptage pixels peints dans textbox et absence hors clip, contrôle positif forme de corps ; seuils non vides utiles.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordFloatWrapTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : DOCX sauvegardé/rechargé→PDF, positions et lignes autour flottants, côtés/alignements/pagebreak/boîteFitsText/table, garde pagecouverte et gaps ; helpers contexte réel sans mocks.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordFootnoteSeparatorTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : DOCX roundtrip footnote separator, longueur règle mesurée sur pixels288dpi, modèle indent et effet pagination avec séparateur60pt ; déterminisme du dessin.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfLayoutDirectionTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : mots/lignes et blocs sur texte vertical90/270 mélangé horizontal, pages tournées, décorations répétées/page numbers et texte ordre exact ; cas fins sur deux pages sans assertions vacues. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfLayoutWordTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : exposants/indices et baselines, petites capitales, chevauchements et ponctuation, espace isolé, tracking/characterSpacing et deux colonnes ; textes et mots attendus littéraux, petits cas six lignes et120espaces. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfNamedPropertyRedactionTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : caviardage retire secrets visibles et ActualText/Alt/E dans tous flux décodés et octets PDF, conserve original et texte PUBLIC/MCID ; variantes formulaire/alias et partage interpages dans les deux ordres, petite fixture indépendante. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfPatternTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : motifs colorés/noncolorés, matrices/lattice et chevauchements, stencil/texte/opacité, shading et erreurs avec gaps, cycles et résolution plafonnée1Mpx, matrices dégénérées ; pixels indépendants à72dpi, aucune charge exécutée. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfRedactionTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète en deux tranches : secrets supprimés de textes, chaînes et flux décodés, positions restantes, images pixellisées ou retirées, métadonnées/annotations, familles/formulaires hérités ou manquants refusés ; motifs/stencils/Type3 d1/FontBBox et zones hors contenu couverts. Assertions positives et négatives sur fichiers réouverts et pixels, scénarios petits ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfRendererFontTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : fixtures CFF charset/encoding/CID et wrappers OpenType, rendu carré avec trois pixels indépendants, modes stroke/clip, noms glyphes cyrilliques/symboles, palettes et gradients avec tolérances ; opérations200x200 bornées, non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfRendererTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : rendu raster de chemins/trous/anti-alias, traits/caps/dashes, clipping page/partiel, images/inline/stencil, fontes TTF/CFF/Type1/Type3 réelles et fallback avec gap, rotation/annotations cachées, PNG et métadonnées de dimensions. Assertions pixels/valeurs non simulées ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordHtmlContentControlTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : vrais SDT OOXML puis HTML parsé, wrappers/tag/kind/alias/nesting et optionoff par témoin sans contrôles ; assertions PDF ici portent comptepages/taille, ne prouvent pas pixels identiques.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordJustifyShrinkTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : tokens géométriques et vrais textes Carlito, limite25pourcent espaces selon compatibility14/15, DOCX roundtrip et lignesPDF comparées à fixtureWord365 ; pas de mock.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordLayoutTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale non tronquée relue séparément : tableaux/styles/shadings vérifiés pixels, alignements/fusions/colonnes et sections, notes et imagesgaps, leaders et champs, paragraphespacing ; PDF réouvert.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordPageAlignmentTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : alignements verticals top/center/bottom/both par baselinesPDF, témoins et DOCX roundtrip, distribution entre paragraphes et gap pour formeflottante ; assertions utiles distinctes simple pagecount.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordPageFieldWidthTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : vrais DOCX→PDF27pages, textes footer1/10/20/27 et nonrecouvrement glyphes, centrage9/12 selon largeurvariable ; helpers baselines réels.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordRightToLeftTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : PDF réouvert, ordrex latin/hébreu/chiffres et parenthèsesmiroir, marges/indents/taillecomplexe/justification, garde arabicgap ; témoins géométriques indépendants des classes layout.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordSymbolFontTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : Symbol/Wingdings par PDF lettres/métriques/fontes réelles, avance glyphes AFM et licences/embedding, caractères mappés disponibles ; CVT-007 astral distinct non dupliqué.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordTableHeaderKeepTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : vrais Word→PDF réouvert, entêtes gardées avec premiercorps et keepNext, contrôle espace suffisant et répétitionunique ; concerne moteurWord distinct PDF-005 fluxPDF.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordToHtmlContentTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : HTML parsé et injection scripts/URI/styles/alt, imagesMIME identifiées par bytes et TIFF/EMF réellement décodés, donnéesUnicode et gaps ; aucune certification navigateur CSP revendiquée.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordToHtmlCspTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : fixturesDOCX réelles→HTML, classesCSS complètes/dédupliquées, nonce/href validés et échappés, images séparées copiées/MIME/dédupliquées ; assertions structurelles CSP utiles, navigateur non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfRenderingDetailTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : courbes/caps/dashes, équivalences opérateurs texte comparées en pixels, formes cycliques, rotations, refus résolution impossible avant allocation, gaps localisés et fonctions shading invalides/taille forgée ; dessins200x200 bornés. Non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfSyntaxTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : tokens nombres/noms/strings et délimiteurs invalides, progression du lexer, tableaux/dictionnaires tolérants, nesting250 refusé, indirect sans endobj et flux Length incorrect/indirect/tronqué. Entrées minuscules, asserts valeurs réelles ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfSignatureTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : vraies clés RSA2048/ECDSA P256 et CMS DER, vérification indépendante ASN1/BCL du digest et signature/ESSCertID, octets signés/préfixe, altérations digest/signature/padding et mises à jour successives, refus certificat sans clé/SHA1/encrypted. Certificats de test autosignés explicites, aucune preuve de confiance PKI externe ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfStructureTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : xref-streams/hybrides réels écrits par fixture indépendante, object-stream valide/invalide et index absent, tables réparées/wrong offsets, longueur auto-référente et counts/widths forgés bornés par petits flux. Texte Hi et WasRepaired vérifiés, non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfTextExtractionTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : positions et largeurs TJ/quotes/Tz attendues numériquement, police absente, CID listes/plages malformed et défaut, UCS2 chinois, MacRoman/Type1 encoding et programme illisible fallback. Fixturing brut indépendant du writer, entrées finies et assertions exactes ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfWriterTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : offsets xref contre en-têtes objets, déterminisme octets, fontes subset/ToUnicode et métadonnées, métriques chiffrées, wrapping contenu intégral et limites largeur, flux multipage/footer et images alignées, CMYK Adobe et invalid-use.80lignes de table/120phrases bornées, pas de mocks ; non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordToHtmlTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : pageHTML complète, styles/listes/fusions/notes/révisions, adresses comparées au WordEditor dans plusieurs parties et fixtures externes, HTML→Word roundtrip structure/textes ; gaps limites documentés.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Conversion/WordToMarkdownTests.cs

Preuve : Revue deleguee /root/nov_coherence, rapport JSONL relu et hash actuel controle : Lecture intégrale : Markdown exact, DOCX sauvegardé équivalent, liens et échappements, référencesimagesbytes volontairement opaques, notes/listes/tableaux et roundtrip Markdown→Word structure/emphase ; CVT-006 espaceImageFolder déjà porté séparément.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.


## tests/OmniEurope.Documents.Tests/Pdf/PdfTransparencyTests.cs

Preuve : Revue deleguee /root/nov_architecture, rapport JSONL relu et hash actuel controle : Lecture complète : tous modes de fusion et cas extrêmes avec couleurs calculées indépendamment, alpha ca/CA, groupes isolés/knockout/AIS, masques luminosité/alpha/BC/TR et matrice, Matte démultiplié, masques imbriqués cycliques et transparence nulle. Rendus200x200, pixels précis et gaps effectifs, non exécuté.

RAS, revue deleguee documentee ; aucune execution de test annoncee par le reviseur.
