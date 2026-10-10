// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>
/// Builds Markdown text. Every method that takes user text escapes it, so a value such as
/// <c>*not bold*</c> or <c># not a title</c> reads back as the same literal text.
/// </summary>
public sealed class MarkdownWriter
{
    private const char Backslash = '\\';

    private readonly StringBuilder _text = new();

    /// <summary>Escapes <paramref name="text"/> for use inside a paragraph, heading, list item or table cell.
    /// Line breaks become spaces and leading spaces are dropped (Markdown would not keep them).</summary>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.TrimStart(' ', '\t');
        var builder = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n')
            {
                builder.Append(' ');
                continue;
            }

            if (i == 0 && TryEscapeOrderedMarker(text, builder, ref i))
            {
                continue;
            }

            if (NeedsEscape(c) || (i == 0 && c is '#' or '-' or '+' or '='))
            {
                builder.Append(Backslash);
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Escapes text that does not start a line: the characters with a meaning anywhere are escaped and
    /// line breaks become spaces; spaces are kept.</summary>
    internal static string EscapeInline(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            if (NeedsEscape(c))
            {
                builder.Append(Backslash);
            }

            builder.Append(c is '\r' or '\n' ? ' ' : c);
        }

        return builder.ToString();
    }

    // "1. text" would start an ordered list: the delimiter is escaped ("1\. text").
    private static bool TryEscapeOrderedMarker(string text, StringBuilder builder, ref int i)
    {
        var end = i;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        if (end == i || end >= text.Length || text[end] is not ('.' or ')'))
        {
            return false;
        }

        builder.Append(text, i, end - i).Append(Backslash).Append(text[end]);
        i = end;
        return true;
    }

    /// <summary>Appends a heading of level 1 to 6.</summary>
    public MarkdownWriter Heading(int level, string text)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 6);
        Separate();
        _text.Append('#', level).Append(' ').Append(Escape(text)).Append('\n');
        return this;
    }

    /// <summary>Appends a paragraph of plain text.</summary>
    public MarkdownWriter Paragraph(string text)
    {
        Separate();
        _text.Append(Escape(text)).Append('\n');
        return this;
    }

    /// <summary>Appends Markdown that is already formatted (not escaped).</summary>
    public MarkdownWriter Raw(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        Separate();
        _text.Append(markdown.TrimEnd('\n')).Append('\n');
        return this;
    }

    /// <summary>Appends a bullet list (or an ordered one) of plain-text items.</summary>
    public MarkdownWriter List(IEnumerable<string> items, bool ordered = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        Separate();
        var number = 1;
        foreach (var item in items)
        {
            _text.Append(ordered ? $"{number++}. " : "- ").Append(Escape(item)).Append('\n');
        }

        return this;
    }

    /// <summary>Appends a GFM pipe table; rows shorter than the header are padded.</summary>
    public MarkdownWriter Table(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string?>> rows, IReadOnlyList<MarkdownTableAlignment>? alignments = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);
        if (header.Count == 0)
        {
            throw new ArgumentException("A table needs at least one column.", nameof(header));
        }

        Separate();
        AppendRow(header);
        _text.Append('|');
        for (var i = 0; i < header.Count; i++)
        {
            _text.Append(' ').Append((alignments is not null && i < alignments.Count ? alignments[i] : MarkdownTableAlignment.None) switch
            {
                MarkdownTableAlignment.Left => ":---",
                MarkdownTableAlignment.Center => ":---:",
                MarkdownTableAlignment.Right => "---:",
                _ => "---",
            }).Append(" |");
        }

        _text.Append('\n');
        foreach (var row in rows)
        {
            AppendRow(Enumerable.Range(0, header.Count).Select(i => i < row.Count ? row[i] ?? string.Empty : string.Empty).ToList());
        }

        return this;
    }

    /// <summary>The Markdown built so far.</summary>
    public override string ToString() => _text.ToString();

    private void AppendRow(IReadOnlyList<string> cells)
    {
        _text.Append('|');
        foreach (var cell in cells)
        {
            _text.Append(' ').Append(Escape(cell)).Append(" |");
        }

        _text.Append('\n');
    }

    private void Separate()
    {
        if (_text.Length > 0)
        {
            _text.Append('\n');
        }
    }

    private static bool NeedsEscape(char c) => c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '&' or '~' or '|';
}
