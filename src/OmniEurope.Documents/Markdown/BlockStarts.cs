// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>Recognises the start of each block kind on the current line, in CommonMark precedence order.</summary>
internal static class BlockStarts
{
    internal enum Started
    {
        None,
        Container,
        Leaf,
    }

    public static bool MaybeSpecial(char c) =>
        c is '#' or '`' or '~' or '*' or '+' or '_' or '=' or '<' or '>' or '-' or '|' or ':' || char.IsAsciiDigit(c);

    public static Started TryStart(BlockParser parser, MarkdownBlock container)
    {
        if (StartBlockQuote(parser))
        {
            return Started.Container;
        }

        if (StartAtxHeading(parser) || StartFencedCode(parser) || StartHtmlBlock(parser, container)
            || StartSetextHeadingOrTable(parser, container) || StartThematicBreak(parser))
        {
            return Started.Leaf;
        }

        if (StartListItem(parser, container))
        {
            return Started.Container;
        }

        return StartIndentedCode(parser) ? Started.Leaf : Started.None;
    }

    public static bool IsClosingFence(ReadOnlySpan<char> text, char fenceChar, int fenceLength)
    {
        var run = 0;
        while (run < text.Length && text[run] == fenceChar)
        {
            run++;
        }

        return run >= fenceLength && text[run..].Trim(" \t").IsEmpty;
    }

    private static bool StartBlockQuote(BlockParser parser)
    {
        if (parser.Indented || parser.Peek(parser.NextNonspace) != '>')
        {
            return false;
        }

        parser.AdvanceNextNonspace();
        parser.AdvanceOffset(1, columns: false);
        if (parser.Peek(parser.Offset) is ' ' or '\t')
        {
            parser.AdvanceOffset(1, columns: true);
        }

        parser.CloseUnmatchedBlocks();
        parser.AddChild(new MarkdownBlockQuote());
        return true;
    }

    private static bool StartAtxHeading(BlockParser parser)
    {
        if (parser.Indented)
        {
            return false;
        }

        var rest = parser.Line.AsSpan(parser.NextNonspace);
        var level = 0;
        while (level < rest.Length && rest[level] == '#')
        {
            level++;
        }

        if (level is 0 or > 6 || (level < rest.Length && rest[level] is not (' ' or '\t')))
        {
            return false;
        }

        parser.AdvanceNextNonspace();
        parser.AdvanceOffset(level, columns: false);
        parser.CloseUnmatchedBlocks();
        var heading = parser.AddChild(new MarkdownHeading { Level = level });
        heading.RawText = StripClosingSequence(parser.Line.AsSpan(parser.Offset)).ToString();
        parser.AdvanceOffset(parser.Line.Length - parser.Offset, columns: false);
        return true;
    }

    private static ReadOnlySpan<char> StripClosingSequence(ReadOnlySpan<char> text)
    {
        text = text.Trim(" \t");
        var end = text.Length;
        while (end > 0 && text[end - 1] == '#')
        {
            end--;
        }

        if (end == 0)
        {
            return [];
        }

        return end < text.Length && text[end - 1] is ' ' or '\t' ? text[..end].TrimEnd(" \t") : text;
    }

    private static bool StartFencedCode(BlockParser parser)
    {
        if (parser.Indented)
        {
            return false;
        }

        var rest = parser.Line.AsSpan(parser.NextNonspace);
        var fence = rest.Length > 0 ? rest[0] : '\0';
        if (fence is not ('`' or '~'))
        {
            return false;
        }

        var length = 0;
        while (length < rest.Length && rest[length] == fence)
        {
            length++;
        }

        if (length < 3 || (fence == '`' && rest[length..].Contains('`')))
        {
            return false;
        }

        parser.CloseUnmatchedBlocks();
        parser.AddChild(new MarkdownCodeBlock
        {
            IsFenced = true,
            FenceChar = fence,
            FenceLength = length,
            FenceOffset = parser.Indent,
            Content = new StringBuilder(),
        });
        parser.AdvanceNextNonspace();
        parser.AdvanceOffset(length, columns: false);
        return true;
    }

    private static bool StartHtmlBlock(BlockParser parser, MarkdownBlock container)
    {
        if (!parser.Options.AllowRawHtml || parser.Indented || parser.Peek(parser.NextNonspace) != '<')
        {
            return false;
        }

        var kind = HtmlBlockSyntax.StartKind(parser.Line.AsSpan(parser.NextNonspace));
        var interruptsParagraph = container is MarkdownParagraph || (!parser.AllClosed && !parser.Blank && parser.Tip is MarkdownParagraph);
        if (kind == 0 || (kind == 7 && interruptsParagraph))
        {
            return false;
        }

        parser.CloseUnmatchedBlocks();
        parser.AddChild(new MarkdownHtmlBlock { Kind = kind, Content = new StringBuilder() });
        return true;
    }

    private static bool StartSetextHeadingOrTable(BlockParser parser, MarkdownBlock container)
    {
        if (parser.Indented || container is not MarkdownParagraph paragraph)
        {
            return false;
        }

        var rest = parser.Line.AsSpan(parser.NextNonspace);
        if (parser.Options.Tables && TableSyntax.TryStart(parser, paragraph, rest))
        {
            return true;
        }

        var level = SetextLevel(rest);
        if (level == 0)
        {
            return false;
        }

        parser.CloseUnmatchedBlocks();
        if (!parser.ExtractReferences(paragraph))
        {
            return false;
        }

        var heading = new MarkdownHeading
        {
            Level = level,
            RawText = paragraph.Content!.ToString().Trim(),
            Line = paragraph.Line,
            Parent = paragraph.Parent,
            IsOpen = true,
        };
        var siblings = paragraph.Parent!.ChildList;
        siblings[siblings.IndexOf(paragraph)] = heading;
        parser.Tip = heading;
        parser.AdvanceOffset(parser.Line.Length - parser.Offset, columns: false);
        return true;
    }

    private static int SetextLevel(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[0] is not ('=' or '-'))
        {
            return 0;
        }

        var marker = text[0];
        var end = 0;
        while (end < text.Length && text[end] == marker)
        {
            end++;
        }

        return text[end..].Trim(" \t").IsEmpty ? (marker == '=' ? 1 : 2) : 0;
    }

    private static bool StartThematicBreak(BlockParser parser)
    {
        if (parser.Indented || !IsThematicBreak(parser.Line.AsSpan(parser.NextNonspace)))
        {
            return false;
        }

        parser.CloseUnmatchedBlocks();
        parser.AddChild(new MarkdownThematicBreak());
        parser.AdvanceOffset(parser.Line.Length - parser.Offset, columns: false);
        return true;
    }

    public static bool IsThematicBreak(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[0] is not ('*' or '-' or '_'))
        {
            return false;
        }

        var marker = text[0];
        var count = 0;
        foreach (var c in text)
        {
            if (c == marker)
            {
                count++;
            }
            else if (c is not (' ' or '\t'))
            {
                return false;
            }
        }

        return count >= 3;
    }

    private static bool StartListItem(BlockParser parser, MarkdownBlock container)
    {
        if (parser.Indented && container is not MarkdownList)
        {
            return false;
        }

        if (!ListMarker.TryParse(parser, container, out var marker))
        {
            return false;
        }

        parser.CloseUnmatchedBlocks();
        if (parser.Tip is not MarkdownList list || !ListMarker.Matches(list, marker))
        {
            parser.AddChild(new MarkdownList { IsOrdered = marker.Ordered, Start = marker.Start, Marker = marker.Delimiter });
        }

        parser.AddChild(new MarkdownListItem { MarkerOffset = marker.MarkerOffset, Padding = marker.Padding });
        return true;
    }

    private static bool StartIndentedCode(BlockParser parser)
    {
        if (!parser.Indented || parser.Tip is MarkdownParagraph || parser.Blank)
        {
            return false;
        }

        parser.AdvanceOffset(4, columns: true);
        parser.CloseUnmatchedBlocks();
        parser.AddChild(new MarkdownCodeBlock { Content = new StringBuilder() });
        return true;
    }
}
