// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>
/// A TrueType face used by a document being written. Text is shown with two-byte codes equal to the
/// original glyph indexes (Identity-H); at save time the face is subset, a CIDToGIDMap sends each code to
/// its glyph in the subset, and a ToUnicode CMap keeps the text extractable.
/// </summary>
internal sealed class EmbeddedFont(TrueTypeFont font, string resourceName)
{
    private readonly SortedDictionary<int, string> _glyphText = [];

    public TrueTypeFont Font { get; } = font;

    public string ResourceName { get; } = resourceName;

    public PdfReference? Reference { get; set; }

    public void Use(int glyph, string text) => _glyphText.TryAdd(glyph, text);

    public void WriteObjects(PdfObjectTable table, PdfReference reference)
    {
        var subset = TrueTypeSubsetter.Subset(Font, _glyphText.Keys);
        var baseName = SubsetTag() + "+" + SafeName(Font.Names.PostScriptName.Length > 0 ? Font.Names.PostScriptName : Font.Names.Family);
        var scale = 1000.0 / Font.UnitsPerEm;
        var fontFile = Stream(subset.Data).SetNumber("Length1", subset.Data.Length);
        var descriptor = new PdfDictionary()
            .SetName("Type", "FontDescriptor")
            .SetName("FontName", baseName)
            .SetNumber("Flags", 4 + (Font.IsFixedPitch ? 1 : 0) + (Font.IsItalic ? 64 : 0))
            .Set("FontBBox", PdfArray.OfNumbers(Math.Round(Font.BoundingBox.XMin * scale), Math.Round(Font.BoundingBox.YMin * scale), Math.Round(Font.BoundingBox.XMax * scale), Math.Round(Font.BoundingBox.YMax * scale)))
            .SetNumber("ItalicAngle", Math.Round(Font.ItalicAngle, 2))
            .SetNumber("Ascent", Math.Round(Font.Ascender * scale))
            .SetNumber("Descent", Math.Round(Font.Descender * scale))
            .SetNumber("CapHeight", Math.Round(Font.CapHeight * scale))
            .SetNumber("StemV", Font.IsBold ? 120 : 80)
            .Set("FontFile2", table.Add(fontFile));
        var cidFont = new PdfDictionary()
            .SetName("Type", "Font")
            .SetName("Subtype", "CIDFontType2")
            .SetName("BaseFont", baseName)
            .Set("CIDSystemInfo", new PdfDictionary().Set("Registry", PdfString.FromText("Adobe")).Set("Ordering", PdfString.FromText("Identity")).SetNumber("Supplement", 0))
            .Set("FontDescriptor", table.Add(descriptor))
            .SetNumber("DW", Math.Round(Font.GetAdvanceWidth(0) * scale))
            .Set("W", Widths(scale))
            .Set("CIDToGIDMap", table.Add(Stream(CidToGid(subset.GlyphMap))));
        var type0 = new PdfDictionary()
            .SetName("Type", "Font")
            .SetName("Subtype", "Type0")
            .SetName("BaseFont", baseName)
            .SetName("Encoding", "Identity-H")
            .Set("DescendantFonts", new PdfArray(table.Add(cidFont)))
            .Set("ToUnicode", table.Add(Stream(Encoding.ASCII.GetBytes(ToUnicode()))));
        table.Set(reference, type0);
    }

    internal static PdfStream Stream(byte[] data)
    {
        var stream = new PdfStream(PngCodec.Deflate(data, CompressionLevel.Optimal));
        stream.SetName("Filter", "FlateDecode");
        return stream;
    }

    private PdfArray Widths(double scale)
    {
        var array = new PdfArray();
        PdfArray? run = null;
        var previous = -2;
        foreach (var glyph in _glyphText.Keys)
        {
            if (glyph != previous + 1 || run is null)
            {
                run = new PdfArray();
                array.Items.Add(PdfNumber.Of(glyph));
                array.Items.Add(run);
            }

            run.Items.Add(PdfNumber.Of((long)Math.Round(Font.GetAdvanceWidth(glyph) * scale)));
            previous = glyph;
        }

        return array;
    }

    private byte[] CidToGid(IReadOnlyDictionary<int, int> map)
    {
        var max = _glyphText.Keys.DefaultIfEmpty(0).Max();
        var data = new byte[(max + 1) * 2];
        foreach (var (cid, gid) in map)
        {
            if (cid <= max)
            {
                data[cid * 2] = (byte)(gid >> 8);
                data[(cid * 2) + 1] = (byte)gid;
            }
        }

        return data;
    }

    private string ToUnicode()
    {
        var cmap = new StringBuilder();
        cmap.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n");
        cmap.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
        cmap.Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
        cmap.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        var entries = _glyphText.Where(e => e.Key != 0 && e.Value.Length > 0).ToList();
        for (var i = 0; i < entries.Count; i += 100)
        {
            var block = entries.Skip(i).Take(100).ToList();
            cmap.Append(block.Count.ToString(CultureInfo.InvariantCulture)).Append(" beginbfchar\n");
            foreach (var (glyph, text) in block)
            {
                cmap.Append('<').Append(glyph.ToString("X4", CultureInfo.InvariantCulture)).Append("> <")
                    .Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))).Append(">\n");
            }

            cmap.Append("endbfchar\n");
        }

        cmap.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        return cmap.ToString();
    }

    // Six capitals derived from the glyph set, as PDF expects for subset fonts.
    private string SubsetTag()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Font.Names.PostScriptName + string.Join(',', _glyphText.Keys)));
        return new string(hash.Take(6).Select(b => (char)('A' + (b % 26))).ToArray());
    }

    private static string SafeName(string name) => new(name.Where(c => c is > ' ' and < '\u007F' and not ('/' or '[' or ']' or '(' or ')' or '<' or '>' or '{' or '}' or '%' or '#')).ToArray());
}
