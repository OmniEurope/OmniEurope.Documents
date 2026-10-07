// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>
/// First phase of CommonMark parsing: splits the input into lines and builds the block tree. Each line
/// first walks down the open blocks it continues, then may start new blocks, then adds its text to the
/// deepest open block (or lazily to an open paragraph). Inline content is parsed afterwards.
/// </summary>
internal sealed class BlockParser
{
    internal enum Continuation
    {
        Matched,
        NotMatched,
        LineConsumed,
    }

    private readonly MarkdownDocument _document = new() { Line = 1 };
    private MarkdownBlock _oldTip = null!;
    private MarkdownBlock _lastMatchedContainer = null!;

    public BlockParser(MarkdownOptions options)
    {
        Options = options;
        Tip = _document;
    }

    public MarkdownOptions Options { get; }

    public Dictionary<string, LinkReference> References { get; } = new(StringComparer.Ordinal);

    // Current line state.
    public string Line { get; private set; } = string.Empty;

    public int LineNumber { get; private set; }

    public int Offset { get; private set; }

    public int Column { get; private set; }

    public int NextNonspace { get; private set; }

    public int NextNonspaceColumn { get; private set; }

    public int Indent { get; private set; }

    public bool Indented => Indent >= 4;

    public bool Blank { get; private set; }

    public bool PartiallyConsumedTab { get; private set; }

    public bool AllClosed { get; private set; }

    public MarkdownBlock Tip { get; set; }

    public MarkdownDocument Parse(string text)
    {
        foreach (var line in SplitLines(text))
        {
            IncorporateLine(line);
        }

        while (Tip != _document)
        {
            Finalize(Tip);
        }

        Finalize(_document);
        return _document;
    }

    public char Peek(int position) => position < Line.Length ? Line[position] : '\0';

    public void AdvanceOffset(int count, bool columns)
    {
        while (count > 0 && Offset < Line.Length)
        {
            if (Line[Offset] == '\t')
            {
                var charsToTab = 4 - (Column % 4);
                if (columns)
                {
                    PartiallyConsumedTab = charsToTab > count;
                    var advance = Math.Min(count, charsToTab);
                    Column += advance;
                    Offset += PartiallyConsumedTab ? 0 : 1;
                    count -= advance;
                }
                else
                {
                    PartiallyConsumedTab = false;
                    Column += charsToTab;
                    Offset++;
                    count--;
                }
            }
            else
            {
                PartiallyConsumedTab = false;
                Offset++;
                Column++;
                count--;
            }
        }
    }

    public void Rewind(int offset, int column)
    {
        Offset = offset;
        Column = column;
        PartiallyConsumedTab = false;
    }

    public void AdvanceNextNonspace()
    {
        Offset = NextNonspace;
        Column = NextNonspaceColumn;
        PartiallyConsumedTab = false;
    }

    public void FindNextNonspace()
    {
        var i = Offset;
        var columns = Column;
        while (i < Line.Length)
        {
            var c = Line[i];
            if (c == ' ')
            {
                i++;
                columns++;
            }
            else if (c == '\t')
            {
                i++;
                columns += 4 - (columns % 4);
            }
            else
            {
                break;
            }
        }

        Blank = i >= Line.Length;
        NextNonspace = i;
        NextNonspaceColumn = columns;
        Indent = columns - Column;
    }

    public void CloseUnmatchedBlocks()
    {
        if (AllClosed)
        {
            return;
        }

        while (_oldTip != _lastMatchedContainer)
        {
            var parent = _oldTip.Parent!;
            Finalize(_oldTip);
            _oldTip = parent;
        }

        AllClosed = true;
    }

    public T AddChild<T>(T block)
        where T : MarkdownBlock
    {
        while (!CanContain(Tip, block))
        {
            Finalize(Tip);
        }

        var parent = (MarkdownContainerBlock)Tip;
        block.Parent = parent;
        block.Line = LineNumber;
        parent.ChildList.Add(block);
        Tip = block;
        return block;
    }

    public void Finalize(MarkdownBlock block)
    {
        var above = block.Parent;
        block.IsOpen = false;
        switch (block)
        {
            case MarkdownParagraph paragraph:
                FinalizeParagraph(paragraph);
                break;
            case MarkdownCodeBlock code:
                FinalizeCode(code);
                break;
            case MarkdownHtmlBlock html:
                html.Html = TrimTrailingBlankLines(html.Content!.ToString());
                html.Content = null;
                break;
            case MarkdownList list:
                list.IsTight = ListTightness.IsTight(list);
                break;
        }

        Tip = above!;
    }

    /// <summary>Removes the link reference definitions at the start of the paragraph; true when text remains.</summary>
    public bool ExtractReferences(MarkdownParagraph paragraph)
    {
        var content = paragraph.Content!.ToString();
        var position = 0;
        while (position < content.Length && content[position] == '[')
        {
            if (!LinkSyntax.TryParseReferenceDefinition(content, position, out var end, out var label, out var reference))
            {
                break;
            }

            References.TryAdd(label, reference);
            position = end;
        }

        if (position > 0)
        {
            paragraph.Content = new StringBuilder(content[position..]);
        }

        return !string.IsNullOrWhiteSpace(paragraph.Content!.ToString());
    }

    private void IncorporateLine(string line)
    {
        Line = line;
        Offset = 0;
        Column = 0;
        Blank = false;
        PartiallyConsumedTab = false;
        LineNumber++;
        _oldTip = Tip;

        MarkdownBlock container = _document;
        while (LastOpenChild(container) is { } child)
        {
            container = child;
            FindNextNonspace();
            var result = Continue(container);
            if (result == Continuation.LineConsumed)
            {
                return;
            }

            if (result == Continuation.NotMatched)
            {
                container = container.Parent!;
                break;
            }
        }

        AllClosed = container == _oldTip;
        _lastMatchedContainer = container;
        container = OpenNewBlocks(container);
        AddLineText(container);
    }

    private MarkdownBlock OpenNewBlocks(MarkdownBlock container)
    {
        var matchedLeaf = container is not (MarkdownParagraph or MarkdownTable) && AcceptsLines(container);
        while (!matchedLeaf)
        {
            FindNextNonspace();
            if (!Indented && !BlockStarts.MaybeSpecial(Peek(NextNonspace)))
            {
                AdvanceNextNonspace();
                break;
            }

            var started = BlockStarts.TryStart(this, container);
            if (started == BlockStarts.Started.None)
            {
                AdvanceNextNonspace();
                break;
            }

            container = Tip;
            matchedLeaf = started == BlockStarts.Started.Leaf;
        }

        return container;
    }

    // A blank line in a block quote, a fenced code block or a list item's first line does not make the list loose.
    private bool KeepsBlankLinesInside(MarkdownBlock container) =>
        container is MarkdownBlockQuote or MarkdownCodeBlock { IsFenced: true }
        || (container is MarkdownListItem { ChildList.Count: 0 } && container.Line == LineNumber);

    private void AddLineText(MarkdownBlock container)
    {
        if (!AllClosed && !Blank && Tip is MarkdownParagraph)
        {
            // Lazy continuation of a paragraph.
            AddLine();
            return;
        }

        CloseUnmatchedBlocks();
        if (Blank && container is MarkdownContainerBlock { ChildList.Count: > 0 } withChildren)
        {
            withChildren.ChildList[^1].LastLineBlank = true;
        }

        var lastLineBlank = Blank && !KeepsBlankLinesInside(container);
        for (MarkdownBlock? block = container; block is not null; block = block.Parent)
        {
            block.LastLineBlank = lastLineBlank;
        }

        if (AcceptsLines(container))
        {
            AddLineTo(container);
        }
        else if (Offset < Line.Length && !Blank)
        {
            AddChild(new MarkdownParagraph { Content = new StringBuilder() });
            AdvanceNextNonspace();
            AddLine();
        }
    }

    private void AddLineTo(MarkdownBlock container)
    {
        if (container is MarkdownTable table)
        {
            // Nothing is left of the delimiter row that opened the table; any other row has text.
            if (Offset < Line.Length)
            {
                table.RawRows.Add(TableSyntax.SplitRow(Line.AsSpan(Offset)));
            }

            return;
        }

        AddLine();
        if (container is MarkdownHtmlBlock { Kind: >= 1 and <= 5 } html && HtmlBlockSyntax.Ends(html.Kind, Line.AsSpan(Offset)))
        {
            Finalize(container);
        }
    }

    private void AddLine()
    {
        if (PartiallyConsumedTab)
        {
            Offset++;
            var charsToTab = 4 - (Column % 4);
            Tip.Content!.Append(' ', charsToTab);
        }

        Tip.Content!.Append(Line, Offset, Line.Length - Offset).Append('\n');
    }

    private Continuation Continue(MarkdownBlock block) => block switch
    {
        MarkdownBlockQuote => ContinueBlockQuote(),
        MarkdownListItem item => ContinueItem(item),
        MarkdownCodeBlock code => ContinueCode(code),
        MarkdownHtmlBlock html => Blank && html.Kind is 6 or 7 ? Continuation.NotMatched : Continuation.Matched,
        MarkdownParagraph or MarkdownTable => Blank ? Continuation.NotMatched : Continuation.Matched,
        MarkdownList => Continuation.Matched,
        _ => Continuation.NotMatched,
    };

    private Continuation ContinueBlockQuote()
    {
        if (Indented || Peek(NextNonspace) != '>')
        {
            return Continuation.NotMatched;
        }

        AdvanceNextNonspace();
        AdvanceOffset(1, columns: false);
        if (Peek(Offset) is ' ' or '\t')
        {
            AdvanceOffset(1, columns: true);
        }

        return Continuation.Matched;
    }

    private Continuation ContinueItem(MarkdownListItem item)
    {
        if (Blank)
        {
            if (item.ChildList.Count == 0)
            {
                return Continuation.NotMatched;
            }

            AdvanceNextNonspace();
            return Continuation.Matched;
        }

        if (Indent >= item.MarkerOffset + item.Padding)
        {
            AdvanceOffset(item.MarkerOffset + item.Padding, columns: true);
            return Continuation.Matched;
        }

        return Continuation.NotMatched;
    }

    private Continuation ContinueCode(MarkdownCodeBlock code)
    {
        if (code.IsFenced)
        {
            if (!Indented && BlockStarts.IsClosingFence(Line.AsSpan(NextNonspace), code.FenceChar, code.FenceLength))
            {
                Finalize(code);
                return Continuation.LineConsumed;
            }

            for (var i = code.FenceOffset; i > 0 && Peek(Offset) is ' ' or '\t'; i--)
            {
                AdvanceOffset(1, columns: true);
            }

            return Continuation.Matched;
        }

        if (Indent >= 4)
        {
            AdvanceOffset(4, columns: true);
            return Continuation.Matched;
        }

        if (Blank)
        {
            AdvanceNextNonspace();
            return Continuation.Matched;
        }

        return Continuation.NotMatched;
    }

    private void FinalizeParagraph(MarkdownParagraph paragraph)
    {
        if (!ExtractReferences(paragraph))
        {
            paragraph.Parent!.ChildList.Remove(paragraph);
        }
    }

    private static void FinalizeCode(MarkdownCodeBlock code)
    {
        var content = code.Content!.ToString();
        code.Content = null;
        if (code.IsFenced)
        {
            var newline = content.IndexOf('\n');
            code.Info = LinkSyntax.Unescape(content[..newline].Trim());
            code.Code = content[(newline + 1)..];
            return;
        }

        code.Code = TrimTrailingBlankLines(content) + "\n";
    }

    private static string TrimTrailingBlankLines(string content)
    {
        var lines = content.Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }

    private static MarkdownBlock? LastOpenChild(MarkdownBlock block) =>
        block is MarkdownContainerBlock { ChildList.Count: > 0 } container && container.ChildList[^1].IsOpen
            ? container.ChildList[^1]
            : null;

    private static bool AcceptsLines(MarkdownBlock block) =>
        block is MarkdownParagraph or MarkdownCodeBlock or MarkdownHtmlBlock or MarkdownTable;

    private static bool CanContain(MarkdownBlock parent, MarkdownBlock child) => parent switch
    {
        MarkdownDocument or MarkdownBlockQuote or MarkdownListItem => child is not MarkdownListItem,
        MarkdownList => child is MarkdownListItem,
        _ => false,
    };

    private static IEnumerable<string> SplitLines(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is not ('\r' or '\n'))
            {
                continue;
            }

            yield return text[start..i].Replace('\0', '�');
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            start = i + 1;
        }

        if (start < text.Length)
        {
            yield return text[start..].Replace('\0', '�');
        }
    }
}
