// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Markdown;

/// <summary>
/// The working state of inline parsing: a doubly linked list of nodes, the delimiter stack (runs of
/// <c>*</c>, <c>_</c>, <c>~</c>) and the bracket stack (<c>[</c>, <c>![</c>). Emphasis is resolved here
/// with the CommonMark delimiter algorithm, including the rule of three.
/// </summary>
internal sealed class InlineList
{
    internal sealed class Item(MarkdownInline inline)
    {
        public MarkdownInline Inline { get; } = inline;

        public Item? Previous { get; set; }

        public Item? Next { get; set; }
    }

    internal sealed class Delimiter(Item item, char character, int count, bool canOpen, bool canClose)
    {
        public Item Item { get; } = item;

        public char Character { get; } = character;

        public int Count { get; set; } = count;

        public int OriginalCount { get; } = count;

        public bool CanOpen { get; } = canOpen;

        public bool CanClose { get; } = canClose;

        public Delimiter? Previous { get; set; }

        public Delimiter? Next { get; set; }
    }

    internal sealed class Bracket(Item item, int index, bool image, Delimiter? previousDelimiter, Bracket? previous)
    {
        public Item Item { get; } = item;

        public int Index { get; } = index;

        public bool Image { get; } = image;

        public Delimiter? PreviousDelimiter { get; } = previousDelimiter;

        public Bracket? Previous { get; } = previous;

        public bool Active { get; set; } = true;
    }

    private Item? _head;

    public Item? Last { get; private set; }

    public Delimiter? TopDelimiter { get; private set; }

    public Bracket? TopBracket { get; private set; }

    public Item Append(MarkdownInline inline)
    {
        var item = new Item(inline) { Previous = Last };
        if (Last is null)
        {
            _head = item;
        }
        else
        {
            Last.Next = item;
        }

        Last = item;
        return item;
    }

    public void PushDelimiter(Item item, char character, int count, bool canOpen, bool canClose)
    {
        var delimiter = new Delimiter(item, character, count, canOpen, canClose) { Previous = TopDelimiter };
        if (TopDelimiter is not null)
        {
            TopDelimiter.Next = delimiter;
        }

        TopDelimiter = delimiter;
    }

    public void PushBracket(Item item, int index, bool image) =>
        TopBracket = new Bracket(item, index, image, TopDelimiter, TopBracket);

    public void PopBracket() => TopBracket = TopBracket!.Previous;

    /// <summary>Moves every node after <paramref name="opener"/> into <paramref name="container"/>, which
    /// replaces the opener node.</summary>
    public void Wrap(Item opener, MarkdownContainerInline container)
    {
        for (var item = opener.Next; item is not null; item = item.Next)
        {
            container.ChildList.Add(item.Inline);
        }

        var replacement = new Item(container) { Previous = opener.Previous };
        if (opener.Previous is null)
        {
            _head = replacement;
        }
        else
        {
            opener.Previous.Next = replacement;
        }

        Last = replacement;
    }

    public List<MarkdownInline> ToList()
    {
        var result = new List<MarkdownInline>();
        for (var item = _head; item is not null; item = item.Next)
        {
            result.Add(item.Inline);
        }

        return result;
    }

    public void ProcessEmphasis(Delimiter? stackBottom)
    {
        var openersBottom = new Dictionary<(char, bool, int), Delimiter?>();
        var closer = TopDelimiter;
        while (closer is not null && closer.Previous != stackBottom)
        {
            closer = closer.Previous;
        }

        while (closer is not null)
        {
            if (!closer.CanClose)
            {
                closer = closer.Next;
                continue;
            }

            var key = (closer.Character, closer.CanOpen, closer.OriginalCount % 3);
            var bottom = openersBottom.GetValueOrDefault(key, stackBottom);
            var opener = FindOpener(closer, stackBottom, bottom);
            var oldCloser = closer;
            var matched = opener is not null && (closer.Character != '~' || opener.Count == closer.Count);
            if (matched)
            {
                closer = ApplyMatch(opener!, closer);
                continue;
            }

            openersBottom[key] = oldCloser.Previous;
            closer = oldCloser.Next;
            if (!oldCloser.CanOpen)
            {
                RemoveDelimiter(oldCloser);
            }
        }

        while (TopDelimiter is not null && TopDelimiter != stackBottom)
        {
            RemoveDelimiter(TopDelimiter);
        }
    }

    private static Delimiter? FindOpener(Delimiter closer, Delimiter? stackBottom, Delimiter? bottom)
    {
        for (var opener = closer.Previous; opener is not null && opener != stackBottom && opener != bottom; opener = opener.Previous)
        {
            var oddMatch = (closer.CanOpen || opener.CanClose) && closer.OriginalCount % 3 != 0
                && (opener.OriginalCount + closer.OriginalCount) % 3 == 0;
            if (opener.Character == closer.Character && opener.CanOpen && !oddMatch)
            {
                return opener;
            }
        }

        return null;
    }

    // Wraps the nodes between opener and closer; returns the closer to examine next.
    private Delimiter? ApplyMatch(Delimiter opener, Delimiter closer)
    {
        var use = closer.Character == '~' ? closer.Count : closer.Count >= 2 && opener.Count >= 2 ? 2 : 1;
        opener.Count -= use;
        closer.Count -= use;
        Shorten(opener.Item, use);
        Shorten(closer.Item, use);

        MarkdownContainerInline container = closer.Character == '~' ? new MarkdownStrikethrough() : new MarkdownEmphasis(use == 2);
        for (var item = opener.Item.Next; item is not null && item != closer.Item; item = item.Next)
        {
            container.ChildList.Add(item.Inline);
        }

        var node = new Item(container) { Previous = opener.Item, Next = closer.Item };
        opener.Item.Next = node;
        closer.Item.Previous = node;

        // Delimiters between the two are inside the new node now.
        opener.Next = closer;
        closer.Previous = opener;

        if (opener.Count == 0)
        {
            RemoveItem(opener.Item);
            RemoveDelimiter(opener);
        }

        if (closer.Count == 0)
        {
            var next = closer.Next;
            RemoveItem(closer.Item);
            RemoveDelimiter(closer);
            return next;
        }

        return closer;
    }

    private static void Shorten(Item item, int count)
    {
        var text = (MarkdownText)item.Inline;
        text.Text = text.Text[..^count];
    }

    private void RemoveItem(Item item)
    {
        if (item.Previous is null)
        {
            _head = item.Next;
        }
        else
        {
            item.Previous.Next = item.Next;
        }

        if (item.Next is null)
        {
            Last = item.Previous;
        }
        else
        {
            item.Next.Previous = item.Previous;
        }
    }

    private void RemoveDelimiter(Delimiter delimiter)
    {
        if (delimiter.Previous is not null)
        {
            delimiter.Previous.Next = delimiter.Next;
        }

        if (delimiter.Next is null)
        {
            TopDelimiter = delimiter.Previous;
        }
        else
        {
            delimiter.Next.Previous = delimiter.Previous;
        }
    }

    /// <summary>Unicode punctuation or symbol, as CommonMark defines flanking.</summary>
    public static bool IsPunctuation(int codePoint)
    {
        if (codePoint < 128)
        {
            return LinkSyntax.IsEscapable((char)codePoint);
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
        return category is >= UnicodeCategory.ConnectorPunctuation and <= UnicodeCategory.OtherSymbol;
    }
}
