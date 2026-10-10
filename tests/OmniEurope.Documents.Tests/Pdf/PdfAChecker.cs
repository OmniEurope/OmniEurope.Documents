// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Structural checks of a file against requirements of ISO 19005-2:2011 (PDF/A-2), each violation reported with
/// its clause. No conformance validator is available offline: these checks are written from the clauses listed, read
/// from the raw bytes where the clause is about syntax and through the package reader for the object model. They
/// cover what the package writes; they are not a complete validator.
/// </summary>
internal static class PdfAChecker
{
    private static readonly Regex HeaderPattern = new(@"^%PDF-1\.[0-7](\r\n|\r|\n)%[\x80-\xFF]{4}", RegexOptions.CultureInvariant);
    private static readonly Regex IdPattern = new(@"/ID\s*\[\s*<[0-9A-Fa-f]+>\s*<[0-9A-Fa-f]+>\s*\]", RegexOptions.CultureInvariant);
    private static readonly Regex StartPattern = new(@"startxref(?:\r\n|\r|\n)(\d+)", RegexOptions.CultureInvariant);
    private static readonly Regex XrefPattern = new(@"xref(?:\r\n|\n)(\d+) (\d+)(?:\r\n|\n)", RegexOptions.CultureInvariant);
    private static readonly Regex ObjectPattern = new(@"(\d+) (\d+) obj(?:\r\n|\r|\n)", RegexOptions.CultureInvariant);

    // The operators of ISO 32000-1 annex A (6.2.2: a content stream holds no other operator).
    private static readonly HashSet<string> Operators = new(StringComparer.Ordinal)
    {
        "b", "B", "b*", "B*", "BDC", "BI", "BMC", "BT", "BX", "c", "cm", "CS", "cs", "d", "d0", "d1", "Do", "DP", "EI", "EMC", "ET", "EX", "f", "F", "f*",
        "G", "g", "gs", "h", "i", "ID", "j", "J", "K", "k", "l", "m", "M", "MP", "n", "q", "Q", "re", "RG", "rg", "ri", "s", "S", "SC", "sc", "SCN", "scn",
        "sh", "T*", "Tc", "Td", "TD", "Tf", "Tj", "TJ", "TL", "Tm", "Tr", "Ts", "Tw", "Tz", "v", "w", "W", "W*", "y", "'", "\"",
    };

    // 6.5.1: actions a PDF/A-2 file shall not hold.
    private static readonly HashSet<string> ForbiddenActions = new(StringComparer.Ordinal)
    {
        "Launch", "Sound", "Movie", "ResetForm", "ImportData", "Hide", "SetOCGState", "Rendition", "Trans", "GoTo3DView", "JavaScript",
    };

    // ISO 32000-1 table 136, the only blend modes 6.2.10 admits.
    private static readonly HashSet<string> BlendModes = new(StringComparer.Ordinal)
    {
        "Normal", "Compatible", "Multiply", "Screen", "Overlay", "Darken", "Lighten", "ColorDodge", "ColorBurn", "HardLight", "SoftLight",
        "Difference", "Exclusion", "Hue", "Saturation", "Color", "Luminosity",
    };

    // 6.6.2.3.1: the predefined schemas the packet may use (and the XMP, RDF and XML namespaces themselves).
    private static readonly HashSet<string> Namespaces = new(StringComparer.Ordinal)
    {
        "adobe:ns:meta/", "http://www.w3.org/1999/02/22-rdf-syntax-ns#", "http://www.w3.org/XML/1998/namespace", "http://www.w3.org/2000/xmlns/",
        "http://purl.org/dc/elements/1.1/", "http://ns.adobe.com/xap/1.0/", "http://ns.adobe.com/pdf/1.3/", "http://www.aiim.org/pdfa/ns/id/",
    };

    /// <summary>The clauses checked, for the record of what a passing file was held to.</summary>
    public static readonly string[] Clauses =
    [
        "6.1.2", "6.1.3", "6.1.4", "6.1.7.1", "6.1.7.2", "6.1.9", "6.1.13", "6.2.2", "6.2.3", "6.2.4.3", "6.2.5", "6.2.8", "6.2.9",
        "6.2.10", "6.2.11.3.2", "6.2.11.4.1", "6.2.11.5", "6.2.11.7.2", "6.2.11.8", "6.3.2", "6.3.3", "6.5.1", "6.5.2", "6.6.2.1", "6.6.2.3.1", "6.6.3",
        "6.6.4",
    ];

    public static List<string> Check(byte[] pdf, char level)
    {
        var violations = new List<string>();
        Header(pdf, violations);
        var offsets = CrossReference(pdf, violations);
        var document = PdfDocument.Open(pdf);
        var store = document.Store;
        Objects(pdf, store, offsets, violations);
        OutputIntent(store, violations);
        Metadata(store, level, violations);
        var visited = new HashSet<PdfObject>(ReferenceEqualityComparer.Instance);
        foreach (var number in store.ObjectNumbers)
        {
            Walk(store, store.Resolve(new PdfReference(number, 0)), violations, visited, 0);
        }

        foreach (var page in document.Pages)
        {
            Content(store, page.ContentBytes(), page.Resources, level, violations, 0);
            Annotations(store, page, violations);
            if (level == 'U' && page.Letters.Any(l => l.Value.Length == 0 || l.Value.Any(c => c is (char)0 or (char)0xFEFF or (char)0xFFFE or (char)0xFFFD)))
            {
                violations.Add("6.2.11.7.2: a character drawn on page " + page.Number + " has no Unicode value");
            }
        }

        if (store.Catalog.ContainsKey("AA") || document.Pages.Any(p => p.Dictionary.ContainsKey("AA")))
        {
            violations.Add("6.5.2: an additional-actions dictionary on the catalog or a page");
        }

        if (store.Get(store.Get<PdfDictionary>(store.Catalog, "Names"), "JavaScript") is not null)
        {
            violations.Add("6.5.1: a JavaScript name tree");
        }

        return violations;
    }

    // 6.1.2: "%PDF-1.n" then a comment line of at least four bytes above 127.
    private static void Header(byte[] pdf, List<string> violations)
    {
        var text = Encoding.Latin1.GetString(pdf, 0, Math.Min(pdf.Length, 32));
        if (!HeaderPattern.IsMatch(text))
        {
            violations.Add("6.1.2: the header is not %PDF-1.n followed by a binary comment");
        }
    }

    // 6.1.3 and 6.1.4: an ID in the trailer, no Encrypt, nothing after the last %%EOF but an end of line; the xref
    // keyword and each subsection header on lines of their own.
    private static Dictionary<int, long> CrossReference(byte[] pdf, List<string> violations)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var end = text.LastIndexOf("%%EOF", StringComparison.Ordinal);
        if (end < 0 || text[(end + 5)..].Trim('\r', '\n').Length > 0)
        {
            violations.Add("6.1.3: data after the last %%EOF");
        }

        var trailer = text[text.LastIndexOf("trailer", StringComparison.Ordinal)..];
        if (!IdPattern.IsMatch(trailer))
        {
            violations.Add("6.1.3: the trailer has no ID of two strings");
        }

        if (trailer.Contains("/Encrypt", StringComparison.Ordinal))
        {
            violations.Add("6.1.3: the trailer has an Encrypt entry");
        }

        var tail = text[Math.Max(0, end - 40)..];
        var start = int.Parse(StartPattern.Match(tail).Groups[1].Value, CultureInfo.InvariantCulture);
        var section = XrefPattern.Match(text, start);
        if (!section.Success || section.Index != start)
        {
            violations.Add("6.1.4: the xref keyword and subsection header are not each followed by one end of line");
            return [];
        }

        var offsets = new Dictionary<int, long>();
        var count = int.Parse(section.Groups[2].Value, CultureInfo.InvariantCulture);
        var first = int.Parse(section.Groups[1].Value, CultureInfo.InvariantCulture);
        for (var i = 0; i < count; i++)
        {
            var entry = text.Substring(section.Index + section.Length + (i * 20), 20);
            if (entry[17] == 'n')
            {
                offsets[first + i] = long.Parse(entry[..10], CultureInfo.InvariantCulture);
            }
        }

        return offsets;
    }

    // 6.1.9: "N G obj" at its offset and "endobj" after an end of line; 6.1.7.1: "stream" followed by an end of line,
    // Length bytes, an end of line and "endstream", and no F, FFilter or FDecodeParms; 6.1.7.2: no LZW or Crypt filter.
    private static void Objects(byte[] pdf, PdfObjectStore store, Dictionary<int, long> offsets, List<string> violations)
    {
        var text = Encoding.Latin1.GetString(pdf);
        foreach (var (number, offset) in offsets)
        {
            var head = ObjectPattern.Match(text, (int)offset);
            var close = text.IndexOf("endobj", (int)offset, StringComparison.Ordinal);
            if (!head.Success || head.Index != offset || head.Groups[1].Value != number.ToString(CultureInfo.InvariantCulture) || close < 1 || text[close - 1] != '\n')
            {
                violations.Add($"6.1.9: object {number} is not delimited by obj and endobj lines");
                continue;
            }

            if (store.Resolve(new PdfReference(number, 0)) is not PdfStream stream)
            {
                continue;
            }

            var keyword = text.IndexOf("stream", (int)offset, StringComparison.Ordinal);
            var dataStart = keyword + (text[keyword + 6] == '\r' ? 8 : 7);
            var length = ((PdfNumber)stream["Length"]!).IntValue;
            if (text[keyword + 6] is not ('\n' or '\r') || text.Substring(dataStart + length, 10) != "\nendstream")
            {
                violations.Add($"6.1.7.1: stream {number} is not framed by its keywords and Length");
            }

            if (stream.ContainsKey("F") || stream.ContainsKey("FFilter") || stream.ContainsKey("FDecodeParms"))
            {
                violations.Add($"6.1.7.1: stream {number} refers to an external file");
            }

            var filters = store.Get(stream, "Filter") switch
            {
                PdfName name => [name.Value],
                PdfArray array => array.Items.OfType<PdfName>().Select(n => n.Value).ToList(),
                _ => new List<string>(),
            };
            if (filters.Any(f => f is "LZWDecode" or "Crypt"))
            {
                violations.Add($"6.1.7.2: stream {number} uses {string.Join(", ", filters)}");
            }
        }
    }

    // 6.2.3: a GTS_PDFA1 output intent whose destination profile is an ICC version 4 or earlier display or printer
    // profile; with an RGB profile its N is 3.
    private static void OutputIntent(PdfObjectStore store, List<string> violations)
    {
        var intents = store.Get<PdfArray>(store.Catalog, "OutputIntents")?.Items.Select(store.Resolve).OfType<PdfDictionary>().ToList() ?? [];
        var intent = intents.Find(i => store.Get(i, "S") is PdfName { Value: "GTS_PDFA1" });
        if (intent is null || store.Get(intent, "DestOutputProfile") is not PdfStream profile)
        {
            violations.Add("6.2.3: no GTS_PDFA1 output intent with a destination profile");
            return;
        }

        var icc = store.DecodeBytes(profile);
        var header = Encoding.ASCII.GetString(icc, 12, 12);
        if (icc.Length < 128 || BinaryPrimitives.ReadUInt32BigEndian(icc) != icc.Length || icc[8] > 4 || header[..4] is not ("mntr" or "prtr")
            || Encoding.ASCII.GetString(icc, 36, 4) != "acsp" || header[4..8] != "RGB " || store.Number(profile, "N") != 3)
        {
            violations.Add("6.2.3: the destination profile is not an RGB display or printer profile of version 4 or earlier");
        }
    }

    // 6.6.2.1: an unfiltered Metadata stream with an xpacket header without bytes or encoding; 6.6.2.3.1: predefined
    // schemas only; 6.6.4: pdfaid part 2 and the level; 6.6.3: the information dictionary entries equal their XMP ones.
    private static void Metadata(PdfObjectStore store, char level, List<string> violations)
    {
        if (store.Get(store.Catalog, "Metadata") is not PdfStream metadata || store.Get(metadata, "Type") is not PdfName { Value: "Metadata" }
            || store.Get(metadata, "Subtype") is not PdfName { Value: "XML" } || metadata.ContainsKey("Filter"))
        {
            violations.Add("6.6.2.1: no unfiltered XML Metadata stream in the catalog");
            return;
        }

        var packet = Encoding.UTF8.GetString(metadata.Data);
        var header = packet[..Math.Max(0, packet.IndexOf("?>", StringComparison.Ordinal))];
        if (header != "<?xpacket begin=\"" + (char)0xFEFF + "\" id=\"W5M0MpCehiHzreSzNTczkc9d\"" && header != "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"")
        {
            violations.Add("6.6.2.1: the xpacket header is not begin and id only");
        }

        var xml = new XmlDocument { XmlResolver = null };
        xml.LoadXml(packet.TrimStart((char)0xFEFF));
        var names = new XmlNamespaceManager(xml.NameTable);
        names.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
        names.AddNamespace("pdfaid", "http://www.aiim.org/pdfa/ns/id/");
        names.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
        names.AddNamespace("xmp", "http://ns.adobe.com/xap/1.0/");
        names.AddNamespace("pdf", "http://ns.adobe.com/pdf/1.3/");
        foreach (var node in xml.SelectNodes("//*|//@*")!.Cast<XmlNode>().Where(n => n.NamespaceURI.Length > 0 && !Namespaces.Contains(n.NamespaceURI)))
        {
            violations.Add("6.6.2.3.1: property " + node.Name + " of a schema that is not predefined");
        }

        string? Value(string path) => xml.SelectSingleNode(path, names)?.InnerText;
        if (Value("//pdfaid:part") != "2" || Value("//pdfaid:conformance") != level.ToString())
        {
            violations.Add("6.6.4: the PDF/A identification is not part 2, level " + level);
        }

        var info = store.Get<PdfDictionary>(store.Trailer, "Info");
        (string Key, string Path)[] pairs =
        [
            ("Title", "//dc:title/rdf:Alt/rdf:li"), ("Author", "//dc:creator/rdf:Seq/rdf:li"), ("Subject", "//dc:description/rdf:Alt/rdf:li"),
            ("Keywords", "//pdf:Keywords"), ("Creator", "//xmp:CreatorTool"), ("Producer", "//pdf:Producer"),
        ];
        foreach (var (key, path) in pairs.Where(p => (store.Get(info, p.Key) as PdfString)?.ToText() != Value(p.Path)))
        {
            violations.Add($"6.6.3: Info {key} differs from its XMP property");
        }

        if (store.Get(info, "CreationDate") is PdfString created
            && PdfDocument.ParseDate(created.ToText()) != (Value("//xmp:CreateDate") is { } date ? DateTimeOffset.Parse(date, CultureInfo.InvariantCulture) : null))
        {
            violations.Add("6.6.3: Info CreationDate differs from xmp:CreateDate");
        }
    }

    // Every object: implementation limits (6.1.13, names included), images (6.2.8), XObjects (6.2.9), graphics states
    // (6.2.5, 6.2.10), fonts (6.2.11.3.2, 6.2.11.4.1, 6.2.11.5) and actions (6.5.1).
    private static void Walk(PdfObjectStore store, PdfObject? value, List<string> violations, HashSet<PdfObject> visited, int depth)
    {
        if (value is null || depth > 64)
        {
            return;
        }

        if (value is PdfName or PdfNumber or PdfString)
        {
            Scalar(value, violations);
            return;
        }

        if (!visited.Add(value))
        {
            return;
        }

        if (value is PdfArray array)
        {
            array.Items.ForEach(item => Walk(store, item is PdfReference ? null : item, violations, visited, depth + 1));
            return;
        }

        if (value is not PdfDictionary dictionary)
        {
            return;
        }

        foreach (var (key, entry) in dictionary.Entries)
        {
            Scalar(PdfName.Of(key), violations);
            Walk(store, entry is PdfReference ? null : entry, violations, visited, depth + 1);
        }

        Dictionary(store, dictionary, violations);
    }

    private static void Scalar(PdfObject value, List<string> violations)
    {
        switch (value)
        {
            case PdfNumber { IsInteger: true } n when n.Value is > int.MaxValue or < int.MinValue:
            case PdfNumber n2 when Math.Abs(n2.Value) > 3.403e38:
                violations.Add("6.1.13: a number outside the implementation limits");
                break;
            case PdfString s when s.Bytes.Length > 32767:
                violations.Add("6.1.13: a string longer than 32767 bytes");
                break;
            case PdfName name when Encoding.UTF8.GetByteCount(name.Value) > 127:
                violations.Add("6.1.13: a name longer than 127 bytes");
                break;
        }
    }

    private static void Dictionary(PdfObjectStore store, PdfDictionary dictionary, List<string> violations)
    {
        var type = (store.Get(dictionary, "Type") as PdfName)?.Value;
        var subtype = (store.Get(dictionary, "Subtype") as PdfName)?.Value;
        if (subtype == "Image")
        {
            Image(store, dictionary, violations);
        }
        else if (subtype is "PS" || dictionary.ContainsKey("Subtype2") || (subtype == "Form" && (dictionary.ContainsKey("OPI") || dictionary.ContainsKey("Ref"))))
        {
            violations.Add("6.2.9: a PostScript, OPI or reference XObject");
        }

        if (type == "ExtGState" || dictionary.ContainsKey("ca") || dictionary.ContainsKey("CA"))
        {
            GraphicsState(store, dictionary, violations);
        }

        if (subtype == "Type0")
        {
            CompositeFont(store, dictionary, violations);
        }
        else if (type == "Font" && subtype is "Type1" or "TrueType" or "MMType1"
            && store.Get<PdfDictionary>(dictionary, "FontDescriptor") is var descriptor && !new[] { "FontFile", "FontFile2", "FontFile3" }.Any(k => descriptor?.ContainsKey(k) == true))
        {
            violations.Add("6.2.11.4.1: a simple font without its program");
        }

        if (store.Get(dictionary, "S") is PdfName action && ForbiddenActions.Contains(action.Value))
        {
            violations.Add("6.5.1: a " + action.Value + " action");
        }
    }

    private static void Image(PdfObjectStore store, PdfDictionary image, List<string> violations)
    {
        if (image.ContainsKey("Alternates") || image.ContainsKey("OPI") || store.Get(image, "Interpolate") is PdfBoolean { Value: true }
            || store.Get(image, "BitsPerComponent") is PdfNumber { IntValue: not (1 or 2 or 4 or 8 or 16) })
        {
            violations.Add("6.2.8: an image with Alternates, OPI, Interpolate or an unusual bit depth");
        }

        if (store.Get(image, "ColorSpace") is PdfName { Value: "DeviceCMYK" })
        {
            violations.Add("6.2.4.3: a DeviceCMYK image under an RGB output intent");
        }
    }

    private static void GraphicsState(PdfObjectStore store, PdfDictionary state, List<string> violations)
    {
        if (state.ContainsKey("TR") || state.ContainsKey("HTP") || state.ContainsKey("HTO") || (state.ContainsKey("TR2") && store.Get(state, "TR2") is not PdfName { Value: "Default" }))
        {
            violations.Add("6.2.5: a graphics state with a transfer function or halftone origin");
        }

        if (store.Get(state, "BM") is PdfName mode && !BlendModes.Contains(mode.Value))
        {
            violations.Add("6.2.10: blend mode " + mode.Value);
        }
    }

    // 6.2.11.3.2: a CIDToGIDMap; 6.2.11.4.1: the program embedded; 6.2.11.5: each width of W equals the program's
    // advance (in 1000ths of the em, rounded) of the glyph its CID maps to.
    private static void CompositeFont(PdfObjectStore store, PdfDictionary font, List<string> violations)
    {
        var cidFont = store.Get<PdfArray>(font, "DescendantFonts")?.Items.Select(store.Resolve).OfType<PdfDictionary>().FirstOrDefault();
        var program = store.Get(store.Get<PdfDictionary>(cidFont, "FontDescriptor"), "FontFile2") as PdfStream;
        if (program is null)
        {
            violations.Add("6.2.11.4.1: a composite font without its TrueType program");
            return;
        }

        if (store.Get(cidFont, "CIDToGIDMap") is not PdfStream map)
        {
            violations.Add("6.2.11.3.2: a CIDFontType2 without a CIDToGIDMap stream");
            return;
        }

        var gids = store.DecodeBytes(map);
        var face = TrueTypeFont.Load(store.DecodeBytes(program));
        var widths = store.Get<PdfArray>(cidFont, "W")?.Items ?? [];
        for (var i = 0; i + 1 < widths.Count; i += 2)
        {
            var first = ((PdfNumber)widths[i]).IntValue;
            var run = (PdfArray)store.Resolve(widths[i + 1])!;
            for (var j = 0; j < run.Count; j++)
            {
                var cid = first + j;
                var gid = (cid * 2) + 1 < gids.Length ? (gids[cid * 2] << 8) | gids[(cid * 2) + 1] : 0;
                if (Math.Abs(((PdfNumber)run[j]).Value - Math.Round(face.GetAdvanceWidth(gid) * 1000.0 / face.UnitsPerEm)) > 1)
                {
                    violations.Add($"6.2.11.5: width of CID {cid} differs from the font program");
                }
            }
        }
    }

    // 6.2.2: only defined operators; 6.1.13: q nested at most 28 deep; 6.2.4.3: no CMYK colour under an RGB intent;
    // 6.2.11.8: no code showing glyph 0; 6.2.11.7.2 (level U): each code shown maps to Unicode in its ToUnicode CMap.
    private static void Content(PdfObjectStore store, byte[] content, PdfDictionary? resources, char level, List<string> violations, int depth)
    {
        var nesting = 0;
        PdfDictionary? font = null;
        foreach (var operation in ContentStreamReader.Read(content))
        {
            var op = operation.Operator;
            if (!Operators.Contains(op))
            {
                violations.Add("6.2.2: undefined operator " + op);
            }

            nesting += op switch { "q" => 1, "Q" => -1, _ => 0 };
            if (nesting > 28)
            {
                violations.Add("6.1.13: q nested more than 28 deep");
                nesting = 0;
            }

            if (op is "k" or "K" || (op is "cs" or "CS" && operation.Operands.FirstOrDefault() is PdfName { Value: "DeviceCMYK" }))
            {
                violations.Add("6.2.4.3: CMYK colour under an RGB output intent");
            }

            font = op == "Tf" && operation.Operands.Count >= 2 ? store.Get(store.Get<PdfDictionary>(resources, "Font"), ((PdfName)operation.Operands[^2]).Value) as PdfDictionary : font;
            foreach (var shown in Shown(operation))
            {
                Codes(store, font, shown, level, violations);
            }

            if (op == "Do" && depth < 8 && store.Get(store.Get<PdfDictionary>(resources, "XObject"), ((PdfName)operation.Operands[0]).Value) is PdfStream { } form
                && store.Get(form, "Subtype") is PdfName { Value: "Form" })
            {
                Content(store, store.DecodeBytes(form), store.Get<PdfDictionary>(form, "Resources") ?? resources, level, violations, depth + 1);
            }
        }
    }

    private static IEnumerable<PdfString> Shown(ContentOperation operation) => operation.Operator switch
    {
        "Tj" or "'" or "\"" => operation.Operands.Count > 0 && operation.Operands[^1] is PdfString s ? [s] : [],
        "TJ" => operation.Operands.Count > 0 && operation.Operands[^1] is PdfArray a ? a.Items.OfType<PdfString>() : [],
        _ => [],
    };

    private static void Codes(PdfObjectStore store, PdfDictionary? font, PdfString shown, char level, List<string> violations)
    {
        if (store.Get(font, "Subtype") is not PdfName { Value: "Type0" })
        {
            return;
        }

        var cidFont = store.Get<PdfArray>(font, "DescendantFonts")?.Items.Select(store.Resolve).OfType<PdfDictionary>().FirstOrDefault();
        var gids = store.Get(cidFont, "CIDToGIDMap") is PdfStream map ? store.DecodeBytes(map) : [];
        var unicode = store.Get(font, "ToUnicode") is PdfStream cmap ? PdfCMap.Parse(store.DecodeBytes(cmap)) : null;
        for (var i = 0; i + 1 < shown.Bytes.Length; i += 2)
        {
            var code = (shown.Bytes[i] << 8) | shown.Bytes[i + 1];
            var gid = (code * 2) + 1 < gids.Length ? (gids[code * 2] << 8) | gids[(code * 2) + 1] : 0;
            if (gid == 0)
            {
                violations.Add("6.2.11.8: code " + code + " shows the .notdef glyph");
            }

            if (level == 'U' && unicode?.ToUnicode((uint)code) is not { Length: > 0 } text)
            {
                violations.Add("6.2.11.7.2: code " + code + " has no ToUnicode entry");
            }
        }
    }

    // 6.3.2: every annotation printable and not hidden, invisible, no-view or toggle-no-view; 6.3.3: an appearance
    // dictionary holding only N, except for links and pop-ups.
    private static void Annotations(PdfObjectStore store, PdfPage page, List<string> violations)
    {
        foreach (var annotation in store.Get<PdfArray>(page.Dictionary, "Annots")?.Items.Select(store.Resolve).OfType<PdfDictionary>() ?? [])
        {
            var flags = (int)store.Number(annotation, "F");
            if ((flags & 4) == 0 || (flags & (1 | 2 | 32 | 256)) != 0)
            {
                violations.Add("6.3.2: an annotation that is not printable or is hidden");
            }

            var subtype = (store.Get(annotation, "Subtype") as PdfName)?.Value;
            if (subtype is not ("Link" or "Popup") && store.Get<PdfDictionary>(annotation, "AP") is not { Entries: [{ Key: "N" }] })
            {
                violations.Add("6.3.3: a " + subtype + " annotation without an appearance dictionary of N only");
            }
        }
    }

}
