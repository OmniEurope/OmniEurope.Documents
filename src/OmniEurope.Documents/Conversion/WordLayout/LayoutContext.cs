// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Shared state of one conversion: the source document, the PDF being built, list counters,
/// note labels, image cache and the gaps met.</summary>
internal sealed class LayoutContext
{
    private readonly Dictionary<(WordNoteKind, int), string> _noteLabels = [];
    private readonly Dictionary<WordImage, PdfImage?> _images = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(PdfFont, double), Pdf.Writing.PdfFontMetrics> _metrics = [];
    private readonly HashSet<string> _checkedFonts = new(StringComparer.OrdinalIgnoreCase);

    public LayoutContext(WordDocument document, PdfDocumentBuilder builder)
    {
        Document = document;
        Builder = builder;
        Lists = new WordListCounter(document.Numbering);
    }

    public WordDocument Document { get; }

    public PdfDocumentBuilder Builder { get; }

    public WordStyleSheet Styles => Document.Styles;

    public WordListCounter Lists { get; }

    public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

    /// <summary>The order in which footnotes were referenced (their reading order).</summary>
    public List<int> FootnoteOrder { get; } = [];

    /// <summary>The order in which endnotes were referenced.</summary>
    public List<int> EndnoteOrder { get; } = [];

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
    /// The label of a note reference: numbered in reading order with the document's number style (a
    /// restart at each page or section is not applied).
    /// </summary>
    public string NoteLabel(WordNoteKind kind, int id)
    {
        if (_noteLabels.TryGetValue((kind, id), out var label))
        {
            return label;
        }

        var settings = Document.Settings;
        var order = kind == WordNoteKind.Footnote ? FootnoteOrder : EndnoteOrder;
        order.Add(id);
        var (format, start) = kind == WordNoteKind.Footnote ? (settings.FootnoteFormat, settings.FootnoteStart) : (settings.EndnoteFormat, settings.EndnoteStart);
        if (kind == WordNoteKind.Footnote && settings.FootnoteRestart != WordNoteRestart.Continuous)
        {
            Gaps.Add("footnote numbering restarts are not applied");
        }

        label = WordNumbering.FormatNumber(start + order.Count - 1, format);
        _noteLabels[(kind, id)] = label;
        return label;
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
