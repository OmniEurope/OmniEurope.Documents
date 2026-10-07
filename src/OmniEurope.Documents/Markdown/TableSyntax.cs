// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>
/// GFM pipe tables: a header row (the last line of the paragraph above), a delimiter row such as
/// <c>| :--- | ---: |</c> that sets the column count and alignments, then body rows until a blank line or
/// another block starts.
/// </summary>
internal static class TableSyntax
{
    public static bool TryStart(BlockParser parser, MarkdownParagraph paragraph, ReadOnlySpan<char> delimiterRow)
    {
        if (!delimiterRow.Contains('|') || !TryParseAlignments(delimiterRow, out var alignments))
        {
            return false;
        }

        var content = paragraph.Content!.ToString().TrimEnd('\n');
        var lastBreak = content.LastIndexOf('\n');
        var headerLine = content[(lastBreak + 1)..];
        var header = SplitRow(headerLine);
        if (header.Count != alignments.Count)
        {
            return false;
        }

        parser.CloseUnmatchedBlocks();
        var parent = paragraph.Parent!;
        var remaining = lastBreak < 0 ? string.Empty : content[..(lastBreak + 1)];
        if (string.IsNullOrWhiteSpace(remaining))
        {
            parent.ChildList.Remove(paragraph);
            parser.Tip = parent;
        }
        else
        {
            paragraph.Content = new StringBuilder(remaining);
            parser.Finalize(paragraph);
        }

        var table = parser.AddChild(new MarkdownTable { Alignments = alignments, RawHeader = header });
        table.Line = parser.LineNumber - 1;
        parser.AdvanceOffset(parser.Line.Length - parser.Offset, columns: false);
        return true;
    }

    /// <summary>Splits a row into raw cell texts: outer pipes dropped, <c>\|</c> kept as a literal pipe.</summary>
    public static List<string> SplitRow(ReadOnlySpan<char> line)
    {
        line = line.Trim(" \t");
        if (line.StartsWith('|'))
        {
            line = line[1..];
        }

        if (line.EndsWith('|') && !IsEscaped(line, line.Length - 1))
        {
            line = line[..^1];
        }

        var cells = new List<string>();
        var cell = new StringBuilder();
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\\' && i + 1 < line.Length && line[i + 1] == '|')
            {
                cell.Append('|');
                i++;
            }
            else if (c == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    private static bool TryParseAlignments(ReadOnlySpan<char> row, out List<MarkdownTableAlignment> alignments)
    {
        alignments = [];
        foreach (var cell in SplitRow(row))
        {
            var text = cell.AsSpan();
            var left = text.StartsWith(':');
            var right = text.EndsWith(':') && text.Length > 1;
            var dashes = text[(left ? 1 : 0)..(text.Length - (right ? 1 : 0))];
            if (dashes.IsEmpty || dashes.ContainsAnyExcept('-'))
            {
                return false;
            }

            alignments.Add((left, right) switch
            {
                (true, true) => MarkdownTableAlignment.Center,
                (true, false) => MarkdownTableAlignment.Left,
                (false, true) => MarkdownTableAlignment.Right,
                _ => MarkdownTableAlignment.None,
            });
        }

        return alignments.Count > 0;
    }

    private static bool IsEscaped(ReadOnlySpan<char> text, int index)
    {
        var backslashes = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 == 1;
    }
}
