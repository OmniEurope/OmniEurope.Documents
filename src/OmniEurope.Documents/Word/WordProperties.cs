// SPDX-License-Identifier: EUPL-1.2
using static OmniEurope.Documents.Word.WordInherit;

namespace OmniEurope.Documents.Word;

/// <summary>Paragraph alignment.</summary>
public enum WordAlignment
{
    /// <summary>Aligned on the start (left) edge.</summary>
    Left,

    /// <summary>Centred.</summary>
    Center,

    /// <summary>Aligned on the end (right) edge.</summary>
    Right,

    /// <summary>Justified, last line on the start edge.</summary>
    Justify,

    /// <summary>Justified including the last line, spaces and letters stretched.</summary>
    Distribute,
}

/// <summary>Underline style.</summary>
public enum WordUnderline
{
    /// <summary>No underline.</summary>
    None,

    /// <summary>Single line.</summary>
    Single,

    /// <summary>Under words only, not spaces.</summary>
    Words,

    /// <summary>Double line.</summary>
    Double,

    /// <summary>Thick line.</summary>
    Thick,

    /// <summary>Dotted line.</summary>
    Dotted,

    /// <summary>Dashed line.</summary>
    Dash,

    /// <summary>Wavy line.</summary>
    Wave,
}

/// <summary>Vertical position of text on its line.</summary>
public enum WordVerticalPosition
{
    /// <summary>On the baseline.</summary>
    Baseline,

    /// <summary>Raised and smaller.</summary>
    Superscript,

    /// <summary>Lowered and smaller.</summary>
    Subscript,
}

/// <summary>How a line spacing value is applied.</summary>
public enum WordLineSpacingRule
{
    /// <summary>A multiple of single spacing (1 is single, 2 is double).</summary>
    Multiple,

    /// <summary>Exactly this many points.</summary>
    Exact,

    /// <summary>At least this many points.</summary>
    AtLeast,
}

/// <summary>Kind of a tab stop.</summary>
public enum WordTabAlignment
{
    /// <summary>Text starts at the stop.</summary>
    Left,

    /// <summary>Text is centred on the stop.</summary>
    Center,

    /// <summary>Text ends at the stop.</summary>
    Right,

    /// <summary>The decimal separator sits on the stop.</summary>
    Decimal,

    /// <summary>A vertical bar is drawn at the stop.</summary>
    Bar,

    /// <summary>Removes an inherited stop at this position.</summary>
    Clear,
}

/// <summary>Characters filling the space before a tab stop.</summary>
public enum WordTabLeader
{
    /// <summary>Blank.</summary>
    None,

    /// <summary>Dots.</summary>
    Dot,

    /// <summary>Hyphens.</summary>
    Hyphen,

    /// <summary>A line.</summary>
    Underscore,

    /// <summary>Centred dots.</summary>
    MiddleDot,

    /// <summary>A thick line.</summary>
    Heavy,
}

/// <summary>A tab stop at <paramref name="Position"/> points from the start indent.</summary>
public sealed record WordTabStop(double Position, WordTabAlignment Alignment = WordTabAlignment.Left, WordTabLeader Leader = WordTabLeader.None);

/// <summary>A border line: style name as in WordprocessingML (<c>single</c>, <c>double</c>, <c>dashed</c>,
/// <c>none</c>...), width and spacing in points, colour as <c>RRGGBB</c> or <c>auto</c>.</summary>
public sealed record WordBorder(string Style = "single", double Width = 0.5, double Space = 0, string Color = "auto")
{
    /// <summary>True when no line is drawn.</summary>
    public bool IsNone => Style is "none" or "nil";
}

/// <summary>Borders of a paragraph; <see cref="Between"/> separates paragraphs that share the same borders.</summary>
public sealed record WordParagraphBorders(WordBorder? Top = null, WordBorder? Left = null, WordBorder? Bottom = null, WordBorder? Right = null, WordBorder? Between = null);

/// <summary>
/// Character formatting. Every member is optional: null means "inherited" (from the character style, the
/// paragraph style, then the document defaults). Sizes and distances are in points.
/// </summary>
public sealed record WordRunProperties
{
    /// <summary>No formatting.</summary>
    public static WordRunProperties Empty { get; } = new();

    /// <summary>Character style id.</summary>
    public string? StyleId { get; init; }

    /// <summary>Font for Latin text.</summary>
    public string? Font { get; init; }

    /// <summary>Font for East Asian text.</summary>
    public string? FontEastAsia { get; init; }

    /// <summary>Font for complex scripts (Arabic, Hebrew...).</summary>
    public string? FontComplex { get; init; }

    /// <summary>Theme font slot for Latin text (<c>minorHAnsi</c>, <c>majorHAnsi</c>...), resolved into
    /// <see cref="Font"/> when the document has a theme.</summary>
    public string? FontTheme { get; init; }

    /// <summary>Bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>Underline.</summary>
    public WordUnderline? Underline { get; init; }

    /// <summary>Single strikethrough.</summary>
    public bool? Strike { get; init; }

    /// <summary>Double strikethrough.</summary>
    public bool? DoubleStrike { get; init; }

    /// <summary>All capitals.</summary>
    public bool? Caps { get; init; }

    /// <summary>Small capitals.</summary>
    public bool? SmallCaps { get; init; }

    /// <summary>Hidden text.</summary>
    public bool? Hidden { get; init; }

    /// <summary>Superscript or subscript.</summary>
    public WordVerticalPosition? VerticalPosition { get; init; }

    /// <summary>Font size in points.</summary>
    public double? FontSize { get; init; }

    /// <summary>Font size for complex scripts, in points.</summary>
    public double? FontSizeComplex { get; init; }

    /// <summary>Text colour as <c>RRGGBB</c> or <c>auto</c>.</summary>
    public string? Color { get; init; }

    /// <summary>Highlight colour name (<c>yellow</c>, <c>green</c>...).</summary>
    public string? Highlight { get; init; }

    /// <summary>Background fill as <c>RRGGBB</c>.</summary>
    public string? Shading { get; init; }

    /// <summary>Extra space between characters, in points.</summary>
    public double? CharacterSpacing { get; init; }

    /// <summary>Raise (positive) or lower the text, in points.</summary>
    public double? Position { get; init; }

    /// <summary>Language of Latin text (<c>fr-FR</c>).</summary>
    public string? Language { get; init; }

    /// <summary>Language of East Asian text.</summary>
    public string? LanguageEastAsia { get; init; }

    /// <summary>Language of complex-script text.</summary>
    public string? LanguageComplex { get; init; }

    /// <summary>Right-to-left run.</summary>
    public bool? RightToLeft { get; init; }

    /// <summary>The members of <paramref name="top"/> that are set replace those of this instance.</summary>
    public WordRunProperties Overlay(WordRunProperties? top)
    {
        if (top is null || ReferenceEquals(top, Empty))
        {
            return this;
        }

        return new WordRunProperties
        {
            StyleId = Pick(top.StyleId, StyleId),
            Font = Pick(top.Font, Font),
            FontEastAsia = Pick(top.FontEastAsia, FontEastAsia),
            FontComplex = Pick(top.FontComplex, FontComplex),
            FontTheme = top.Font is not null && top.FontTheme is null ? null : top.FontTheme ?? FontTheme,
            Bold = Pick(top.Bold, Bold),
            Italic = Pick(top.Italic, Italic),
            Underline = Pick(top.Underline, Underline),
            Strike = Pick(top.Strike, Strike),
            DoubleStrike = Pick(top.DoubleStrike, DoubleStrike),
            Caps = Pick(top.Caps, Caps),
            SmallCaps = Pick(top.SmallCaps, SmallCaps),
            Hidden = Pick(top.Hidden, Hidden),
            VerticalPosition = Pick(top.VerticalPosition, VerticalPosition),
            FontSize = Pick(top.FontSize, FontSize),
            FontSizeComplex = Pick(top.FontSizeComplex, FontSizeComplex),
            Color = Pick(top.Color, Color),
            Highlight = Pick(top.Highlight, Highlight),
            Shading = Pick(top.Shading, Shading),
            CharacterSpacing = Pick(top.CharacterSpacing, CharacterSpacing),
            Position = Pick(top.Position, Position),
            Language = Pick(top.Language, Language),
            LanguageEastAsia = Pick(top.LanguageEastAsia, LanguageEastAsia),
            LanguageComplex = Pick(top.LanguageComplex, LanguageComplex),
            RightToLeft = Pick(top.RightToLeft, RightToLeft),
        };
    }
}

/// <summary>
/// Paragraph formatting. Every member is optional: null means "inherited" (from numbering, the paragraph
/// style chain, then the document defaults). Distances are in points.
/// </summary>
public sealed record WordParagraphProperties
{
    /// <summary>No formatting.</summary>
    public static WordParagraphProperties Empty { get; } = new();

    /// <summary>Paragraph style id.</summary>
    public string? StyleId { get; init; }

    /// <summary>Alignment.</summary>
    public WordAlignment? Alignment { get; init; }

    /// <summary>Start (left) indent.</summary>
    public double? IndentLeft { get; init; }

    /// <summary>End (right) indent.</summary>
    public double? IndentRight { get; init; }

    /// <summary>First-line indent relative to <see cref="IndentLeft"/>; negative for a hanging indent.</summary>
    public double? FirstLineIndent { get; init; }

    /// <summary>Space above.</summary>
    public double? SpacingBefore { get; init; }

    /// <summary>Space below.</summary>
    public double? SpacingAfter { get; init; }

    /// <summary>Line spacing: a multiple or a number of points, see <see cref="LineSpacingRule"/>.</summary>
    public double? LineSpacing { get; init; }

    /// <summary>How <see cref="LineSpacing"/> applies.</summary>
    public WordLineSpacingRule? LineSpacingRule { get; init; }

    /// <summary>Ignore space before and after between paragraphs of the same style.</summary>
    public bool? ContextualSpacing { get; init; }

    /// <summary>Keep on the same page as the next paragraph.</summary>
    public bool? KeepNext { get; init; }

    /// <summary>Keep all lines on one page.</summary>
    public bool? KeepLines { get; init; }

    /// <summary>Start on a new page.</summary>
    public bool? PageBreakBefore { get; init; }

    /// <summary>Avoid a single first or last line on a page.</summary>
    public bool? WidowControl { get; init; }

    /// <summary>Outline level, 0 for a top-level heading.</summary>
    public int? OutlineLevel { get; init; }

    /// <summary>Numbering instance id; 0 removes inherited numbering.</summary>
    public int? NumberingId { get; init; }

    /// <summary>Numbering level, from 0.</summary>
    public int? NumberingLevel { get; init; }

    /// <summary>Tab stops (a <see cref="WordTabAlignment.Clear"/> stop removes an inherited one).</summary>
    public IReadOnlyList<WordTabStop>? Tabs { get; init; }

    /// <summary>Background fill as <c>RRGGBB</c>.</summary>
    public string? Shading { get; init; }

    /// <summary>Borders.</summary>
    public WordParagraphBorders? Borders { get; init; }

    /// <summary>Right-to-left paragraph.</summary>
    public bool? RightToLeft { get; init; }

    /// <summary>Formatting of the paragraph mark (also used for an empty paragraph's height).</summary>
    public WordRunProperties? MarkProperties { get; init; }

    /// <summary>The members of <paramref name="top"/> that are set replace those of this instance; tab
    /// stops are merged, a cleared stop removing the inherited one at its position.</summary>
    public WordParagraphProperties Overlay(WordParagraphProperties? top)
    {
        if (top is null || ReferenceEquals(top, Empty))
        {
            return this;
        }

        return new WordParagraphProperties
        {
            StyleId = Pick(top.StyleId, StyleId),
            Alignment = Pick(top.Alignment, Alignment),
            IndentLeft = Pick(top.IndentLeft, IndentLeft),
            IndentRight = Pick(top.IndentRight, IndentRight),
            FirstLineIndent = Pick(top.FirstLineIndent, FirstLineIndent),
            SpacingBefore = Pick(top.SpacingBefore, SpacingBefore),
            SpacingAfter = Pick(top.SpacingAfter, SpacingAfter),
            LineSpacing = Pick(top.LineSpacing, LineSpacing),
            LineSpacingRule = top.LineSpacing is null ? LineSpacingRule : top.LineSpacingRule ?? LineSpacingRule,
            ContextualSpacing = Pick(top.ContextualSpacing, ContextualSpacing),
            KeepNext = Pick(top.KeepNext, KeepNext),
            KeepLines = Pick(top.KeepLines, KeepLines),
            PageBreakBefore = Pick(top.PageBreakBefore, PageBreakBefore),
            WidowControl = Pick(top.WidowControl, WidowControl),
            OutlineLevel = Pick(top.OutlineLevel, OutlineLevel),
            NumberingId = Pick(top.NumberingId, NumberingId),
            NumberingLevel = top.NumberingLevel ?? (top.NumberingId is null ? NumberingLevel : null),
            Tabs = MergeTabs(Tabs, top.Tabs),
            Shading = Pick(top.Shading, Shading),
            Borders = Pick(top.Borders, Borders),
            RightToLeft = Pick(top.RightToLeft, RightToLeft),
            MarkProperties = MarkProperties is null ? top.MarkProperties : MarkProperties.Overlay(top.MarkProperties),
        };
    }

    private static IReadOnlyList<WordTabStop>? MergeTabs(IReadOnlyList<WordTabStop>? below, IReadOnlyList<WordTabStop>? above)
    {
        if (above is null || below is null)
        {
            return above ?? below;
        }

        var cleared = above.Where(t => t.Alignment == WordTabAlignment.Clear).Select(t => Math.Round(t.Position, 2)).ToHashSet();
        var set = above.Where(t => t.Alignment != WordTabAlignment.Clear).ToList();
        var positions = set.Select(t => Math.Round(t.Position, 2)).ToHashSet();
        return below.Where(t => !cleared.Contains(Math.Round(t.Position, 2)) && !positions.Contains(Math.Round(t.Position, 2)))
            .Concat(set)
            .OrderBy(t => t.Position)
            .ToList();
    }
}

/// <summary>Inheritance of optional properties: the upper layer wins when it is set.</summary>
internal static class WordInherit
{
    public static T? Pick<T>(T? top, T? below)
        where T : struct => top ?? below;

    public static T? Pick<T>(T? top, T? below)
        where T : class => top ?? below;
}
