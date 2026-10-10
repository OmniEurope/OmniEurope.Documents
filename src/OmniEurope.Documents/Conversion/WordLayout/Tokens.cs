// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>What painting needs to know about the page: canvas and page-number fields.</summary>
internal sealed class PaintContext(PdfCanvas canvas, LayoutContext layout)
{
    public PdfCanvas Canvas { get; } = canvas;

    public LayoutContext Layout { get; } = layout;

    public int PageNumber { get; init; } = 1;

    public WordNumberFormat PageFormat { get; init; } = WordNumberFormat.Decimal;

    public int PageCount { get; init; } = 1;

    public int SectionPages { get; init; } = 1;

    /// <summary>Left edge and width of the text column the item belongs to (for floating shapes).</summary>
    public (double Left, double Width) Column { get; set; }

    /// <summary>The page margins box (left, top, width, height) for floating shapes.</summary>
    public (double Left, double Top, double Width, double Height) Margins { get; init; }

    /// <summary>The page size, for floating shapes.</summary>
    public (double Width, double Height) PageSize { get; init; }

    /// <summary>Where the floating shapes of the page body were placed, by anchor.</summary>
    public IReadOnlyDictionary<AnchorToken, (double X, double Y)> Floats { get; init; } = new Dictionary<AnchorToken, (double X, double Y)>();

    public string FieldText(string kind) => kind switch
    {
        "NUMPAGES" => PageCount.ToString(CultureInfo.InvariantCulture),
        "SECTIONPAGES" => SectionPages.ToString(CultureInfo.InvariantCulture),
        _ => WordNumbering.FormatNumber(PageNumber, PageFormat),
    };
}

/// <summary>A unit of inline content for line breaking.</summary>
internal abstract class Token(TextStyle style)
{
    public TextStyle Style { get; } = style;

    /// <summary>Natural width (tabs get theirs while lines are built).</summary>
    public double Width { get; set; }

    /// <summary>A line may break before this token (after a space or a hyphen).</summary>
    public bool BreakBefore { get; set; }

    /// <summary>The bidirectional embedding level of each character of a text token (one entry for other tokens);
    /// null in a paragraph that is all left to right.</summary>
    public byte[]? Levels { get; set; }

    /// <summary>Height above the baseline.</summary>
    public abstract double Ascent(LayoutContext context);

    /// <summary>Depth below the baseline.</summary>
    public abstract double Descent(LayoutContext context);
}

/// <summary>A word or a run of spaces in one style.</summary>
internal sealed class TextToken(string text, TextStyle style) : Token(style)
{
    public string Text { get; set; } = text;

    public bool IsSpace => Text.Length > 0 && Text.All(char.IsWhiteSpace);

    /// <summary>The note this token marks, if it is a note reference.</summary>
    public (WordNoteKind Kind, int Id)? Note { get; init; }

    public override double Ascent(LayoutContext context) => context.LineMetrics(Style).Ascent + Style.Shift;

    public override double Descent(LayoutContext context) => Math.Max(0, context.Metrics(Style).Descent - Style.Shift);
}

/// <summary>A tab; with an alignment it is a positional tab measured from the margins.</summary>
internal sealed class TabToken(TextStyle style, WordTab? positional) : Token(style)
{
    public WordTab? Positional { get; } = positional;

    /// <summary>The leader filling the tab, set while lines are built.</summary>
    public WordTabLeader Leader { get; set; }

    public override double Ascent(LayoutContext context) => context.LineMetrics(Style).Ascent;

    public override double Descent(LayoutContext context) => context.Metrics(Style).Descent;
}

/// <summary>A line, page or column break.</summary>
internal sealed class BreakToken(TextStyle style, WordBreakKind kind) : Token(style)
{
    public WordBreakKind Kind { get; } = kind;

    public override double Ascent(LayoutContext context) => context.LineMetrics(Style).Ascent;

    public override double Descent(LayoutContext context) => context.Metrics(Style).Descent;
}

/// <summary>A page-number field, drawn with the value of the page it lands on.</summary>
internal sealed class FieldToken(TextStyle style, string kind) : Token(style)
{
    public string Kind { get; } = kind;

    public override double Ascent(LayoutContext context) => context.LineMetrics(Style).Ascent + Style.Shift;

    public override double Descent(LayoutContext context) => context.Metrics(Style).Descent;
}

/// <summary>An inline box (picture or text box) standing on the baseline.</summary>
internal sealed class BoxToken(TextStyle style, double width, double height, Action<PaintContext, double, double> paint) : Token(style)
{
    public double Height { get; } = height;

    /// <summary>Paints the box with its top-left corner at the given point.</summary>
    public Action<PaintContext, double, double> Paint { get; } = paint;

    public double BoxWidth { get; } = width;

    public override double Ascent(LayoutContext context) => Height;

    public override double Descent(LayoutContext context) => 0;
}

/// <summary>A floating shape anchored in the paragraph; it takes no room in the line.</summary>
internal sealed class AnchorToken(TextStyle style, WordShape shape, Action<PaintContext, double, double> paint) : Token(style)
{
    public WordShape Shape { get; } = shape;

    public Action<PaintContext, double, double> Paint { get; } = paint;

    /// <summary>The height the shape takes (a text box fitting its text may be taller than its stated height).</summary>
    public double Height { get; init; } = shape.Height;

    public override double Ascent(LayoutContext context) => 0;

    public override double Descent(LayoutContext context) => 0;
}
