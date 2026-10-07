// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Markdown;

/// <summary>A node of the Markdown syntax tree.</summary>
public abstract class MarkdownNode
{
    internal MarkdownNode()
    {
    }
}

/// <summary>A block-level node.</summary>
public abstract class MarkdownBlock : MarkdownNode
{
    internal MarkdownBlock()
    {
    }

    /// <summary>The 1-based source line where the block starts.</summary>
    public int Line { get; internal set; }

    // Parse-time state, meaningless once the document is built.
    internal MarkdownContainerBlock? Parent { get; set; }

    internal bool IsOpen { get; set; } = true;

    internal bool LastLineBlank { get; set; }

    internal System.Text.StringBuilder? Content { get; set; }
}

/// <summary>A block that holds other blocks.</summary>
public abstract class MarkdownContainerBlock : MarkdownBlock
{
    internal readonly List<MarkdownBlock> ChildList = [];

    internal MarkdownContainerBlock()
    {
    }

    /// <summary>The child blocks, in order.</summary>
    public IReadOnlyList<MarkdownBlock> Children => ChildList;
}

/// <summary>The root of a parsed document.</summary>
public sealed class MarkdownDocument : MarkdownContainerBlock
{
    internal MarkdownDocument()
    {
    }
}

/// <summary>A block quote (<c>&gt;</c>).</summary>
public sealed class MarkdownBlockQuote : MarkdownContainerBlock
{
    internal MarkdownBlockQuote()
    {
    }
}

/// <summary>A bullet or ordered list.</summary>
public sealed class MarkdownList : MarkdownContainerBlock
{
    internal MarkdownList()
    {
    }

    /// <summary>True for an ordered list.</summary>
    public bool IsOrdered { get; internal set; }

    /// <summary>The first number of an ordered list.</summary>
    public int Start { get; internal set; } = 1;

    /// <summary>The bullet character (<c>-</c>, <c>+</c>, <c>*</c>) or the ordered delimiter (<c>.</c>, <c>)</c>).</summary>
    public char Marker { get; internal set; }

    /// <summary>True when no blank line separates the items or their blocks.</summary>
    public bool IsTight { get; internal set; } = true;
}

/// <summary>An item of a <see cref="MarkdownList"/>.</summary>
public sealed class MarkdownListItem : MarkdownContainerBlock
{
    internal MarkdownListItem()
    {
    }

    /// <summary>The state of a GFM task item (<c>[ ]</c> or <c>[x]</c>), null for an ordinary item.</summary>
    public bool? TaskChecked { get; internal set; }

    internal int MarkerOffset { get; set; }

    internal int Padding { get; set; }
}

/// <summary>A paragraph.</summary>
public sealed class MarkdownParagraph : MarkdownBlock
{
    internal MarkdownParagraph()
    {
    }

    /// <summary>The inline content.</summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; internal set; } = [];
}

/// <summary>An ATX (<c>#</c>) or setext (underlined) heading.</summary>
public sealed class MarkdownHeading : MarkdownBlock
{
    internal MarkdownHeading()
    {
    }

    /// <summary>The level, 1 to 6.</summary>
    public int Level { get; internal set; }

    /// <summary>The inline content.</summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; internal set; } = [];

    internal string RawText { get; set; } = string.Empty;
}

/// <summary>A thematic break (<c>---</c>).</summary>
public sealed class MarkdownThematicBreak : MarkdownBlock
{
    internal MarkdownThematicBreak()
    {
    }
}

/// <summary>A fenced or indented code block.</summary>
public sealed class MarkdownCodeBlock : MarkdownBlock
{
    internal MarkdownCodeBlock()
    {
    }

    /// <summary>The info string of a fenced block (its first word is usually the language), empty otherwise.</summary>
    public string Info { get; internal set; } = string.Empty;

    /// <summary>The literal content, each line ending with a line feed.</summary>
    public string Code { get; internal set; } = string.Empty;

    /// <summary>True for a fenced block.</summary>
    public bool IsFenced { get; internal set; }

    internal char FenceChar { get; set; }

    internal int FenceLength { get; set; }

    internal int FenceOffset { get; set; }
}

/// <summary>A raw HTML block, kept only when raw HTML is allowed.</summary>
public sealed class MarkdownHtmlBlock : MarkdownBlock
{
    internal MarkdownHtmlBlock()
    {
    }

    /// <summary>The raw HTML.</summary>
    public string Html { get; internal set; } = string.Empty;

    internal int Kind { get; set; }
}

/// <summary>Column alignment of a GFM table.</summary>
public enum MarkdownTableAlignment
{
    /// <summary>No alignment given.</summary>
    None,

    /// <summary>Left aligned.</summary>
    Left,

    /// <summary>Centred.</summary>
    Center,

    /// <summary>Right aligned.</summary>
    Right,
}

/// <summary>A GFM pipe table.</summary>
public sealed class MarkdownTable : MarkdownBlock
{
    internal MarkdownTable()
    {
    }

    /// <summary>The alignment of each column.</summary>
    public IReadOnlyList<MarkdownTableAlignment> Alignments { get; internal set; } = [];

    /// <summary>The header cells.</summary>
    public IReadOnlyList<IReadOnlyList<MarkdownInline>> Header { get; internal set; } = [];

    /// <summary>The body rows, each with exactly one entry per column.</summary>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<MarkdownInline>>> Rows { get; internal set; } = [];

    internal List<string> RawHeader { get; set; } = [];

    internal List<List<string>> RawRows { get; } = [];
}

/// <summary>An inline node.</summary>
public abstract class MarkdownInline : MarkdownNode
{
    internal MarkdownInline()
    {
    }
}

/// <summary>Literal text.</summary>
public sealed class MarkdownText(string text) : MarkdownInline
{
    /// <summary>The text, with escapes and entities already decoded.</summary>
    public string Text { get; internal set; } = text;
}

/// <summary>A code span.</summary>
public sealed class MarkdownCode(string code) : MarkdownInline
{
    /// <summary>The literal code.</summary>
    public string Code { get; } = code;
}

/// <summary>A line break: hard (rendered as a break) or soft (rendered as a line feed).</summary>
public sealed class MarkdownLineBreak(bool hard) : MarkdownInline
{
    /// <summary>True for a hard break (two trailing spaces or a backslash).</summary>
    public bool IsHard { get; } = hard;
}

/// <summary>Raw inline HTML, kept only when raw HTML is allowed.</summary>
public sealed class MarkdownRawHtml(string html) : MarkdownInline
{
    /// <summary>The raw HTML.</summary>
    public string Html { get; } = html;
}

/// <summary>An inline node that holds other inline nodes.</summary>
public abstract class MarkdownContainerInline : MarkdownInline
{
    internal readonly List<MarkdownInline> ChildList = [];

    internal MarkdownContainerInline()
    {
    }

    /// <summary>The children, in order.</summary>
    public IReadOnlyList<MarkdownInline> Children => ChildList;
}

/// <summary>Emphasis (<c>*a*</c>) or strong emphasis (<c>**a**</c>).</summary>
public sealed class MarkdownEmphasis : MarkdownContainerInline
{
    internal MarkdownEmphasis(bool strong)
    {
        IsStrong = strong;
    }

    /// <summary>True for strong emphasis.</summary>
    public bool IsStrong { get; }
}

/// <summary>GFM strikethrough (<c>~~a~~</c>).</summary>
public sealed class MarkdownStrikethrough : MarkdownContainerInline
{
    internal MarkdownStrikethrough()
    {
    }
}

/// <summary>A link, or an image when <see cref="IsImage"/> is true (its children are the alternative text).</summary>
public sealed class MarkdownLink : MarkdownContainerInline
{
    internal MarkdownLink(string url, string title, bool image, bool autolink)
    {
        Url = url;
        Title = title;
        IsImage = image;
        IsAutolink = autolink;
    }

    /// <summary>The destination, as written (escapes and entities decoded).</summary>
    public string Url { get; }

    /// <summary>The title, empty when none.</summary>
    public string Title { get; }

    /// <summary>True for an image.</summary>
    public bool IsImage { get; }

    /// <summary>True for an autolink (<c>&lt;https://...&gt;</c> or a GFM bare URL).</summary>
    public bool IsAutolink { get; }
}
