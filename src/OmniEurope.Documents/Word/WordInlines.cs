// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Word;

/// <summary>Inline content of a paragraph.</summary>
public abstract class WordInline
{
    private protected WordInline()
    {
    }

    /// <summary>Character formatting set on the run itself.</summary>
    public WordRunProperties Properties { get; set; } = WordRunProperties.Empty;

    /// <summary>Set when the content was inserted or deleted with change tracking on.</summary>
    public WordRevision? Revision { get; set; }

    internal abstract void AppendText(StringBuilder builder);

    internal static string TextOf(IEnumerable<WordInline> inlines)
    {
        var builder = new StringBuilder();
        WordTextBuilder.Append(builder, inlines);
        return builder.ToString();
    }
}

/// <summary>A piece of text with one formatting.</summary>
public sealed class WordText : WordInline
{
    /// <summary>Text with optional formatting; tabs and line feeds are not interpreted (use
    /// <see cref="WordParagraph.AddText"/> to split them).</summary>
    public WordText(string text, WordRunProperties? properties = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        Value = text;
        Properties = properties ?? WordRunProperties.Empty;
    }

    /// <summary>The text.</summary>
    public string Value { get; set; }

    internal override void AppendText(StringBuilder builder) => builder.Append(Value);

    /// <summary>Text, tabs and breaks for a string holding <c>\t</c> and <c>\n</c> (<c>\r</c> is dropped).</summary>
    internal static IEnumerable<WordInline> Split(string text, WordRunProperties properties)
    {
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] is not ('\t' or '\n' or '\r'))
            {
                continue;
            }

            if (i > start)
            {
                yield return new WordText(text[start..i], properties);
            }

            if (i < text.Length && text[i] == '\t')
            {
                yield return new WordTab { Properties = properties };
            }
            else if (i < text.Length && text[i] == '\n')
            {
                yield return new WordBreak(WordBreakKind.Line) { Properties = properties };
            }

            start = i + 1;
        }
    }
}

/// <summary>A tab character; with an <see cref="Alignment"/> it is a positional tab measured from the margin.</summary>
public sealed class WordTab : WordInline
{
    /// <summary>For a positional tab: where the following text goes (left, centre or right of the margins).</summary>
    public WordTabAlignment? Alignment { get; set; }

    /// <summary>For a positional tab: the leader filling the gap.</summary>
    public WordTabLeader Leader { get; set; }

    internal override void AppendText(StringBuilder builder) => builder.Append('\t');
}

/// <summary>Kind of a break.</summary>
public enum WordBreakKind
{
    /// <summary>A line break inside the paragraph.</summary>
    Line,

    /// <summary>A page break.</summary>
    Page,

    /// <summary>A column break.</summary>
    Column,
}

/// <summary>A line, page or column break.</summary>
public sealed class WordBreak(WordBreakKind kind) : WordInline
{
    /// <summary>The break kind.</summary>
    public WordBreakKind Kind { get; } = kind;

    internal override void AppendText(StringBuilder builder) => builder.Append('\n');
}

/// <summary>A symbol character taken from a symbol font (<c>w:sym</c>).</summary>
public sealed class WordSymbol(string font, char character) : WordInline
{
    /// <summary>The symbol font.</summary>
    public string Font { get; } = font;

    /// <summary>The character code in that font (often in the private use area U+F000-U+F0FF).</summary>
    public char Character { get; } = character;

    internal override void AppendText(StringBuilder builder) => builder.Append(Character);
}

/// <summary>A field: its instruction (<c>PAGE</c>, <c>NUMPAGES</c>, <c>TOC \o</c>...) and its last computed result.</summary>
public sealed class WordField : WordInline
{
    /// <summary>A field with a plain-text result.</summary>
    public WordField(string instruction, string result = "")
    {
        ArgumentNullException.ThrowIfNull(instruction);
        Instruction = instruction.Trim();
        if (result.Length > 0)
        {
            Result.Add(new WordText(result));
        }
    }

    /// <summary>The instruction, trimmed.</summary>
    public string Instruction { get; set; }

    /// <summary>The field type, the first word of the instruction in upper case.</summary>
    public string Kind
    {
        get
        {
            var end = Instruction.IndexOfAny([' ', '\t']);
            return (end < 0 ? Instruction : Instruction[..end]).ToUpperInvariant();
        }
    }

    /// <summary>The result as last computed by the editing application.</summary>
    public List<WordInline> Result { get; } = [];

    /// <summary>The current page number.</summary>
    public static WordField Page(WordRunProperties? properties = null) => new("PAGE", "1") { Properties = properties ?? WordRunProperties.Empty };

    /// <summary>The number of pages.</summary>
    public static WordField NumPages(WordRunProperties? properties = null) => new("NUMPAGES", "1") { Properties = properties ?? WordRunProperties.Empty };

    internal override void AppendText(StringBuilder builder) => WordTextBuilder.Append(builder, Result);
}

/// <summary>A hyperlink to an external address or to a bookmark of the document.</summary>
public sealed class WordHyperlink : WordInline
{
    /// <summary>A link with its visible text.</summary>
    public WordHyperlink(string? target, string text, string? anchor = null)
    {
        Target = target;
        Anchor = anchor;
        Inlines.Add(new WordText(text, new WordRunProperties { StyleId = "Hyperlink" }));
    }

    private WordHyperlink(string? target, string? anchor, bool empty)
    {
        Target = target;
        Anchor = anchor;
        _ = empty;
    }

    /// <summary>The external address, or null for an internal link.</summary>
    public string? Target { get; set; }

    /// <summary>The bookmark name for an internal link.</summary>
    public string? Anchor { get; set; }

    /// <summary>The linked content.</summary>
    public List<WordInline> Inlines { get; } = [];

    /// <summary>A link without content yet (filled by the reader).</summary>
    internal static WordHyperlink Create(string? target, string? anchor) => new(target, anchor, empty: true);

    internal override void AppendText(StringBuilder builder) => WordTextBuilder.Append(builder, Inlines);
}

/// <summary>Kind of a note.</summary>
public enum WordNoteKind
{
    /// <summary>A footnote.</summary>
    Footnote,

    /// <summary>An endnote.</summary>
    Endnote,
}

/// <summary>The reference mark of a note: in the text (pointing at the note) or at the start of the note itself.</summary>
public sealed class WordNoteReference(WordNoteKind kind, int id, bool isMark = false) : WordInline
{
    /// <summary>Footnote or endnote.</summary>
    public WordNoteKind Kind { get; } = kind;

    /// <summary>The note id in <see cref="WordDocument.Footnotes"/> or <see cref="WordDocument.Endnotes"/>.</summary>
    public int Id { get; set; } = id;

    /// <summary>True for the mark repeated at the start of the note's own text.</summary>
    public bool IsMark { get; } = isMark;

    internal override void AppendText(StringBuilder builder)
    {
    }
}

/// <summary>The anchor of a comment (<see cref="WordDocument.Comments"/>) in the text.</summary>
public sealed class WordCommentReference(int id) : WordInline
{
    /// <summary>The comment id.</summary>
    public int Id { get; set; } = id;

    internal override void AppendText(StringBuilder builder)
    {
    }
}

/// <summary>How text wraps around a floating shape.</summary>
public enum WordWrap
{
    /// <summary>Text runs over or under the shape.</summary>
    None,

    /// <summary>Around its bounding box.</summary>
    Square,

    /// <summary>Close around its outline.</summary>
    Tight,

    /// <summary>Through its outline.</summary>
    Through,

    /// <summary>Above and below only.</summary>
    TopAndBottom,
}

/// <summary>Position of a floating shape: offsets in points from the named reference (<c>column</c>,
/// <c>page</c>, <c>margin</c>, <c>paragraph</c>...).</summary>
public sealed record WordFloatingPosition(
    double HorizontalOffset = 0,
    string HorizontalRelativeTo = "column",
    double VerticalOffset = 0,
    string VerticalRelativeTo = "paragraph",
    WordWrap Wrap = WordWrap.Square,
    bool BehindText = false);

/// <summary>A drawing object placed in the text: a picture or a text box.</summary>
public abstract class WordShape : WordInline
{
    private protected WordShape(double width, double height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>Displayed width in points.</summary>
    public double Width { get; set; }

    /// <summary>Displayed height in points.</summary>
    public double Height { get; set; }

    /// <summary>Alternative text.</summary>
    public string? Description { get; set; }

    /// <summary>Null when the shape sits in the line like a character, else where it floats.</summary>
    public WordFloatingPosition? Floating { get; set; }

    internal override void AppendText(StringBuilder builder)
    {
    }
}

/// <summary>A picture.</summary>
public sealed class WordPicture(WordImage image, double width, double height) : WordShape(width, height)
{
    /// <summary>The image data.</summary>
    public WordImage Image { get; set; } = image ?? throw new ArgumentNullException(nameof(image));
}

/// <summary>A text box; its text is not part of the paragraph text, read <see cref="Blocks"/>.</summary>
public sealed class WordTextBox(double width, double height) : WordShape(width, height)
{
    /// <summary>Content of the box.</summary>
    public List<WordBlock> Blocks { get; } = [];
}

/// <summary>Image data with its media type.</summary>
public sealed class WordImage
{
    /// <summary>Image bytes with an explicit media type.</summary>
    public WordImage(byte[] data, string contentType)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        Data = data;
        ContentType = contentType;
    }

    /// <summary>The encoded image.</summary>
    public byte[] Data { get; }

    /// <summary>The media type (<c>image/png</c>...).</summary>
    public string ContentType { get; }

    /// <summary>The package part it was read from, if any.</summary>
    public string? PartName { get; init; }

    /// <summary>Image bytes whose type is recognised (PNG, JPEG, GIF, BMP, TIFF, EMF, WMF).</summary>
    /// <exception cref="ArgumentException">The format is not recognised.</exception>
    public static WordImage FromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (ImageInfo.TryIdentify(data, out var info))
        {
            return new WordImage(data, info.ContentType);
        }

        // EMF: header record type 1 and the " EMF" signature at offset 40; WMF: the placeable-file key.
        if (IsEnhancedMetafile(data))
        {
            return new WordImage(data, "image/x-emf");
        }

        if (data.AsSpan().StartsWith(WmfKey))
        {
            return new WordImage(data, "image/x-wmf");
        }

        throw new ArgumentException("Unrecognised image format.", nameof(data));
    }

    /// <summary>True for bytes that start like an Enhanced Metafile: header record type 1 and the " EMF" signature.</summary>
    internal static bool IsEnhancedMetafile(byte[] data) =>
        data.Length > 44 && data.AsSpan(0, 4).SequenceEqual(EmfRecord) && data.AsSpan(40, 4).SequenceEqual(EmfSignature);

    private static ReadOnlySpan<byte> EmfRecord => [1, 0, 0, 0];

    private static ReadOnlySpan<byte> EmfSignature => [0x20, 0x45, 0x4D, 0x46];

    private static ReadOnlySpan<byte> WmfKey => [0xD7, 0xCD, 0xC6, 0x9A];

    internal string Extension => Extensions.GetValueOrDefault(ContentType, "bin");

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpeg",
        ["image/gif"] = "gif",
        ["image/bmp"] = "bmp",
        ["image/tiff"] = "tiff",
        ["image/x-emf"] = "emf",
        ["image/emf"] = "emf",
        ["image/x-wmf"] = "wmf",
        ["image/wmf"] = "wmf",
        ["image/svg+xml"] = "svg",
    };
}
