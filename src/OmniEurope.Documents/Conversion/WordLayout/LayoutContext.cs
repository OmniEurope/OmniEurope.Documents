// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Shared state of one conversion: the source document, the PDF being built, list counters,
/// note numbering, image cache and the gaps met.</summary>
internal sealed class LayoutContext
{
    private readonly Dictionary<WordImage, PdfImage?> _images = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(PdfFont, double), Pdf.Writing.PdfFontMetrics> _metrics = [];
    private readonly Dictionary<(PdfFont, double), Pdf.Writing.PdfFontMetrics> _lineMetrics = [];
    private readonly HashSet<string> _checkedFonts = new(StringComparer.OrdinalIgnoreCase);

    public LayoutContext(WordDocument document, PdfDocumentBuilder builder)
    {
        Document = document;
        Builder = builder;
        Lists = new WordListCounter(document.Numbering);
        Notes = new NoteNumbering(document);
    }

    public WordDocument Document { get; }

    public PdfDocumentBuilder Builder { get; }

    public WordStyleSheet Styles => Document.Styles;

    public WordListCounter Lists { get; private set; }

    /// <summary>Note labels in reading order, with the footnote restarts of the settings and sections.</summary>
    public NoteNumbering Notes { get; }

    public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

    public double Measure(string text, TextStyle style) => Builder.MeasureText(text, style.Font, style.Size, style.CharacterSpacing);

    public Pdf.Writing.PdfFontMetrics Metrics(TextStyle style)
    {
        CheckFont(style.Font.Family);
        if (!_metrics.TryGetValue((style.Font, style.Size), out var metrics))
        {
            metrics = Builder.Metrics(style.Font, style.Size);
            _metrics[(style.Font, style.Size)] = metrics;
        }

        return metrics;
    }

    /// <summary>
    /// The metrics a line is laid out with: Word's single line is the font's Windows ascent and descent plus its
    /// external leading, above the text (<see cref="Metrics"/> keeps the font's own ascent, for painting).
    /// </summary>
    public Pdf.Writing.PdfFontMetrics LineMetrics(TextStyle style)
    {
        if (!_lineMetrics.TryGetValue((style.Font, style.Size), out var line))
        {
            var metrics = Metrics(style);
            var leading = Builder.ExternalLeading(style.Font, style.Size);
            line = metrics with { Ascent = metrics.Ascent + leading, LineHeight = metrics.LineHeight + leading };
            _lineMetrics[(style.Font, style.Size)] = line;
        }

        return line;
    }

    /// <summary>
    /// After a layout, prepares another one when footnotes restarting at each page were numbered without
    /// knowing the page they landed on; returns false when the layout made can be kept.
    /// </summary>
    public bool Restart(IReadOnlyList<PageFrame> pages)
    {
        var placed = new Dictionary<int, int>();
        for (var page = 0; page < pages.Count; page++)
        {
            foreach (var note in pages[page].Footnotes)
            {
                placed.TryAdd(note.Id, page);
            }
        }

        if (!Notes.Restart(placed))
        {
            return false;
        }

        Lists = new WordListCounter(Document.Numbering);
        return true;
    }

    /// <summary>The PDF image of a picture, or null when its format cannot be embedded.</summary>
    public PdfImage? Image(WordImage image)
    {
        if (_images.TryGetValue(image, out var cached))
        {
            return cached;
        }

        PdfImage? result = null;
        try
        {
            result = image.ContentType is "image/png" or "image/jpeg" or "image/gif" or "image/bmp" or "image/tiff"
                ? Builder.AddImage(image.Data)
                : null;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException)
        {
            Gaps.Add("unreadable picture replaced by a frame");
        }

        _images[image] = result;
        return result;
    }

    // A family that no bundled or registered font matches is drawn with a look-alike, which is reported.
    private void CheckFont(string family)
    {
        if (_checkedFonts.Add(family) && Builder.Fonts.IsSubstituted(family))
        {
            Gaps.Add($"font \"{family}\" replaced by {FontLibrary.BundledStem(family)}");
        }
    }
}
