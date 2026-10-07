// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word;

/// <summary>What a style formats.</summary>
public enum WordStyleType
{
    /// <summary>Paragraphs (and the runs in them).</summary>
    Paragraph,

    /// <summary>Runs.</summary>
    Character,

    /// <summary>Tables.</summary>
    Table,

    /// <summary>Numbering definitions.</summary>
    Numbering,
}

/// <summary>A named style. Its properties are its own; <see cref="WordStyleSheet"/> resolves inheritance.</summary>
public sealed record WordStyle(string Id, WordStyleType Type)
{
    /// <summary>Display name (<c>heading 1</c>, <c>Normal</c>...).</summary>
    public string? Name { get; init; }

    /// <summary>The style this one inherits from.</summary>
    public string? BasedOn { get; init; }

    /// <summary>The paragraph style used for the next paragraph after pressing Enter.</summary>
    public string? Next { get; init; }

    /// <summary>The linked paragraph or character style.</summary>
    public string? Link { get; init; }

    /// <summary>The default style of its type.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Shown in the quick style gallery.</summary>
    public bool IsPrimary { get; init; }

    /// <summary>Paragraph formatting.</summary>
    public WordParagraphProperties? ParagraphProperties { get; init; }

    /// <summary>Character formatting.</summary>
    public WordRunProperties? RunProperties { get; init; }

    /// <summary>Table formatting (table styles).</summary>
    public WordTableProperties? TableProperties { get; init; }

    /// <summary>Cell formatting (table styles).</summary>
    public WordTableCellProperties? CellProperties { get; init; }

    /// <summary>Formats of table regions (table styles).</summary>
    public IReadOnlyList<WordTableConditionalFormat> ConditionalFormats { get; init; } = [];
}

/// <summary>
/// The styles of a document and the document defaults, with inheritance resolution. Layers apply in this
/// order: document defaults, table style, paragraph style chain, numbering level, character style chain,
/// direct formatting. On/off properties such as bold are overridden by each layer rather than toggled.
/// </summary>
public sealed class WordStyleSheet
{
    private const int MaxChain = 32;

    /// <summary>Document default paragraph formatting.</summary>
    public WordParagraphProperties DefaultParagraphProperties { get; set; } = WordParagraphProperties.Empty;

    /// <summary>Document default character formatting.</summary>
    public WordRunProperties DefaultRunProperties { get; set; } = WordRunProperties.Empty;

    /// <summary>Theme font for headings (<c>major</c> slots), when the document has a theme.</summary>
    public string? MajorFont { get; set; }

    /// <summary>Theme font for body text (<c>minor</c> slots), when the document has a theme.</summary>
    public string? MinorFont { get; set; }

    /// <summary>Styles by id.</summary>
    public Dictionary<string, WordStyle> Styles { get; } = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces a style.</summary>
    public WordStyle Add(WordStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        Styles[style.Id] = style;
        return style;
    }

    /// <summary>The style with this id, or null.</summary>
    public WordStyle? Get(string? id) => id is not null && Styles.TryGetValue(id, out var style) ? style : null;

    /// <summary>The default style of a type (<c>Normal</c> for paragraphs).</summary>
    public WordStyle? DefaultStyle(WordStyleType type) => Styles.Values.FirstOrDefault(s => s.Type == type && s.IsDefault);

    /// <summary>The effective paragraph formatting of a paragraph with <paramref name="direct"/> formatting.</summary>
    public WordParagraphProperties ResolveParagraph(WordParagraphProperties direct, WordNumbering? numbering = null, string? tableStyleId = null)
    {
        ArgumentNullException.ThrowIfNull(direct);
        var result = DefaultParagraphProperties;
        foreach (var style in Chain(tableStyleId, WordStyleType.Table))
        {
            result = result.Overlay(style.ParagraphProperties);
        }

        var styleId = direct.StyleId ?? DefaultStyle(WordStyleType.Paragraph)?.Id;
        foreach (var style in Chain(styleId, WordStyleType.Paragraph))
        {
            result = result.Overlay(style.ParagraphProperties);
        }

        var numberingId = direct.NumberingId ?? result.NumberingId;
        var level = numbering?.GetLevel(numberingId ?? 0, direct.NumberingLevel ?? result.NumberingLevel ?? 0);
        result = result.Overlay(level?.ParagraphProperties).Overlay(direct);
        return result with
        {
            StyleId = styleId,
            Tabs = result.Tabs?.Where(t => t.Alignment != WordTabAlignment.Clear).ToList(),
        };
    }

    /// <summary>The effective character formatting of a run in a paragraph.</summary>
    public WordRunProperties ResolveRun(WordParagraphProperties paragraph, WordRunProperties direct, string? tableStyleId = null)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(direct);
        var result = DefaultRunProperties;
        foreach (var style in Chain(tableStyleId, WordStyleType.Table))
        {
            result = result.Overlay(style.RunProperties);
        }

        foreach (var style in Chain(paragraph.StyleId ?? DefaultStyle(WordStyleType.Paragraph)?.Id, WordStyleType.Paragraph))
        {
            result = result.Overlay(style.RunProperties);
        }

        foreach (var style in Chain(direct.StyleId ?? DefaultStyle(WordStyleType.Character)?.Id, WordStyleType.Character))
        {
            result = result.Overlay(style.RunProperties);
        }

        return ApplyTheme(result.Overlay(direct with { StyleId = null }));
    }

    /// <summary>The effective table formatting (style chain, then direct).</summary>
    public WordTableProperties ResolveTable(WordTableProperties direct)
    {
        ArgumentNullException.ThrowIfNull(direct);
        var result = WordTableProperties.Empty;
        foreach (var style in Chain(direct.StyleId ?? DefaultStyle(WordStyleType.Table)?.Id, WordStyleType.Table))
        {
            result = result.Overlay(style.TableProperties);
        }

        return result.Overlay(direct);
    }

    /// <summary>The styles from the root ancestor down to <paramref name="id"/> (cycles and other types ignored).</summary>
    public IReadOnlyList<WordStyle> Chain(string? id, WordStyleType type)
    {
        var chain = new List<WordStyle>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var style = Get(id); style is not null && style.Type == type && chain.Count < MaxChain && seen.Add(style.Id); style = Get(style.BasedOn))
        {
            chain.Add(style);
        }

        chain.Reverse();
        return chain;
    }

    private WordRunProperties ApplyTheme(WordRunProperties properties)
    {
        if (properties.FontTheme is not { } theme)
        {
            return properties;
        }

        var font = theme.StartsWith("major", StringComparison.Ordinal) ? MajorFont : MinorFont;
        return font is null ? properties : properties with { Font = font };
    }

    /// <summary>
    /// A style sheet with the usual built-in styles: Normal, Title, Subtitle, Heading1 to Heading6, Header,
    /// Footer, FootnoteText/FootnoteReference, EndnoteText/EndnoteReference, Hyperlink, ListParagraph,
    /// Caption, Quote, TableNormal and TableGrid.
    /// </summary>
    public static WordStyleSheet CreateDefault(string font = "Calibri", double fontSize = 11, string? language = null)
    {
        var sheet = new WordStyleSheet
        {
            DefaultRunProperties = new WordRunProperties { Font = font, FontEastAsia = font, FontComplex = font, FontSize = fontSize, FontSizeComplex = fontSize, Language = language },
            DefaultParagraphProperties = new WordParagraphProperties { SpacingAfter = 8, LineSpacing = 1.08, LineSpacingRule = WordLineSpacingRule.Multiple },
        };
        sheet.Add(new WordStyle("Normal", WordStyleType.Paragraph) { Name = "Normal", IsDefault = true, IsPrimary = true });
        sheet.Add(new WordStyle("DefaultParagraphFont", WordStyleType.Character) { Name = "Default Paragraph Font", IsDefault = true });
        sheet.Add(new WordStyle("TableNormal", WordStyleType.Table)
        {
            Name = "Normal Table",
            IsDefault = true,
            TableProperties = new WordTableProperties { Indent = 0, CellMargins = new WordCellMargins(0, 5.4, 0, 5.4) },
        });
        AddHeadings(sheet);
        AddNotesAndLinks(sheet);
        return sheet;
    }

    private static void AddHeadings(WordStyleSheet sheet)
    {
        sheet.Add(Paragraph("Title", "Title", new WordParagraphProperties { SpacingAfter = 4, ContextualSpacing = true }, new WordRunProperties { FontSize = 28, FontSizeComplex = 28 }));
        sheet.Add(Paragraph("Subtitle", "Subtitle", null, new WordRunProperties { FontSize = 14, Color = "595959" }));
        double[] sizes = [16, 14, 12, 11, 11, 11];
        for (var level = 1; level <= sizes.Length; level++)
        {
            var paragraph = new WordParagraphProperties
            {
                KeepNext = true,
                KeepLines = true,
                SpacingBefore = level == 1 ? 12 : 6,
                SpacingAfter = level == 1 ? 6 : 3,
                OutlineLevel = level - 1,
            };
            var run = new WordRunProperties { Bold = level <= 4, Italic = level >= 5 ? true : null, FontSize = sizes[level - 1], FontSizeComplex = sizes[level - 1], Color = "1F3864" };
            sheet.Add(Paragraph("Heading" + level, "heading " + level, paragraph, run) with { Next = "Normal" });
        }
    }

    private static void AddNotesAndLinks(WordStyleSheet sheet)
    {
        var compact = new WordParagraphProperties { SpacingAfter = 0, LineSpacing = 1, LineSpacingRule = WordLineSpacingRule.Multiple };
        var bands = new WordParagraphProperties { Tabs = [new WordTabStop(226.8, WordTabAlignment.Center), new WordTabStop(453.6, WordTabAlignment.Right)] };
        sheet.Add(Paragraph("Header", "header", compact.Overlay(bands), null) with { IsPrimary = false });
        sheet.Add(Paragraph("Footer", "footer", compact.Overlay(bands), null) with { IsPrimary = false });
        sheet.Add(Paragraph("FootnoteText", "footnote text", compact, new WordRunProperties { FontSize = 10, FontSizeComplex = 10 }) with { IsPrimary = false });
        sheet.Add(Paragraph("EndnoteText", "endnote text", compact, new WordRunProperties { FontSize = 10, FontSizeComplex = 10 }) with { IsPrimary = false });
        var superscript = new WordRunProperties { VerticalPosition = WordVerticalPosition.Superscript };
        sheet.Add(new WordStyle("FootnoteReference", WordStyleType.Character) { Name = "footnote reference", RunProperties = superscript });
        sheet.Add(new WordStyle("EndnoteReference", WordStyleType.Character) { Name = "endnote reference", RunProperties = superscript });
        sheet.Add(new WordStyle("Hyperlink", WordStyleType.Character) { Name = "Hyperlink", RunProperties = new WordRunProperties { Color = "0563C1", Underline = WordUnderline.Single } });
        sheet.Add(Paragraph("ListParagraph", "List Paragraph", new WordParagraphProperties { IndentLeft = 36, ContextualSpacing = true }, null));
        sheet.Add(Paragraph("Caption", "caption", new WordParagraphProperties { SpacingAfter = 10 }, new WordRunProperties { Italic = true, FontSize = 9, FontSizeComplex = 9, Color = "44546A" }));
        sheet.Add(Paragraph("Quote", "Quote", new WordParagraphProperties { IndentLeft = 43.2, IndentRight = 43.2, Alignment = WordAlignment.Center }, new WordRunProperties { Italic = true, Color = "404040" }));
        sheet.Add(new WordStyle("TableGrid", WordStyleType.Table)
        {
            Name = "Table Grid",
            BasedOn = "TableNormal",
            IsPrimary = true,
            ParagraphProperties = compact,
            TableProperties = new WordTableProperties { Borders = WordTableBorders.Grid() },
        });
    }

    private static WordStyle Paragraph(string id, string name, WordParagraphProperties? paragraph, WordRunProperties? run) =>
        new(id, WordStyleType.Paragraph)
        {
            Name = name,
            BasedOn = "Normal",
            Next = "Normal",
            IsPrimary = true,
            ParagraphProperties = paragraph,
            RunProperties = run,
        };
}
