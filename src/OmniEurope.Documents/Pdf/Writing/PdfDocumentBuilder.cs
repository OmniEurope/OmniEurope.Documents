// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>
/// Creates a PDF: add pages, draw on their <see cref="PdfCanvas"/>, then save. Fonts are embedded as
/// subsets (Unicode text stays searchable and copyable), images are embedded once even when drawn many
/// times, and the output is deterministic (same content, same bytes).
/// </summary>
public sealed class PdfDocumentBuilder
{
    private readonly PdfObjectTable _table;
    private readonly List<PdfCanvas> _pages = [];
    private readonly Dictionary<TrueTypeFont, EmbeddedFont> _fonts = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, PdfImage> _images = new(StringComparer.Ordinal);
    private readonly List<(string Title, PdfCanvas Page, double Y)> _bookmarks = [];
    private byte[]? _saved;

    /// <summary>Creates an empty document.</summary>
    public PdfDocumentBuilder()
        : this(1, string.Empty)
    {
    }

    // Objects numbered from firstNumber and prefixed resource names: drawing over pages of an existing file.
    internal PdfDocumentBuilder(int firstNumber, string resourcePrefix)
    {
        _table = new PdfObjectTable(firstNumber);
        ResourcePrefix = resourcePrefix;
    }

    /// <summary>The fonts the document draws with. Default: the bundled fonts.</summary>
    public FontLibrary Fonts { get; init; } = FontLibrary.Default;

    /// <summary>Document title (Info dictionary).</summary>
    public string? Title { get; set; }

    /// <summary>Author.</summary>
    public string? Author { get; set; }

    /// <summary>Subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Creating application.</summary>
    public string? Creator { get; set; }

    /// <summary>Creation date; none by default so equal documents stay byte-identical.</summary>
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary>The pages added so far.</summary>
    public IReadOnlyList<PdfCanvas> Pages => _pages;

    /// <summary>Adds a page (A4 portrait by default) and returns its canvas.</summary>
    public PdfCanvas AddPage(double width = 595.28, double height = 841.89)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 3);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 14400);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 14400);
        ThrowIfSaved();
        var page = new PdfCanvas(this, width, height);
        _pages.Add(page);
        return page;
    }

    /// <summary>Adds an image file (PNG, JPEG, GIF, BMP, TIFF); the same bytes give the same image.</summary>
    public PdfImage AddImage(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var key = Convert.ToHexString(SHA256.HashData(file));
        if (!_images.TryGetValue(key, out var image))
        {
            image = PdfImage.Create(file, _table, ResourcePrefix + "Im" + (_images.Count + 1));
            _images.Add(key, image);
        }

        return image;
    }

    /// <summary>Adds decoded pixels as an image.</summary>
    public PdfImage AddImage(RasterImage pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var image = PdfImage.FromRaster(pixels, _table, ResourcePrefix + "Im" + (_images.Count + 1));
        _images.Add(Guid.NewGuid().ToString("N"), image);
        return image;
    }

    /// <summary>Adds an entry to the document outline (bookmarks panel) pointing at a page position.</summary>
    public void AddBookmark(string title, PdfCanvas page, double y = 0)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(page);
        _bookmarks.Add((title, page, y));
    }

    /// <summary>The width of <paramref name="text"/> in points.</summary>
    public double MeasureText(string text, PdfFont font, double size, double characterSpacing = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        double width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value < 0x20 && rune.Value != '\t')
            {
                continue;
            }

            var face = Face(font, rune.Value == '\t' ? ' ' : rune.Value);
            width += (face.GetAdvanceWidth(face.GetGlyphIndex(rune.Value == '\t' ? ' ' : rune.Value)) * size / face.UnitsPerEm) + characterSpacing;
        }

        return width;
    }

    /// <summary>The vertical metrics of a font at a size (from its main face).</summary>
    public PdfFontMetrics Metrics(PdfFont font, double size)
    {
        ArgumentNullException.ThrowIfNull(font);
        var face = Fonts.Resolve(font.Family, font.Bold, font.Italic);
        var scale = size / face.UnitsPerEm;
        return new PdfFontMetrics(face.WinAscent * scale, face.WinDescent * scale, (face.WinAscent + face.WinDescent) * scale,
            face.CapHeight * scale, -face.UnderlinePosition * scale, Math.Max(face.UnderlineThickness * scale, 0.25));
    }

    /// <summary>Writes the document. A document is finished once saved: saving again writes the same bytes,
    /// adding pages afterwards throws.</summary>
    public void Save(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_saved is not null)
        {
            output.Write(_saved);
            return;
        }

        if (_pages.Count == 0)
        {
            throw new InvalidOperationException("A PDF needs at least one page.");
        }

        var catalogRef = _table.Reserve();
        var pagesRef = _table.Reserve();
        var pageRefs = _pages.Select(p => p.WriteObjects(_table, pagesRef)).ToList();
        foreach (var font in _fonts.Values)
        {
            font.WriteObjects(_table, font.Reference!);
        }

        _table.Set(pagesRef, new PdfDictionary().SetName("Type", "Pages").Set("Kids", new PdfArray(pageRefs)).SetNumber("Count", pageRefs.Count));
        var catalog = new PdfDictionary().SetName("Type", "Catalog").Set("Pages", pagesRef);
        if (_bookmarks.Count > 0)
        {
            catalog.Set("Outlines", PdfOutlineWriter.Write(_table, _bookmarks.Select(b => (b.Title, pageRefs[_pages.IndexOf(b.Page)], b.Page.Height - b.Y)).ToList()));
            catalog.SetName("PageMode", "UseOutlines");
        }

        _table.Set(catalogRef, catalog);
        using var buffer = new MemoryStream();
        _table.Write(buffer, catalogRef, _table.Add(InfoDictionary()));
        _saved = buffer.ToArray();
        output.Write(_saved);
    }

    /// <summary>Writes the document to a byte array.</summary>
    public byte[] ToArray()
    {
        using var output = new MemoryStream();
        Save(output);
        return output.ToArray();
    }

    internal string ResourcePrefix { get; }

    /// <summary>A canvas that is not a page of this document (its content goes into another file).</summary>
    internal PdfCanvas CreateDetachedCanvas(double width, double height) => new(this, width, height);

    /// <summary>Writes the embedded fonts used so far (for a detached canvas).</summary>
    internal void WriteFonts()
    {
        foreach (var font in _fonts.Values)
        {
            font.WriteObjects(_table, font.Reference!);
        }
    }

    internal void ThrowIfSaved()
    {
        if (_saved is not null)
        {
            throw new InvalidOperationException("The document has already been saved.");
        }
    }

    internal TrueTypeFont Face(PdfFont font, int codePoint) => Fonts.ResolveForCharacter(font.Family, font.Bold, font.Italic, codePoint);

    internal EmbeddedFont Embed(TrueTypeFont face)
    {
        if (!_fonts.TryGetValue(face, out var font))
        {
            font = new EmbeddedFont(face, ResourcePrefix + "F" + (_fonts.Count + 1)) { Reference = _table.Reserve() };
            _fonts.Add(face, font);
        }

        return font;
    }

    internal PdfObjectTable Table => _table;

    private PdfDictionary InfoDictionary()
    {
        var info = new PdfDictionary().Set("Producer", PdfString.FromText("OmniEurope.Documents"));
        void Text(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                info.Set(key, PdfString.FromText(value));
            }
        }

        Text("Title", Title);
        Text("Author", Author);
        Text("Subject", Subject);
        Text("Keywords", Keywords);
        Text("Creator", Creator);
        if (CreationDate is { } date)
        {
            var offset = date.Offset;
            var sign = offset < TimeSpan.Zero ? '-' : '+';
            info.Set("CreationDate", PdfString.FromText($"D:{date:yyyyMMddHHmmss}{sign}{Math.Abs(offset.Hours):00}'{Math.Abs(offset.Minutes):00}'"));
        }

        return info;
    }
}

/// <summary>Writes a flat document outline.</summary>
internal static class PdfOutlineWriter
{
    public static PdfReference Write(PdfObjectTable table, List<(string Title, PdfReference Page, double Top)> entries)
    {
        var root = table.Reserve();
        var refs = entries.Select(_ => table.Reserve()).ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            var item = new PdfDictionary()
                .Set("Title", PdfString.FromText(entries[i].Title))
                .Set("Parent", root)
                .Set("Dest", new PdfArray(entries[i].Page, PdfName.Of("XYZ"), PdfNumber.Of(0), PdfNumber.Of(entries[i].Top), PdfNull.Instance))
                .Set("Prev", i > 0 ? refs[i - 1] : null)
                .Set("Next", i + 1 < refs.Count ? refs[i + 1] : null);
            table.Set(refs[i], item);
        }

        table.Set(root, new PdfDictionary().SetName("Type", "Outlines").Set("First", refs[0]).Set("Last", refs[^1]).SetNumber("Count", refs.Count));
        return root;
    }
}
