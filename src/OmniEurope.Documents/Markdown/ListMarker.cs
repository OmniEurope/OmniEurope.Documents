// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Markdown;

/// <summary>List item markers (<c>-</c>, <c>+</c>, <c>*</c>, <c>1.</c>, <c>1)</c>) and list tightness.</summary>
internal static class ListMarker
{
    internal readonly record struct Marker(bool Ordered, int Start, char Delimiter, int MarkerOffset, int Padding);

    public static bool TryParse(BlockParser parser, MarkdownBlock container, out Marker marker)
    {
        marker = default;
        var rest = parser.Line.AsSpan(parser.NextNonspace);
        var inParagraph = container is MarkdownParagraph;
        if (parser.Indent >= 4
            || !TryReadMarker(rest, inParagraph, out var ordered, out var start, out var delimiter, out var length)
            || !FollowedBySpace(rest, length, inParagraph))
        {
            return false;
        }

        var markerOffset = parser.Indent;
        parser.AdvanceNextNonspace();
        parser.AdvanceOffset(length, columns: true);
        marker = new Marker(ordered, start, delimiter, markerOffset, Padding(parser, length));
        return true;
    }

    // The marker is followed by a space, a tab or the end of the line; an empty item cannot interrupt a paragraph.
    private static bool FollowedBySpace(ReadOnlySpan<char> rest, int length, bool inParagraph) =>
        (length >= rest.Length || rest[length] is ' ' or '\t') && !(inParagraph && rest[length..].Trim(" \t").IsEmpty);

    // The item content starts 1 to 4 columns after the marker; 5 or more (indented code) or none counts as one.
    private static int Padding(BlockParser parser, int length)
    {
        var spacesStartColumn = parser.Column;
        var spacesStartOffset = parser.Offset;
        do
        {
            parser.AdvanceOffset(1, columns: true);
        }
        while (parser.Column - spacesStartColumn < 5 && parser.Peek(parser.Offset) is ' ' or '\t');

        var spacesAfterMarker = parser.Column - spacesStartColumn;
        if (spacesAfterMarker is >= 1 and < 5 && parser.Offset < parser.Line.Length)
        {
            return length + spacesAfterMarker;
        }

        ResetTo(parser, spacesStartOffset, spacesStartColumn);
        if (parser.Peek(parser.Offset) is ' ' or '\t')
        {
            parser.AdvanceOffset(1, columns: true);
        }

        return length + 1;
    }
    public static bool Matches(MarkdownList list, Marker marker) =>
        list.IsOrdered == marker.Ordered && list.Marker == marker.Delimiter;

    // An ordered marker: 1 to 9 digits then '.' or ')'; returns the digit count, or 0.
    private static int OrderedDigits(ReadOnlySpan<char> rest)
    {
        var digits = rest.IndexOfAnyExceptInRange('0', '9');
        return digits is >= 1 and <= 9 && rest[digits] is '.' or ')' ? digits : 0;
    }
    private static bool TryReadMarker(ReadOnlySpan<char> rest, bool interruptsParagraph, out bool ordered, out int start, out char delimiter, out int length)
    {
        ordered = false;
        start = 1;
        delimiter = '\0';
        length = 0;
        if (rest.IsEmpty)
        {
            return false;
        }

        if (rest[0] is '-' or '+' or '*')
        {
            delimiter = rest[0];
            length = 1;
            return true;
        }

        var digits = OrderedDigits(rest);
        if (digits == 0)
        {
            return false;
        }

        start = int.Parse(rest[..digits], provider: System.Globalization.CultureInfo.InvariantCulture);
        if (interruptsParagraph && start != 1)
        {
            return false;
        }

        ordered = true;
        delimiter = rest[digits];
        length = digits + 1;
        return true;
    }

    // Rewinds within the current line; only used to undo the look-ahead over spaces after a marker.
    private static void ResetTo(BlockParser parser, int offset, int column)
    {
        parser.Rewind(offset, column);
    }
}

/// <summary>Decides whether a finished list is tight (no blank line between its items or their blocks).</summary>
internal static class ListTightness
{
    public static bool IsTight(MarkdownList list)
    {
        var items = list.ChildList;
        for (var i = 0; i < items.Count; i++)
        {
            var item = (MarkdownContainerBlock)items[i];
            var lastItem = i == items.Count - 1;
            if (EndsWithBlankLine(item) && !lastItem)
            {
                return false;
            }

            var children = item.ChildList;
            for (var k = 0; k < children.Count; k++)
            {
                if (EndsWithBlankLine(children[k]) && (!lastItem || k < children.Count - 1))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool EndsWithBlankLine(MarkdownBlock? block)
    {
        while (block is not null)
        {
            if (block.LastLineBlank)
            {
                return true;
            }

            block = block is MarkdownList or MarkdownListItem && block is MarkdownContainerBlock { ChildList.Count: > 0 } container
                ? container.ChildList[^1]
                : null;
        }

        return false;
    }
}
