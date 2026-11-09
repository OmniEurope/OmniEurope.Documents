# Revue du module Codecs

Audit neuf du 9 novembre 2026. Chaque bloc conserve sa preuve de lecture ou d'inspection.

Revue Full terminée : 105/105 fichiers, état, index et empreintes contrôlés. CDC001 (Moyen) est le seul constat retenu, reproduit sur un pixel via les DLL Release. Aucune source, aucun test ni version modifiés ; aucune suite ou compilation de solution relancée.

Les sources couvrent les codecs raster et fax, JBIG2/JPEG 2000, le texte bidirectionnel et les encodages, OPC/ZIP/XML, les fontes et leur sous-ensemble, les limites publiques et les messages localisés. Chaque preuve ci-dessous résulte de la lecture complète du fichier ou d'un parsing natif complet des données. Les 21 fontes du cœur ont toutes leurs glyphes/contours, cmap, métriques et ressources DLL inspectés dans `metrics/font-data-native-proof.json`. Licences OFL/Adobe lues intégralement et confrontées au package vérifié. Les 113 entrées de chaque langue ont été parsées et leurs messages lus en parallèle, leurs clés et formats vérifiés dans `metrics/gap-resource-native-proof.json`.

Qualité, conventions, architecture, sécurité, performance, fiabilité et contrats publics ont été examinés sur les fichiers lus, avec les bornes d'allocations et les limites documentées des décodeurs. Les métriques de couverture/CRAP restent celles de la collecte centrale ; aucune valeur n'est déduite de la lecture. Les résultats Gitleaks et dépendances sont ceux de `SECURITY_SCAN.md`, sans prétendre à un SAST complet. Les doublons CVT007 (surrogates) et COH002 (DPI) sont exclus.

Vérifications de fermeture : zéro divergence LSB/hmtx contre glyf sur Liberation Sans, Carlito et Caladea réguliers, piste écartée (`metrics/font-subset-lsb-proof.json`). La lecture BMP tolérante ne fournit pas de constat distinct suffisamment étayé. Le JP2 imbriqué de 480 012 octets (60 000 boîtes) a été exécuté une seule fois dans le conteneur SDK local, réseau coupé, CPU1, mémoire256MiB, exécution limitée à20s : retour `InvalidDataException`, exit0 (`metrics/jp2-recursion-container.log`). Ce cas ne prouve pas une borne générale de récursion ; aucune panne observée et aucun constat retenu. Le premier accès Docker sandbox refusé a été relancé par le canal approuvé, sans changer la configuration.

Proportionnalité : les implémentations spécialisées sont cohérentes avec le contrat sans dépendance ; un lecteur externe simplifierait les codecs mais violerait ce contrat. Aucun découpage supplémentaire n'est justifié par les conséquences observées.

## src/OmniEurope.Documents/Fonts/FontReader.cs

Preuve : Lecture intégrale : lectures big-endian, Has utilise une somme long et refuse offset/longueur négatifs, tables décrites sans accès natif.

RAS


## src/OmniEurope.Documents/Fonts/FontUse.cs

Preuve : Lecture intégrale : contrat public Requested/Used/Exact/Note confronté aux substitutions de FontLibrary et au consommateur du package.

RAS


## src/OmniEurope.Documents/Fonts/SymbolMetrics.cs

Preuve : Lecture intégrale : parsing AFM invariant C/WX, filtre codes 0..255, chargement Lazy et notice Adobe embarquée obligatoire.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Adobe/MustRead.html

Preuve : Lecture intégrale de la permission Adobe : redistribution commerciale autorisée, notices conservées et AFM accompagné du présent fichier.

RAS


## src/OmniEurope.Documents/Fonts/TrueTypeCmap.cs

Preuve : Lecture intégrale des formats cmap 0/4/6/12, préférence Unicode, alias symboles et budgets bornés pour groupes/segments chevauchants.

RAS


## src/OmniEurope.Documents/Fonts/TrueTypeFont.cs

Preuve : Lecture intégrale : répertoire sfnt/TTC, noms Unicode, métriques hhea/OS2/post, cmap et contrôles de bornes ; données partagées exposées explicitement.

RAS


## src/OmniEurope.Documents/Fonts/TrueTypeGlyphs.cs

Preuve : Lecture intégrale : loca court/long, contours simples et composés, profondeur limitée à huit, composantes renumérotées et approximation point-matching explicite.

RAS


## src/OmniEurope.Documents/Fonts/TrueTypeSubsetter.cs

Preuve : Lecture intégrale : fermeture des composantes, glyf/loca/hmtx, tables conservées, alignement et checksum ; piste LSB à confronter aux métriques natives.

RAS à ce stade ; vérification ciblée du LSB restant à effectuer.


## src/OmniEurope.Documents/Fonts/FontLibrary.cs

Preuve : Lecture intégrale : caches ConcurrentDictionary et verrous par famille, priorité aux enregistrements, substitutions explicites, alias métriques et licences embarquées.

RAS


## src/OmniEurope.Documents/OmniEurope.Documents.csproj

Preuve : Lecture intégrale : aucune dépendance NuGet, AOT/trim activés, assemblage compagnon ajouté au même package par cible sans référence circulaire, ressources et licences confrontées au pack vérifié.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LICENSE-Caladea.txt

Preuve : Lecture intégrale : copyright Caladea 2012, OFL 1.1 redistribution et incorporation autorisées, licence et notice conservées au pack.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LICENSE-Carlito.txt

Preuve : Lecture intégrale : copyright Carlito 2013 et nom réservé, cinq conditions OFL 1.1, incorporation permise et licence embarquée/livrée.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LICENSE-Liberation.txt

Preuve : Lecture intégrale : copyrights Google/Red Hat, noms réservés Arimo/Tinos/Cousine/Liberation, conditions OFL 1.1 et livraison distincte vérifiée.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LICENSE-NotoSansSymbols2.txt

Preuve : Lecture intégrale : copyright Noto 2022, OFL 1.1 complet, fonte régulière de symboles autorisée et accompagnée du texte dans ressources/package.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Adobe/Symbol.afm

Preuve : Lecture intégrale : Adobe Symbol 001.008, 190 métriques dont apple non encodé, avances C/WX et notice 1985..1997 présentes ; ressource AFM avec licence packée.

RAS


## src/OmniEurope.Documents/DocumentGap.cs

Preuve : Lecture intégrale : codes stables, ressources neutres anglaises/françaises, égalité par texte anglais et intégration des textes libres via GapSet.

RAS


## src/OmniEurope.Documents/PackageLimits.cs

Preuve : Lecture intégrale : limites publiques init-only, valeurs 2000/256MiB/1GiB/200 documentées et exceptions de format distinctes des IOException.

RAS


## src/OmniEurope.Documents/Internal/GapSet.cs

Preuve : Lecture intégrale : déduplication ordinale des textes, conservation du détail existant et reconstruction des textes ajoutés publiquement sans code.

RAS


## src/OmniEurope.Documents/Internal/Crc32.cs

Preuve : Lecture intégrale : table CRC32 réfléchie 0xEDB88320, complément entrée/sortie et mise à jour incrémentale utilisées par PNG et ZIP.

RAS


## src/OmniEurope.Documents/Internal/SafeZip.cs

Preuve : Lecture intégrale : contrôle nombre/tailles déclarées et lues/ratio, budget total cumulatif, XML DTD interdit et XmlResolver nul, fermeture configurée.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Caladea-Bold.ttf

Preuve : Inspection native intégrale de la fonte Caladea Bold : 464 glyphes parcourus, 435 correspondances cmap, 883 contours/17662 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Caladea-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Caladea Bold Italic : 459 glyphes parcourus, 430 correspondances cmap, 871 contours/18412 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Caladea-Italic.ttf

Preuve : Inspection native intégrale de la fonte Caladea Italic : 459 glyphes parcourus, 430 correspondances cmap, 872 contours/18491 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Caladea-Regular.ttf

Preuve : Inspection native intégrale de la fonte Caladea Regular : 464 glyphes parcourus, 435 correspondances cmap, 884 contours/17253 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Carlito-Bold.ttf

Preuve : Inspection native intégrale de la fonte Carlito Bold : 2783 glyphes parcourus, 2117 correspondances cmap, 6839 contours/126426 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Carlito-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Carlito Bold Italic : 2782 glyphes parcourus, 2117 correspondances cmap, 5321 contours/124013 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Carlito-Italic.ttf

Preuve : Inspection native intégrale de la fonte Carlito Italic : 2783 glyphes parcourus, 2117 correspondances cmap, 6735 contours/124076 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/Carlito-Regular.ttf

Preuve : Inspection native intégrale de la fonte Carlito Regular : 2783 glyphes parcourus, 2117 correspondances cmap, 6830 contours/125188 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationMono-Bold.ttf

Preuve : Inspection native intégrale de la fonte Liberation Mono Bold : 2423 glyphes parcourus, 2305 correspondances cmap, 5058 contours/69254 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationMono-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Liberation Mono Bold Italic : 2425 glyphes parcourus, 2305 correspondances cmap, 5040 contours/74159 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationMono-Italic.ttf

Preuve : Inspection native intégrale de la fonte Liberation Mono Italic : 2425 glyphes parcourus, 2305 correspondances cmap, 5057 contours/74180 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationMono-Regular.ttf

Preuve : Inspection native intégrale de la fonte Liberation Mono : 2423 glyphes parcourus, 2305 correspondances cmap, 5049 contours/69101 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSans-Bold.ttf

Preuve : Inspection native intégrale de la fonte Liberation Sans Bold : 2620 glyphes parcourus, 2327 correspondances cmap, 5281 contours/71094 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSans-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Liberation Sans Bold Italic : 2622 glyphes parcourus, 2327 correspondances cmap, 5285 contours/76002 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSans-Italic.ttf

Preuve : Inspection native intégrale de la fonte Liberation Sans Italic : 2622 glyphes parcourus, 2327 correspondances cmap, 5294 contours/75668 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSans-Regular.ttf

Preuve : Inspection native intégrale de la fonte Liberation Sans : 2620 glyphes parcourus, 2327 correspondances cmap, 5288 contours/71785 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSerif-Bold.ttf

Preuve : Inspection native intégrale de la fonte Liberation Serif Bold : 2602 glyphes parcourus, 2321 correspondances cmap, 5256 contours/87960 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSerif-BoldItalic.ttf

Preuve : Inspection native intégrale de la fonte Liberation Serif Bold Italic : 2605 glyphes parcourus, 2322 correspondances cmap, 5248 contours/89818 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSerif-Italic.ttf

Preuve : Inspection native intégrale de la fonte Liberation Serif Italic : 2610 glyphes parcourus, 2329 correspondances cmap, 5247 contours/90565 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/LiberationSerif-Regular.ttf

Preuve : Inspection native intégrale de la fonte Liberation Serif : 2602 glyphes parcourus, 2321 correspondances cmap, 5254 contours/87764 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Fonts/Bundled/NotoSansSymbols2-Regular.ttf

Preuve : Inspection native intégrale de la fonte Noto Sans Symbols 2 Regular : 2660 glyphes parcourus, 2641 correspondances cmap, 14594 contours/206923 points ; métriques, incorporation OFL et égalité exacte avec ressource DLL vérifiées.

RAS


## src/OmniEurope.Documents/Internal/OpcRelationships.cs

Preuve : Lecture intégrale : résolution URI relative/racine et segments point, cibles externes jamais résolues ni téléchargées, chemins de relations inverses vérifiés.

RAS


## src/OmniEurope.Documents/Internal/OpenXmlPackageWriter.cs

Preuve : Lecture intégrale : timestamps déterministes, XML UTF8 sans BOM, échappement Office littéral et contrôles interdits, paires de surrogates conservées.

RAS


## src/OmniEurope.Documents/Internal/PrefixedReadStream.cs

Preuve : Lecture intégrale : préfixe servi sans perte, lectures synchrones/asynchrones et fermeture leaveOpen, absence seek/écriture, ReadAtMost borné par compte demandé.

RAS


## src/OmniEurope.Documents/Internal/OpcPackage.cs

Preuve : Lecture intégrale : stockage brut des parties inchangées, XML DTD interdit/profondeur256, relations/contenus ajoutés ou retirés cohérents et sauvegarde déterministe.

RAS


## src/OmniEurope.Documents/Text/TextEncodingDetector.cs

Preuve : Lecture intégrale : BOM UTF32 avant UTF16, UTF8 surlong/surrogate/hors-Unicode refusés, troncature de prélèvement et limite ASCII explicitement documentées.

RAS


## src/OmniEurope.Documents/Text/Windows1252Encoding.cs

Preuve : Lecture intégrale : table 0x80..9F et inverse, cinq contrôles conservés, paire surrogate remplacée une fois, contrats des buffers et préambule vide.

RAS


## src/OmniEurope.Documents/Text/BidiBrackets.cs

Preuve : Lecture intégrale des tableaux de paires/mirroirs, initialisation réciproque et canonisation U2329/U232A ; couverture limitée annoncée dans summary.

RAS


## src/OmniEurope.Documents/Text/BidiClasses.cs

Preuve : Lecture integrale : classes explicites, plages RTL, categories BCL, non-caracteres BN et limites documentees ; aucune table de code externe.

RAS


## src/OmniEurope.Documents/Text/BidiAlgorithm.cs

Preuve : Lecture integrale : classification UTF16 des paires, niveaux paragraphes, reset L1 et ordre visuel L2 ; doublon CVT007 LineBreaker exclu.

RAS


## src/OmniEurope.Documents/Text/BidiSequence.cs

Preuve : Lecture integrale : W1..W7, pile de 63 parentheses, N0/N1/N2, sequences isolantes et niveaux I1/I2, ordre logique des paires restaure.

RAS


## src/OmniEurope.Documents/Text/BidiParagraph.cs

Preuve : Lecture integrale : appariement isolates, pile directionnelle profondeur125 avec compteurs de debordement, sequences de niveaux et sos/eos, controles retires.

RAS


## src/OmniEurope.Documents/Imaging/ImageScaler.cs

Preuve : Lecture integrale : filtre par zones entieres, dimensions minimales1, preservation type couleur et DPI proportionnel ; COH002 DPI deja pris par coherence non duplique.

RAS


## src/OmniEurope.Documents/Imaging/DamagedData.cs

Preuve : Lecture integrale : erreurs indices/bornes/overflow requalifiees en InvalidDataException avec cause et nom du codec, aucune reussite factice.

RAS


## src/OmniEurope.Documents/Imaging/RasterImage.cs

Preuve : Lecture integrale : limite pixels en long, taille buffer exacte, alpha droit explicite, conversion CMYK complement et copie avec DPI conserves.

RAS


## src/OmniEurope.Documents/Imaging/ImageInfo.cs

Preuve : Lecture integrale : reconnaissance headers PNG/JPEG/GIF/BMP/TIFF/WebP, tailles positives, refus des headers tronques et WebP identification seule documentee.

RAS


## src/OmniEurope.Documents/Imaging/CcittCodes.cs

Preuve : Lecture integrale des codes T4 blancs/noirs terminaux et make-up/etendus, tables par longueur maximale13 et enumeration inverse pour controles.

RAS


## src/OmniEurope.Documents/Imaging/PngCodec.cs

Preuve : Lecture integrale : signatures et CRC par chunk, tailles long bornees, decompression jusqua taille attendue, profondeurs autorisees et encoding zlib deterministe.

RAS


## src/OmniEurope.Documents/Imaging/PngUnpacker.cs

Preuve : Lecture integrale : cinq filtres reversibles, sept passes Adam7, palettes/tRNS et reduction16bits, filtres adaptatifs encodage et score absolu.

RAS


## src/OmniEurope.Documents/Imaging/BmpDecoder.cs

Preuve : Lecture integrale : DIB core/info/V4/V5, palettes/masks/alpha, RLE4/8 et lignes inversees ; piste de troncature silencieuse a verifier sur petit fichier.

RAS


## src/OmniEurope.Documents/Imaging/GifDecoder.cs

Preuve : Lecture integrale : ecran logique, palettes/transparence, quatre passes et LZW12bits bornes, protection dimensions avant allocation, premiere frame explicite.

RAS


## src/OmniEurope.Documents/Imaging/TiffDecoder.cs

Preuve : Lecture integrale : IFD sans cycles, limites dimensions/types et tags, strips/tiles, compression bornee et DPI ; piste alpha associe a verifier sur une seule cellule.

CDC001 : alpha TIFF associe non demultiplie, preuve native a un pixel dans metrics/tiff-alpha-proof.json.


## src/OmniEurope.Documents/Imaging/TiffSupport.cs

Preuve : Lecture integrale : echantillons 1..16bits, CMYK/palette/alpha, LZW early-change12bits, PackBits et predictor horizontal endian ; alpha associe non distingue.

CDC001 : alpha TIFF associe non demultiplie, preuve native a un pixel dans metrics/tiff-alpha-proof.json.


## src/OmniEurope.Documents/Imaging/CcittFaxDecoder.cs

Preuve : Lecture integrale : options T4/T6, plafond bytes/pixels avant allocation et pendant croissance, completion false explicite si interruption, lignes manquantes blanches.

RAS


## src/OmniEurope.Documents/Imaging/CcittRows.cs

Preuve : Lecture integrale : modes pass/horizontal/vertical, elements changeants monotones, FindB reprend la recherche, codes de longueur au plus13 et fin de bitstream refusee.

RAS


## src/OmniEurope.Documents/Imaging/JpegDecoder.cs

Preuve : Lecture integrale : segments et tables quantification/Huffman, frames8bits baseline/progressive, precision/format non gere refuses, exceptions endommagees et JFIF DPI.

RAS


## src/OmniEurope.Documents/Imaging/JpegBitReader.cs

Preuve : Lecture integrale : Huffman canonique16bits, stuffing FF00, restart et markers, zero extension des scans tronques explicitement annoncee.

RAS


## src/OmniEurope.Documents/Imaging/JpegFrame.cs

Preuve : Lecture integrale : geometrie MCU avec facteurs1..4 valides en amont, blocs utilises/paddes distincts, indices de coefficients et permutation zigzag64.

RAS


## src/OmniEurope.Documents/Imaging/JpegScanDecoder.cs

Preuve : Lecture integrale : baseline et quatre phases progressives, DC predictif et reset RST, EOB run, raffinement coefficients et tables absentes refusees.

RAS


## src/OmniEurope.Documents/Imaging/JpegIdct.cs

Preuve : Lecture integrale : IDCT separable, quantification et clamp8bits, upsampling triangle3/4-1/4, CMYK/YCCK Adobe et RGB explicites.

RAS


## src/OmniEurope.Documents/Imaging/JpegEncoder.cs

Preuve : Lecture integrale : qualite1..100, limite65535, DCT et quantification, YCbCr420 optionnel et DPI JFIF, alpha abandonne explicitement.

RAS


## src/OmniEurope.Documents/Imaging/JpegEntropy.cs

Preuve : Lecture integrale : frequences et Huffman optimal limite16bits, code tout1 reserve, parcours MCU, DC/AC/ZRL/EOB et stuffing FF00.

RAS


## src/OmniEurope.Documents/Imaging/MqDecoder.cs

Preuve : Lecture integrale : etats Qe/NextMps/NextLps et echange, renormalisation et ByteIn, garde IdleLimit65536 pour extension apres marqueur/fin.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2Bitmap.cs

Preuve : Lecture integrale : limites dimensions en long, pixels hors image blancs, combinaisons OR/AND/XOR/XNOR/Replace, extraction et croissance de stripes.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2BitReader.cs

Preuve : Lecture integrale : lecture bits MSB dans une plage, compteur long, alignement octet et deplacement controles par parseurs appelants.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2IntegerDecoder.cs

Preuve : Lecture integrale : arbre entier arithmetique annexeA avec512contextes, signe et valeur hors bande distincte, compteur bits fini.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2SymbolIdDecoder.cs

Preuve : Lecture integrale : procedure IAID et contexte par prefixe, longueur de code derivee des dictionnaires bornes verifies dans le decodeur.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2Segments.cs

Preuve : Lecture integrale : headers courts/longs, comptes references bornes par donnees, longueurs inconnues marqueur/compte lignes, plages de segments et pixels adaptatifs.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2Decoder.cs

Preuve : Lecture integrale : globals precedant page, dictionnaires/types regions dispatches, template etendu refuse, premieres pages/stripes et hauteur inconnue gerees.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2GenericDecoder.cs

Preuve : Lecture integrale : quatre templates, pixels adaptatifs causaux, prediction typique et contexte partage ; MMR arrete sur EOFB, budget pixels du bitmap confronte.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2RefinementDecoder.cs

Preuve : Lecture integrale : templates0/1, reference deplacee, prediction voisinage3x3 uniforme et contexte causal, copies des contextes pour dictionnaires.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2Halftone.cs

Preuve : Lecture integrale : dictionnaires motifs, grille bornee, plans Gray par XOR, skip hors region, geometrie en long et combinaison explicite.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2HuffmanTable.cs

Preuve : Lecture integrale : quinze tables standards et valeurs, attribution canonique, plages lower/upper/OOB et tables personnalisees finies par flux bits borne.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2TextSegment.cs

Preuve : Lecture integrale : flags et selections Huffman/refinement, nombre instances et codes symboles RLE, tables personnalisees manquantes refusees.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2SymbolDictionary.cs

Preuve : Lecture integrale : comptes symboles bornes, contextes retenus copies, classes hauteur/largeur, fermeture des exports, cumul pixels et references symboles controles.

RAS


## src/OmniEurope.Documents/Imaging/Jbig2/Jbig2TextRegion.cs

Preuve : Lecture integrale : comptes instances bornes, bandes S/T et coins transposees, identifiants valides, refinements et variantes Huffman/arithmetique, fin derniere bande tolerante documentee.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kParameters.cs

Preuve : Lecture integrale : geometrie SIZ/tiles, progression cinq ordres, flags codeblock, quantification derivee/explicite et refus bandes manquantes.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kPacketBits.cs

Preuve : Lecture integrale : stuffing7bits apresFF, flag Overrun explicite, alignement et marqueurs SOP/EPH sans boucle non bornee.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kRawBits.cs

Preuve : Lecture integrale : bypass bits MSB, sept bits apresFF et extension1 en fin de segment, compte de passes fourni par paquet borne.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kTagTree.cs

Preuve : Lecture integrale : pyramide minima, valeurs et bornes inferieures memorisees, seuil limite par couches et precision dans lecteur de paquets.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kColorTransform.cs

Preuve : Lecture integrale : transformees RCT5-3/ICT9-7, trois composantes de taille identique et de meme reversibilite exigees.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/Jp2File.cs

Preuve : Lecture integrale : boites JP2/JPX, tailles32/64bits controlees, palette et canaux/ICC/Lab, recursive jp2h identifiee pour verification bornee separee.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/Jpeg2000Decoder.cs

Preuve : Lecture integrale : conteneur ou codestream brut, decodage toutes tiles vers plans puis canaux, exceptions de bornes requalifiees sans masquer erreurs format.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/Jpeg2000Image.cs

Preuve : Lecture integrale : echantillons signes/decalages, subsampling, plans et palette, Lab/YCC, alpha premultiplie demultiplie et sorties explicitement8bits.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/Jpeg2000Channels.cs

Preuve : Lecture integrale : mapping et palettes, definitions reordonnees, alpha distingue des couleurs, profil ICC classe par espace declare sans pretendre gestion colorimetrique complete.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/Jpeg2000Colors.cs

Preuve : Lecture integrale : YCC et Lab D50/D65 versRGB, gamma et clamp, alpha droit et demultiplication explicite avec alpha0 gere.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kTileDecoder.cs

Preuve : Lecture integrale : priorite parametres main/tile/composante, tiers2/1 puis ondelette et composantes, decalage DC et clipping precision.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kCodestream.cs

Preuve : Lecture integrale : SIZ validates pixels/tiles/samples par byte, marqueurs longueurs et styles bornes, limitesprecision30 et niveaux32, PPM/PPT/POC et refus part2/HT.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kPacketReader.cs

Preuve : Lecture integrale : tiers2 inclusion/tagtree, passes/segments et bypass, flags Overrun et contributions bornees au corps, troncature annoncee dans contrat interne.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kTileComponent.cs

Preuve : Lecture integrale : niveaux/bandes/precincts crees paresseusement, MaxPrecincts1M, magnitude30bits, geometrie Ceil en long et buffers de codeblocks par croissance.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kPacketOrder.cs

Preuve : Lecture integrale : cinq progressions et POC bornes, couche suivante par precinct sans doublon, parcours positions sur grille reference en long.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kBlockDecoder.cs

Preuve : Lecture integrale : trois passes tiers1, contextes19 et bandes4lignes, bypass/reset/segmentation, profondeur30bits, dequantification et ROI avec reconstruction midpoint.

RAS


## src/OmniEurope.Documents/Imaging/Jpeg2000/J2kWavelet.cs

Preuve : Lecture integrale : interleave bandes puis lignes/colonnes, extension symetrique et parite absolue, filtres lifting5-3entier/9-7reel et signal singleton.

RAS


## src/OmniEurope.Documents/Resources/GapMessages.resx

Preuve : Parsing XML natif integral et lecture des 113 messages ; noms uniques, tous templates formates, paires anglais/francais confrontees aux details et contrat DocumentGap, UnknownGap neutre vide volontaire.

RAS


## src/OmniEurope.Documents/Resources/GapMessages.fr.resx

Preuve : Parsing XML natif integral et lecture des 113 messages ; noms uniques, tous templates formates, paires anglais/francais confrontees aux details et contrat DocumentGap, UnknownGap neutre vide volontaire.

RAS
