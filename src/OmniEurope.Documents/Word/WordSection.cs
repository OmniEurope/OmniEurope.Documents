// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word;

/// <summary>How a section starts.</summary>
public enum WordSectionStart
{
    /// <summary>On a new page.</summary>
    NextPage,

    /// <summary>On the same page.</summary>
    Continuous,

    /// <summary>On the next even page.</summary>
    EvenPage,

    /// <summary>On the next odd page.</summary>
    OddPage,

    /// <summary>In the next column.</summary>
    NextColumn,
}

/// <summary>Which pages a header or footer applies to.</summary>
public enum WordHeaderFooterKind
{
    /// <summary>All pages not covered by the others (odd pages when even pages have their own).</summary>
    Default,

    /// <summary>The first page of the section, when <see cref="WordPageSetup.TitlePage"/> is set.</summary>
    First,

    /// <summary>Even pages, when <see cref="WordSettings.EvenAndOddHeaders"/> is set.</summary>
    Even,
}

/// <summary>Page size, margins and columns of a section. Distances are in points.</summary>
public sealed record WordPageSetup
{
    /// <summary>A4 portrait with 2.5 cm margins.</summary>
    public static WordPageSetup A4 { get; } = new();

    /// <summary>US Letter portrait with 1 inch margins.</summary>
    public static WordPageSetup Letter { get; } = new() { Width = 612, Height = 792, MarginTop = 72, MarginBottom = 72, MarginLeft = 72, MarginRight = 72 };

    /// <summary>Page width.</summary>
    public double Width { get; init; } = 595.3;

    /// <summary>Page height.</summary>
    public double Height { get; init; } = 841.9;

    /// <summary>Landscape orientation flag (the width and height are already swapped).</summary>
    public bool Landscape { get; init; }

    /// <summary>Top margin.</summary>
    public double MarginTop { get; init; } = 70.9;

    /// <summary>Bottom margin.</summary>
    public double MarginBottom { get; init; } = 70.9;

    /// <summary>Left margin.</summary>
    public double MarginLeft { get; init; } = 70.9;

    /// <summary>Right margin.</summary>
    public double MarginRight { get; init; } = 70.9;

    /// <summary>Distance from the page top to the header.</summary>
    public double HeaderDistance { get; init; } = 35.45;

    /// <summary>Distance from the page bottom to the footer.</summary>
    public double FooterDistance { get; init; } = 35.45;

    /// <summary>Extra binding margin.</summary>
    public double Gutter { get; init; }

    /// <summary>Number of text columns.</summary>
    public int Columns { get; init; } = 1;

    /// <summary>Space between columns.</summary>
    public double ColumnSpacing { get; init; } = 36;

    /// <summary>A line is drawn between columns.</summary>
    public bool ColumnSeparator { get; init; }

    /// <summary>Widths of unequal columns (null when all columns are equal).</summary>
    public IReadOnlyList<double>? ColumnWidths { get; init; }

    /// <summary>Space after each unequal column (<c>w:col/@w:space</c>), in the order of <see cref="ColumnWidths"/>;
    /// null when every column is followed by <see cref="ColumnSpacing"/>.</summary>
    public IReadOnlyList<double>? ColumnSpacings { get; init; }

    /// <summary>The first page has its own header and footer.</summary>
    public bool TitlePage { get; init; }

    /// <summary>How the section starts.</summary>
    public WordSectionStart Start { get; init; }

    /// <summary>Page number of the section's first page (null continues from the previous section).</summary>
    public int? PageNumberStart { get; init; }

    /// <summary>Page number style.</summary>
    public WordNumberFormat PageNumberFormat { get; init; } = WordNumberFormat.Decimal;

    /// <summary>Footnote numbering of the section (<c>w:sectPr/w:footnotePr</c>); null, or a member left null,
    /// follows <see cref="WordSettings"/>.</summary>
    public WordNoteNumbering? FootnoteNumbering { get; init; }

    /// <summary>The same setup turned to landscape (width and height swapped).</summary>
    public WordPageSetup ToLandscape() => Landscape ? this : this with { Landscape = true, Width = Height, Height = Width };

    /// <summary>Width available for text between the margins.</summary>
    public double ContentWidth => Width - MarginLeft - MarginRight - Gutter;
}

/// <summary>A header or footer: blocks shown on the pages of a section.</summary>
public sealed class WordHeaderFooter
{
    /// <summary>An empty header or footer.</summary>
    public WordHeaderFooter()
    {
    }

    /// <summary>A header or footer with the given blocks.</summary>
    public WordHeaderFooter(params IEnumerable<WordBlock> blocks)
    {
        Blocks.AddRange(blocks);
    }

    /// <summary>Content.</summary>
    public List<WordBlock> Blocks { get; } = [];

    /// <summary>The part it was read from, if any.</summary>
    public string? PartName { get; init; }

    /// <summary>The text of its blocks.</summary>
    public string Text => WordBlock.TextOf(Blocks);
}

/// <summary>A section: page setup, headers and footers, and its blocks.</summary>
public sealed class WordSection
{
    /// <summary>An empty A4 section.</summary>
    public WordSection()
    {
    }

    /// <summary>An empty section with the given page setup.</summary>
    public WordSection(WordPageSetup page)
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
    }

    /// <summary>Page setup.</summary>
    public WordPageSetup Page { get; set; } = WordPageSetup.A4;

    /// <summary>Headers by kind. When read, a kind the section does not define is inherited from the
    /// previous section, as Word displays it.</summary>
    public Dictionary<WordHeaderFooterKind, WordHeaderFooter> Headers { get; } = [];

    /// <summary>Footers by kind, inherited like <see cref="Headers"/>.</summary>
    public Dictionary<WordHeaderFooterKind, WordHeaderFooter> Footers { get; } = [];

    /// <summary>Content.</summary>
    public List<WordBlock> Blocks { get; } = [];
}

/// <summary>A footnote or endnote.</summary>
public sealed class WordNote(int id)
{
    /// <summary>Its id, used by <see cref="WordNoteReference"/>.</summary>
    public int Id { get; } = id;

    /// <summary>Content.</summary>
    public List<WordBlock> Blocks { get; } = [];

    /// <summary>The text of its blocks.</summary>
    public string Text => WordBlock.TextOf(Blocks);
}

/// <summary>A comment anchored in the text by a <see cref="WordCommentReference"/>.</summary>
public sealed class WordComment(int id)
{
    /// <summary>Its id.</summary>
    public int Id { get; } = id;

    /// <summary>Author name.</summary>
    public string? Author { get; set; }

    /// <summary>Author initials.</summary>
    public string? Initials { get; set; }

    /// <summary>Date written.</summary>
    public DateTimeOffset? Date { get; set; }

    /// <summary>Content.</summary>
    public List<WordBlock> Blocks { get; } = [];

    /// <summary>The text of its blocks.</summary>
    public string Text => WordBlock.TextOf(Blocks);
}

/// <summary>When note numbering restarts.</summary>
public enum WordNoteRestart
{
    /// <summary>Never.</summary>
    Continuous,

    /// <summary>At each section.</summary>
    EachSection,

    /// <summary>At each page.</summary>
    EachPage,
}

/// <summary>Note numbering set on a section; each member that is set replaces the document setting for the
/// notes of that section.</summary>
public sealed record WordNoteNumbering
{
    /// <summary>Number style.</summary>
    public WordNumberFormat? Format { get; init; }

    /// <summary>The number of the first note, and the number numbering restarts at.</summary>
    public int? Start { get; init; }

    /// <summary>When numbering restarts.</summary>
    public WordNoteRestart? Restart { get; init; }
}

/// <summary>Document-wide settings. Distances are in points.</summary>
public sealed record WordSettings
{
    /// <summary>Interval of the default tab stops.</summary>
    public double DefaultTabStop { get; init; } = 35.4;

    /// <summary>Even pages have their own headers and footers.</summary>
    public bool EvenAndOddHeaders { get; init; }

    /// <summary>Footnote number style.</summary>
    public WordNumberFormat FootnoteFormat { get; init; } = WordNumberFormat.Decimal;

    /// <summary>First footnote number.</summary>
    public int FootnoteStart { get; init; } = 1;

    /// <summary>When footnote numbering restarts.</summary>
    public WordNoteRestart FootnoteRestart { get; init; }

    /// <summary>Endnote number style.</summary>
    public WordNumberFormat EndnoteFormat { get; init; } = WordNumberFormat.LowerRoman;

    /// <summary>First endnote number.</summary>
    public int EndnoteStart { get; init; } = 1;

    /// <summary>Ask the editing application to update fields on open.</summary>
    public bool UpdateFieldsOnOpen { get; init; }
}

/// <summary>Core document properties.</summary>
public sealed record WordInformation
{
    /// <summary>Title.</summary>
    public string? Title { get; init; }

    /// <summary>Subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Author.</summary>
    public string? Author { get; init; }

    /// <summary>Keywords.</summary>
    public string? Keywords { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Category.</summary>
    public string? Category { get; init; }

    /// <summary>Who saved it last.</summary>
    public string? LastModifiedBy { get; init; }

    /// <summary>Creation date.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Last modification date.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>True when nothing is set.</summary>
    public bool IsEmpty => this == new WordInformation();
}
