# Revue du module Pdf

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

## Clôture du module

**96 fichiers sur 96 lus intégralement et consignés**, avec empreintes source et preuves revérifiées : zéro différence, zéro doublon, zéro entrée absente. Quatre constats Moyen actuels dans `findings-Pdf.json`, sans duplication de COH-002 (Dpi) porté en Coherence.

| ID | Localisation principale | Conséquence reproduite |
|---|---|---|
| PDF-001 | Pdf/PdfPage.cs:84 | Flux indécodable omis : rendu blanc, aucun gap, alors que le témoin contient kept. |
| PDF-002 | Pdf/Editing/PdfEditor.cs:121 | CreationDate disparaît après rotation et compression. |
| PDF-003 | Pdf/Forms/FormTree.cs:30 | MaxLen2 hérité annoncé null, ABC accepté ; la limite directe refuse ABC. |
| PDF-005 | Pdf/Writing/PdfTable.cs:97 | Première entête peinte hors page avant le saut qui place son corps. |

Preuve native : `dotnet run --project .claude/audit/2026-11-09/artefacts/coherence-probe/CoherenceProbe.csproj -c Release`, code0. Références aux DLL Release existantes, aucun build source. Le programme construit de vrais petits PDF avec xref valide, ouvre les sorties, rend leurs pixels, compare les champs et les lettres ; données dans `coherence-probe/results.json`, PDF et journal conservés. Le témoin flux contient 393 composantes non blanches contre0 après filtre illisible. La sonde profonde205 niveaux échoue explicitement au garde de profondeur, gardé intact ; elle n'est pas assimilée à une preuve de prise en charge du contenu rejeté.

Roslyn réellement appelé sur ce lot : API publique de PdfForm, localisation de FormFieldNode et références de MaxLength ; les lectures intégrales couvrent les96 sources. Les distinctions Open/Read/Fill/Flatten/Sign/Verify, les modèles de coordonnées et les résultats Problems/Gaps restent détaillés dans `AUDIT_COHERENCE.md`. Aucune convention différente n'est promue en défaut sans effet observé. Les caractères Unicode normatifs de tables et coupures restent RAS, leur échappement littéral serait un choix de style sans correction fonctionnelle.

Sécurité : revue manuelle best-effort, non outillée par SAST général, des parsing/limites/récursions, chiffrement, signatures, caviardage et formulaires. Primitives cryptographiques BCL et bornes de profondeur/allocation relues. Gitleaks et requêtes dépendances sont les preuves centrales distinctes dans `metrics/SECURITY_SCAN.md`. Aucune certification externe PDF/A, PAdES, ICC ou sécurité du caviardage n'est revendiquée. Aucun test de charge, épuisement hôte, outil Python ni bureau utilisateur.

Métriques : `metrics/crap_report.md` est encore en attente de la collecte centrale finale ; aucun score, complexité ou taux actuel inventé. Recherche des modificateurs partial, stubs et suppressions de diagnostics sur ce module, puis lecture des occurrences : aucun modificateur partial ni suppression trouvé. Aucun code produit, test, version, politique, branche ni publication modifié par ce réviseur.

Proportionnalité : les classes collaborent sur les étapes réelles du PDF (objets, lexer/parser, fontes, rendu, formulaires, signature) ; aucune couche de normalisation générale des noms proposée.

## src/OmniEurope.Documents/Pdf/PdfDocument.cs

Preuve : Lecture intégrale actuelle 163 lignes : en-tête, Open bytes/flux, metadata et dates PDF, héritage permissions et validation numéros pages ; vraies lectures PDF dans coherence-probe.

RAS


## src/OmniEurope.Documents/Pdf/Rendering/PdfRenderer.cs

Preuve : Lecture intégrale actuelle 91 lignes : résolution/limite pixels, crop/rotation, images/résultat codé ; roundtrip PDF PNG PDF confirme COH-002 avec dimensions et DPI réouverts.

COH-002 porté exclusivement dans findings-Coherence.json : résolution pincée pour pixels mais non pour métadonnées ; pas de doublon PDF.


## src/OmniEurope.Documents/Pdf/Writing/PdfFont.cs

Preuve : Lecture intégrale actuelle 74 lignes : demande famille/style, métriques en points, tailles normalisées et conversion millimètres ; API relue pendant la matrice de cohérence.

RAS


## src/OmniEurope.Documents/Pdf/PdfPage.cs

Preuve : Lecture intégrale actuelle 163 lignes, fin arbre pages relue : héritage boxes/rotation et parcours à pile ; PDF minuscule Flate illisible démontre rendu blanc et textes vides sans gap.

PDF-001, Moyen : ContentBytes attrape erreur de décodage et supprime le flux sans transmettre de gap au renderer. Deux PDF avec xref valide, WasRepaired=false : témoin texte kept et393octets nonblancs ; flux Flate invalide texte vide/0octet nonblanc, Gaps et GapDetails vides. Preuves coherence-probe/results.json unreadablePdfContent, validPdfContent et deux PDF.


## src/OmniEurope.Documents/Pdf/Editing/PdfEditor.cs

Preuve : Lecture intégrale 171 lignes : opérations pages, copies références et options rotation/encryption ; vraie création PDF datée puis rotation réouverte prouve perte CreationDate.

PDF-002, Moyen : Information réécrit les champs texte mais omet CreationDate/ModificationDate malgré metadata kept. Source CreationDate=2020-01-02T04:04:05+01:00, sortie rotation et compression=null ; coherence-probe/results.json pdfMetadataDates. Préserver dates documentées, avec politique ModificationDate explicite.


## src/OmniEurope.Documents/Pdf/Editing/PdfCompressor.cs

Preuve : Lecture intégrale 155 lignes : copie objets atteignables, recompression sans perte et choix JPEG/downsample optionnels, masques conservés ; PDF daté compressé puis réouvert.

PDF-002 porté dans PdfEditor.Information : la compression réutilise la même reconstruction metadata et perd aussi CreationDate ; pas de doublon.


## src/OmniEurope.Documents/Pdf/Editing/PdfIncrementalWriter.cs

Preuve : Lecture intégrale 116 lignes : append octets originaux, références/xref tables ou streams, générations et nouveaux ID, Prev/Root/Info transmis examinés.

RAS


## src/OmniEurope.Documents/Pdf/Editing/PdfObjectCopier.cs

Preuve : Lecture intégrale 164 lignes : graphe copié par file pour chaînes longues, références pages réservées, héritage ressources/boxes, suppression parents et profondeur directe max256 examinés.

RAS


## src/OmniEurope.Documents/Pdf/PdfColor.cs

Preuve : Lecture intégrale 75 lignes : parsing hex3/6, sortie RRGGBB, palette nommée et choix contraste par luminance sRGB examinés.

RAS


## src/OmniEurope.Documents/Pdf/PdfEncryption.cs

Preuve : Lecture intégrale 55 lignes : bits permissions ISO, distinction propriétaire/utilisateur, limites mot de passe et portée EncryptMetadata documentées et comparées aux façades.

RAS


## src/OmniEurope.Documents/Pdf/Objects/PdfObjects.cs

Preuve : Lecture intégrale 256 lignes : contrats des objets publics, cache noms borné4096 et verrouillé, références/générations, texte UTF16 et formats de nombres examinés.

RAS


## src/OmniEurope.Documents/Pdf/Objects/PdfCompactWriter.cs

Preuve : Lecture intégrale 101 lignes : streams objets groupés100, offsets, lignes xref1/2, taille/ID et empaquetage avec First examinés.

RAS


## src/OmniEurope.Documents/Pdf/Objects/PdfObjectTable.cs

Preuve : Lecture intégrale 79 lignes : numérotation/réservation, offsets xref, chiffrement sauf dictionnaire sécurité et trailer Root/Info/ID examinés.

RAS


## src/OmniEurope.Documents/Pdf/Objects/PdfSerializer.cs

Preuve : Lecture intégrale 148 lignes : dispatch objets, noms UTF8 échappés#XX, strings littérales/hex, Length streams et buffer ASCII borné examinés.

RAS


## src/OmniEurope.Documents/Pdf/Reading/PdfLexer.cs

Preuve : Lecture intégrale actuelle 322 lignes : tokens, nombres, chaînes littérales et hexadécimales, commentaires et bornes fin de flux ; contrôle manuel sécurité best-effort sans SAST.

RAS, tolérance lexicale cohérente avec réparation PDF et bornes explicites.


## src/OmniEurope.Documents/Pdf/Reading/PdfParser.cs

Preuve : Lecture intégrale actuelle 232 lignes : objets indirects, tableaux/dictionnaires, profondeur plafonnée, longueurs de flux et recherche endstream, références et traitement EOF.

RAS, profondeur bornée et repli de lecture explicite ; aucun stub présenté comme support.


## src/OmniEurope.Documents/Pdf/Reading/PdfXref.cs

Preuve : Lecture intégrale actuelle 238 lignes : xref classique/stream/hybride, Prev avec visited, W et Index, réparation et lecture des flux compressés ; validation des bornes.

RAS, parcours de chaîne borné et réparation distincte du chemin normal.


## src/OmniEurope.Documents/Pdf/Reading/PdfObjectStore.cs

Preuve : Lecture intégrale 316 lignes : résolution récursive et cache, objets compressés avec N borné par données, déchiffrement et références cycliques, réparation du catalogue.

RAS, limites profondeur et paramètres des object streams explicites ; sécurité manuelle best-effort.


## src/OmniEurope.Documents/Pdf/Reading/PdfFilters.cs

Preuve : Lecture intégrale 413 lignes : Flate avec récupération, LZW et filtres ASCII/RunLength, plafond sortie 512 Mio et prédicteurs calculés sur 64 bits.

RAS, filtres non pris en charge échouent explicitement et plafond préservé pendant récupération ; aucun essai volumétrique hôte.


## src/OmniEurope.Documents/Pdf/Reading/PdfSecurity.cs

Preuve : Lecture intégrale : révisions RC4/AES, owner/user, méthodes Crypt, validations Perms, SASLprep et repli UTF8 ; primitives BCL et disposal examinés.

RAS sur lecture manuelle best-effort ; aucune certification cryptographique ni vecteurs externes exécutés.


## src/OmniEurope.Documents/Pdf/Reading/PdfPasswordHash.cs

Preuve : Lecture intégrale : SHA256 revision 5, boucle AES/SHA2 revision 6 au moins64 tours et arrêt borné par octet, tailles des blocs dérivées du mot de passe plafonné.

RAS, calcul réel BCL ; aucune simulation ni dépendance externe.


## src/OmniEurope.Documents/Pdf/Reading/SaslPrep.cs

Preuve : Lecture intégrale : tables RFC, NFKC, interdictions, surrogates et règle bidirectionnelle ; écart version Unicode runtime explicitement documenté.

RAS, portée SASLprep et version Unicode annoncées avec limites explicites.


## src/OmniEurope.Documents/Pdf/Signing/CmsReader.cs

Preuve : Lecture intégrale : ASN.1 BER SignedData, premier signer, sélection certificat issuer/serial ou SKI et attributs signés retagués ; entrées invalides échouent explicitement.

RAS, portée premier signataire annoncée et aucun accès réseau.


## src/OmniEurope.Documents/Pdf/Signing/CmsSignature.cs

Preuve : Lecture intégrale : ASN.1 DER CMS détaché, certificats dédupliqués, attributs ESSCertIDv2 et signatures RSA/ECDSA SHA2 ; primitives BCL et ressources using.

RAS sur examen manuel des chemins ; conformité PAdES externe non certifiée par cette lecture.


## src/OmniEurope.Documents/Pdf/Signing/PdfSignatureTypes.cs

Preuve : Lecture intégrale : options digest, date, réserve et certificats ; résultat distingue plage/digest/signature/certificat et confiance applicative explicitement exclue.

RAS, noms et sémantique IsValid/Problems documentés sans confondre intégrité et confiance.


## src/OmniEurope.Documents/Pdf/Signing/PdfSigner.cs

Preuve : Lecture intégrale : validation réserve/certificat, mise à jour incrémentale, réécriture en place ByteRange/Contents, SHA2 et collecte des champs signés.

RAS, données source préservées avant append ; échec taille CMS explicite.


## src/OmniEurope.Documents/Pdf/Signing/SignatureField.cs

Preuve : Lecture intégrale : champ invisible sur page1, noms uniques, Annots/AcroForm modifiés incrémentalement, génération placeholder et date avec offset explicite.

RAS, pas de remplacement silencieux de champs existants.


## src/OmniEurope.Documents/Pdf/Signing/SignatureVerifier.cs

Preuve : Lecture intégrale : plages dans fichier, encadrement exact Contents, règles PAdES, erreurs ASN.1, digest et signature réels ; confiance externe volontairement laissée au consommateur.

RAS sur chemins manuels ; aucun validateur externe PAdES exécuté.


## src/OmniEurope.Documents/Pdf/Forms/FormTree.cs

Preuve : Lecture intégrale : arbre itératif, visited/profondeur32, attributs hérités et widgets ; sonde réelle MaxLen parent versus terminal sur deux PDF non réparés.

PDF-003 : MaxLen hérité absent de Inherited, lecture et remplissage divergent du champ équivalent direct.


## src/OmniEurope.Documents/Pdf/Forms/PdfForm.cs

Preuve : Lecture intégrale : Read/Fill/Flatten, validation fichiers incrémentaux, types champs et appearances ; reproduction MaxLen effectif non propagé à ToField.

PDF-003 porté par FormTree, pas de doublon : MaxLength null pour la limite héritée.


## src/OmniEurope.Documents/Pdf/Forms/FormFiller.cs

Preuve : Lecture intégrale : SetText/Toggle/Choice, validations, ressources fontes et suppression XFA ; sonde ABC accepté si MaxLen2 hérité et refusé si direct.

PDF-003 porté par FormTree, pas de doublon : validation terminale oublie MaxLen hérité.


## src/OmniEurope.Documents/Pdf/Forms/PdfFormField.cs

Preuve : Lecture intégrale : records champ/widget/choix, flags, valeurs simples/multiples et options Fonts/Flatten ; nulls explicites et noms publics cohérents au domaine.

RAS, types résultat et saisie distincts justifiés par usage.


## src/OmniEurope.Documents/Pdf/Forms/IncrementalUpdate.cs

Preuve : Lecture intégrale : copie unique par numéro indirect, générations conservées et échec explicite pour objet direct ; pas de mutation source en place.

RAS, abstraction requise par édition du champ et widget fusionnés.


## src/OmniEurope.Documents/Pdf/Forms/AnnotationAppearance.cs

Preuve : Lecture intégrale : état normal et référence stream, placement des quatre coins transformés, rectangle normalisé et refus boîtes vides.

RAS, calcul de placement effectif et tolérance bornée.


## src/OmniEurope.Documents/Pdf/Forms/FormFlattener.cs

Preuve : Lecture intégrale : widgets/page, isolation q/Q, fusion ressources, filtrage widgets et suppression AcroForm en append ; chemins sans widgets examinés.

RAS sur lecture ; absence appearance traitée comme aucune apparence courante, sans génération annoncée.


## src/OmniEurope.Documents/Pdf/Forms/FieldAppearance.cs

Preuve : Lecture intégrale : textes/comb/multilignes/listes/boutons réels, fontes et taille automatique, clipping et cadre ; dépendance MaxLen terminale identifiée.

PDF-003 porté par FormTree, même valeur effective à fournir au peigne ; autres chemins RAS.


## src/OmniEurope.Documents/Pdf/Writing/PdfDocumentBuilder.cs

Preuve : Lecture intégrale : pages bornées, déduplication image, métriques fontes, Save figé et cache bytes, metadata dates, outlines et conformance ; sonde réelle création/réouverture.

RAS sur parcours examinés ; types builder et document lu justifient Save versus Open.


## src/OmniEurope.Documents/Pdf/Writing/PdfEncryptor.cs

Preuve : Lecture intégrale : aléa BCL fichier/sels/IV, algorithmes U/O/Perms revision6, récursion objets et exclusion metadata conforme option ; disposal AES.

RAS sur examen manuel best-effort, aucun essai cryptanalytique ni certification.


## src/OmniEurope.Documents/Pdf/Writing/ContentStreamBuilder.cs

Preuve : Lecture intégrale : opérateurs numériques via PdfFormat invariant, couleurs, noms et hexadécimal ; API interne utilisée par dessin PDF réel.

RAS, couche mince dédiée à sérialisation opérateurs, pas de framework superflu.


## src/OmniEurope.Documents/Pdf/Objects/PdfTextEncoding.cs

Preuve : Lecture intégrale : BOM UTF16BE/UTF8 et PDFDocEncoding ; valeur0x84 normative U+2014 présente littéralement ligne14.

RAS, valeur Unicode normative utile ; aucun défaut fonctionnel ni règle projet applicable ne justifie un constat de style.


## src/OmniEurope.Documents/Pdf/Writing/TextWrapper.cs

Preuve : Lecture intégrale : tokens espaces/coupures, mots longs découpés par runes, métriques effectives et EndsParagraph ; U+2014 littéral ligne126.

RAS, valeur Unicode normative utile ; aucun défaut fonctionnel ni règle projet applicable ne justifie un constat de style.


## src/OmniEurope.Documents/Pdf/Text/GlyphNames.cs

Preuve : Lecture intégrale : noms glyphes, uniXXXX/uXXXXXX, AFII, accents composés et tables ; U+2014 littéral ligne163 réellement relu.

RAS, valeur Unicode normative utile ; aucun défaut fonctionnel ni règle projet applicable ne justifie un constat de style.


## src/OmniEurope.Documents/Pdf/Text/StandardEncodings.cs

Preuve : Lecture intégrale : WinAnsi/MacRoman/Standard/Symbol et différences, tables256 entrées ; U+2014 présent lignes27/60 réellement relues.

RAS, valeur Unicode normative utile ; aucun défaut fonctionnel ni règle projet applicable ne justifie un constat de style.


## src/OmniEurope.Documents/Pdf/Writing/PdfArchive.cs

Preuve : Lecture intégrale : interdiction encryption et DeviceCMYK, métadonnées XMP et profil ICC en OutputIntent ; dépendances internes uniquement.

RAS sur lecture, conformité externe PDF/A non établie par cette seule inspection.


## src/OmniEurope.Documents/Pdf/Writing/PdfConformance.cs

Preuve : Lecture intégrale enum publique : None/PdfA2b/PdfA2u, descriptions des niveaux B et U ; casse acronymes cohérente avec types Pdf.

RAS, valeurs et noms explicites.


## src/OmniEurope.Documents/Pdf/Writing/SrgbProfile.cs

Preuve : Lecture intégrale : calcul primaires sRGB, adaptation Bradford D50, courbes1024, entête ICC et offsets alignés4 ; données générées déterministes.

RAS sur calcul lu ; validation ICC externe non exécutée.


## src/OmniEurope.Documents/Pdf/Writing/XmpPacket.cs

Preuve : Lecture intégrale : XML XMP UTF8, échappement texte, propriétés dates/info et nettoyage contrôles ; pas de parseur XML externe ni accès réseau.

RAS sur parcours examinés ; PDF/A externe non certifié.


## src/OmniEurope.Documents/Pdf/Writing/PdfFlowLayout.cs

Preuve : Lecture intégrale : pages et marges, EnsureSpace, paragraphes images et table, finish/footer et calcul justification ; callbacks justifiés par décorations.

RAS pour ce fichier, pagination spécifique table examinée dans PdfTable.


## src/OmniEurope.Documents/Pdf/Writing/EmbeddedFont.cs

Preuve : Lecture intégrale : subset TrueType, CIDToGID, widths, ToUnicode UTF16BE et tags SHA256 déterministes ; réel PDF sauvegardé et réouvert par sonde.

RAS, formats et métriques effectifs ; aucune fonte placeholder.


## src/OmniEurope.Documents/Pdf/Writing/PdfCanvas.cs

Preuve : Lecture intégrale : dessins textes/runes/rotations/images et liens, q/Q/clipping, ressources et PDF/A omissionnotdef ; sonde réelle plusieurs chemins dessin.

RAS sur chemins examinés ; pas de moteur externe de comparaison visuelle exécuté.


## src/OmniEurope.Documents/Pdf/Writing/PdfTable.cs

Preuve : Lecture intégrale : poids/colspan, mesures lignes, entêtes répétées et pagination ; vraie sonde tableNearBottom produit HEAD à baselineY négative sur page1.

PDF-005 : première entête dessinée avant réservation espace, hors marges/page.


## src/OmniEurope.Documents/Pdf/Writing/EmbeddedImage.cs

Preuve : Lecture intégrale : JPEG embarqué, CMYK et marqueur Adobe, pixels RGB/Gray/alpha split et masqueSMask, taille naturelle fondée DPI.

RAS établi sur chemins examinés ; conversion TIFF CMYK vers PDF/A non exercée, aucune conclusion extrapolée.


## src/OmniEurope.Documents/Pdf/Rendering/RenderGapCodes.cs

Preuve : Lecture intégrale des huit codes de gaps renderer, identifiants via nameof et ressources annoncées ; rapprochement résultat diagnostic.

RAS, codes stables explicites, absence de code flux contenu traitée dans PDF-001.


## src/OmniEurope.Documents/Pdf/Rendering/GraphicsState.cs

Preuve : Lecture intégrale : état record q/Q, clip/composition/mask, métriques texte et conversion espaces couleurs ; tableaux copiés avant conversion.

RAS, état requis par interpréteur PDF, pas de couche opportuniste.


## src/OmniEurope.Documents/Pdf/Rendering/GlyphShape.cs

Preuve : Lecture intégrale : commandes contours quadratiques/cubiques, points implicites et transformation matrice ; bornes contour et fermeture.

RAS sur lecture, sortie vectorielle réelle sans fonte simulée.


## src/OmniEurope.Documents/Pdf/Rendering/Blending.cs

Preuve : Lecture intégrale : seize modes, noms/table et fallback Normal, fonctions séparables et non séparables, saturation/luminance et clamps.

RAS sur chemins examinés ; pas de comparaison numérique externe de tous les modes.


## src/OmniEurope.Documents/Pdf/Rendering/PathBuilder.cs

Preuve : Lecture intégrale : segments, sous-chemins, fermeture et aplatissement Bézier plafonné100 pas ; transformation vers pixels.

RAS sur lecture, approximation géométrique explicite.


## src/OmniEurope.Documents/Pdf/Rendering/Rasterizer.cs

Preuve : Lecture intégrale : arêtes finies, limites canvas, cinq sous-échantillons, winding/evenodd et spans accumulés ; indices de sortie contrôlés.

RAS sur parcours examinés, pas de stress volumétrique hôte.


## src/OmniEurope.Documents/Pdf/Rendering/Stroker.cs

Preuve : Lecture intégrale : segments et caps/joins, miter, dashes et phase, orientation polygones, limite subdivision cercles ; approximation dessin examinée.

RAS établi sur lecture ; aucune comparaison exhaustive à moteur PDF externe.


## src/OmniEurope.Documents/Pdf/Rendering/PageRenderer.cs

Preuve : Lecture intégrale : tables opérateurs/ExtGState, path/clip, texte/images/patterns, formes cycliques profondeur12 et restauration état ; erreurs converties en gaps.

RAS établi sur lecture des chemins ; perte pré-interpréteur portée par PDF-001 et non dupliquée ici.


## src/OmniEurope.Documents/Pdf/Rendering/TextPainter.cs

Preuve : Lecture intégrale : matrices texte, leading et espacement, modes remplissage/stroke/clip, glyphes Type3 et avances des glyphes ; BT/ET.

RAS sur lecture et parcours texte réel sonde, pas de vérification exhaustive modes externes.


## src/OmniEurope.Documents/Pdf/Rendering/ImagePainter.cs

Preuve : Lecture intégrale : décodage avec gap, stencil, inverse matrice et limites surface, sampling bilinéaire et masque propre prioritaire.

RAS, absence de décodage image remontée au résultat, contrairement au flux contenu PDF-001.


## src/OmniEurope.Documents/Pdf/Rendering/Surface.cs

Preuve : Lecture intégrale : clipping avec raccourci rectangle plein, bounds et canvas, composition RGB opaque et masques ; coordonnées finies et boucles bornées.

RAS sur lecture, allocation surface limitée par entrée renderer déjà examinée.


## src/OmniEurope.Documents/Pdf/Rendering/GroupCanvas.cs

Preuve : Lecture intégrale : groupes isolés/non isolés/knockout, bounds, tableaux float, équations alpha et soustraction du backdrop ; clipping et AIS.

RAS sur lecture, pas de certification visuelle de tous les modes.


## src/OmniEurope.Documents/Pdf/Rendering/TransparencyPainter.cs

Preuve : Lecture intégrale : groupes et masques alpha/luminosité, TR réel avec gap si inconnu, matrice/bounds et initialisation backdrop.

RAS, limitations non prises en charge annoncées par gaps.


## src/OmniEurope.Documents/Pdf/Rendering/Patterns.cs

Preuve : Lecture intégrale : matrices inverses, shading et tiling, cellules limitées1Mpx, overlap4, dimensions/steps finies et parcours indices bornés.

RAS sur lecture ; bornes approximation explicites, aucun épuisement hôte testé.


## src/OmniEurope.Documents/Pdf/Rendering/Shading.cs

Preuve : Lecture intégrale : fonctions0/2/3 et récursion8, samples bornés par flux, shadings axiaux/radiaux et extend ; types non pris en charge explicites.

RAS sur lecture des chemins, couverture numérique exhaustive non exécutée.


## src/OmniEurope.Documents/Pdf/Rendering/AnnotationPainter.cs

Preuve : Lecture intégrale : annotations visibles, flags Hidden/NoView et normal appearance placé via matrice ; absence appearance annoncée ignorée.

RAS, comportement correspond au contrat local.


## src/OmniEurope.Documents/Pdf/Rendering/PdfPageComparison.cs

Preuve : Lecture intégrale : options validées, overloads pages/docs/PNG/raster, tolérance symétrique voisinage, alpha sur blanc et DiffPng ; sonde Dpi exécutée.

COH-002 déjà porté en Coherence pour Dpi renderer ; validation comparaison reste explicitement stricte, pas de doublon.


## src/OmniEurope.Documents/Pdf/Text/ContentStreamReader.cs

Preuve : Lecture intégrale : matrices, tokens opérateurs, plafond64 operands et inline images ; petite sonde205 niveaux confirme InvalidDataException de garde parser.

RAS fonctionnel établi : garde profondeur préservé, sonde bornée rejetée explicitement ; formule interne never throws ne couvre pas ce garde et nécessite précision documentaire si maintenue.


## src/OmniEurope.Documents/Pdf/Text/PdfCMap.cs

Preuve : Lecture intégrale : codespaces1à4, bfchar/bfrange/cidchar/cidrange, incrément destinationUTF16 et identités ; borne expansion65536 et boucle lexer plafonnée.

RAS sur lecture, aucune sonde volumétrique hôte.


## src/OmniEurope.Documents/Pdf/Text/PdfFontDecoder.cs

Preuve : Lecture intégrale : décodage simple/composite, ToUnicode et encoding, Widths/CID bornés, Type3 bounds et fontesstandards, repli documenté.

RAS établi sur parcours examinés, pas de vérification exhaustive des encodages historiques.


## src/OmniEurope.Documents/Pdf/Text/TextExtractor.cs

Preuve : Lecture intégrale : opérateurs texte, état q/Q et formes cycliques12, glyphes/avances/horizontalscale et géométrie lettres en espacePDF ; sonde texte réelle.

RAS sur chemins examinés, extraction sans annotations conforme périmètre page/formes.


## src/OmniEurope.Documents/Pdf/Text/SimpleTextOrder.cs

Preuve : Lecture intégrale : groupes directions et lignes, tracking retiré et espace selon distances/décalagebaseline ; réutilise analyse layout.

RAS, heuristique annoncée et comportement réel sur lettres de sonde.


## src/OmniEurope.Documents/Pdf/Text/PageImages.cs

Preuve : Lecture intégrale : API images/PNG null si illisible, collecte inline/XObject, matrices q/Q et parcours formes12 avec active set.

RAS, décodage nullable documenté et distinct de résultat renderer avec gaps.


## src/OmniEurope.Documents/Pdf/Text/PdfColorSpace.cs

Preuve : Lecture intégrale : familles/device/indexed/Lab/ICCBased/encres, récursion8, décode par défaut et sortiesRGB/CMYK/Gray ; approximations explicitement documentées.

RAS sur lecture, approximation couleurs annoncée sans gestion ICC complète revendiquée.


## src/OmniEurope.Documents/Pdf/Text/PdfImageDecoder.cs

Preuve : Lecture intégrale : codecs filtres, dimensions et samples, Decode/Mask/SMask, matte et stencil boundedgrowth, index64bits et TryDecode nullable.

RAS établi sur parcours lus, masques illisibles non exercés dans cette preuve ; aucune extrapolation de fidélité totale.


## src/OmniEurope.Documents/Pdf/Layout/PdfLayoutModel.cs

Preuve : Lecture intégrale : records mots/lignes/blocs/pages, propriétés typographiques à partir lettres et flags décoration/scanned ; invariants collections créées par analyzer.

RAS sur lecture, heuristiques fontnames annoncées et cohérentes.


## src/OmniEurope.Documents/Pdf/Layout/TextDirection.cs

Preuve : Lecture intégrale : normalisation angle par5degrés, groupes dans ordre dessin et projections points/boîtes selon framebaseline.

RAS, approximation direction explicitement documentée.


## src/OmniEurope.Documents/Pdf/Layout/LineGrouping.cs

Preuve : Lecture intégrale : baselines/scripts/overlays/spacingtracked, seuils documentés, recherche dichotomique et segmentation mots ; aucune extrapolation par comptage.

RAS sur lecture, complexité requise par variantes texte réelles, pas de simplification spéculative.


## src/OmniEurope.Documents/Pdf/Layout/PdfLayoutAnalyzer.cs

Preuve : Lecture intégrale : Analyze/Words/Lines, scanned par surface image, directions/scripts, BlockBuilder XYcut récursif et décorations crosspage ; heuristiques explicites.

RAS, analyse structurelle heuristique distincte extraction texte simple ; pas de garantie OCR inventée.


## src/OmniEurope.Documents/Pdf/Rendering/FontPrograms.cs

Preuve : Lecture intégrale : lookup glyphname/Unicode/built-in, CFF sous sfnt, CIDToGID et Type1 ; dépendances internes fontes partagées.

RAS sur lecture des chemins, aucun dead code déduit de grep.


## src/OmniEurope.Documents/Pdf/Rendering/RenderFont.cs

Preuve : Lecture intégrale : cache/context/gaps, fonte embarquée Type1/CFF/TTF/Type3, repli bundled et conversion CID/GID ; gaps embeddingunreadable explicites.

RAS sur lecture et fontes réellement dessinées dans sondes ; limitations repli annoncées.


## src/OmniEurope.Documents/Pdf/Rendering/Fonts/CffStandardStrings.cs

Preuve : Lecture intégrale de construction table391 SIDs, noms ASCII préservés et Get borné ; liste réellement parcourue sans conclusion par comptage seul.

RAS, table de format normative, aucun framework superflu.


## src/OmniEurope.Documents/Pdf/Rendering/Fonts/CffFont.cs

Preuve : Lecture intégrale : INDEX/DICT, nombres, charsets/encoding/FDSelect et privates/subrs, glyph bounds ; données invalides captées par chargeur renderer.

RAS sur lecture ; expert charset non gardé explicitement annoncé dans source, pas de support intégral déduit.


## src/OmniEurope.Documents/Pdf/Rendering/Fonts/Type1Font.cs

Preuve : Lecture intégrale : segments PFB/longueurs, eexec/charstring déchiffrés, regex encoding/private et subrs plafonnées65535, contours réels.

RAS sur lecture ; fontes historiques non vérifiées exhaustivement par corpus externe.


## src/OmniEurope.Documents/Pdf/Rendering/Fonts/Type1CharString.cs

Preuve : Lecture intégrale : interprétation Type1, nombres, stacks et subrs profondeur10, flex et seac réels, commandescontours et clôture.

RAS sur lecture, limitations corpus historique non extrapolées.


## src/OmniEurope.Documents/Pdf/Rendering/Fonts/Type2CharString.cs

Preuve : Lecture intégrale : opérateurs dessin CFF, stems/hints, biases subrs profondeur10 et flex ; arithmétique/storage non supportés annoncés.

RAS sur lecture, prise en charge partielle explicitement documentée.


## src/OmniEurope.Documents/Pdf/Editing/PdfRedactionException.cs

Preuve : Lecture intégrale : exception publique explicite refusant la redaction quand fontes impossibles à résoudre ; portée absence résultat documentée.

RAS, échec honnête requis pour éviter faux masquage.


## src/OmniEurope.Documents/Pdf/Editing/RedactGapCodes.cs

Preuve : Lecture intégrale : codes vectorpaintover, patternsremoved, glyphsmaymove, formsdeep et undecodable ; identifiants nameof liés aux ressources.

RAS, pertes déclarées par codes stables.


## src/OmniEurope.Documents/Pdf/Editing/RedactionState.cs

Preuve : Lecture intégrale : matrices/texte et espacements, patterns fill/stroke, ExtGState et linewidth/miter ; switches operands gardés.

RAS sur lecture, état réel requis pour suppression et non simple surimpression.


## src/OmniEurope.Documents/Pdf/Editing/PdfRedactor.cs

Preuve : Lecture intégrale : API redaction et options, objets reconstruits, passages différés propriétéspartagées, annotations et remplissage ; garde font unresolved examinée.

PDF-002 porté par PdfEditor : conservation dates Info aussi dépendante de la méthode commune ; redaction non certifiée sécurité exhaustive par cette lecture.


## src/OmniEurope.Documents/Pdf/Editing/PdfStamper.cs

Preuve : Lecture intégrale : incrément append et refus encrypt/repaired, canvas crop, noms ressources uniques, q/Q et fusion annotations/resources.

RAS sur lecture, signatures antérieures conservées comme révisions, pas revendication document final encore signé.


## src/OmniEurope.Documents/Pdf/Editing/RedactionImages.cs

Preuve : Lecture intégrale : pixels RGBA et centre sous zone, fill opaque, reconstructionRGB/alpha et nouvelles références, images illisibles supprimées entières.

RAS établi sur code actuel ; garanties de suppression limitées au modèle et aux zones documentées.


## src/OmniEurope.Documents/Pdf/Editing/RedactionPatterns.cs

Preuve : Lecture intégrale : suivi chemins et marges stroke, détection patterns contenant texte/images ou illisibles, retrait conservateur et ExtGState.

RAS sur lecture, suppression conservatrice annoncée et gap dédié.


## src/OmniEurope.Documents/Pdf/Editing/RedactionRewriter.cs

Preuve : Lecture intégrale : glyphes vers TJ et conservation avance, formes récursives profondeur12, substitutions objets/images/patterns, propriétés ActualText nettoyées et fontes refusées explicitement.

RAS sur lecture actuelle, aucun défaut de sécurité conclu depuis ancien audit ou heuristique seule.
