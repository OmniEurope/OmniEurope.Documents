// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Html;

/// <summary>
/// A parsed CSS selector list. Supported: type and universal selectors, <c>#id</c>, <c>.class</c>,
/// attribute selectors (<c>[a]</c>, <c>=</c>, <c>~=</c>, <c>|=</c>, <c>^=</c>, <c>$=</c>, <c>*=</c>, with an
/// optional <c>i</c> flag), <c>:first-child</c>, <c>:last-child</c>, <c>:only-child</c>, <c>:empty</c>,
/// <c>:nth-child(n)</c>, <c>:not(...)</c>, the four combinators (descendant, <c>&gt;</c>, <c>+</c>,
/// <c>~</c>) and comma-separated groups.
/// </summary>
public sealed class HtmlSelector
{
    private readonly List<List<(char Combinator, Compound Part)>> _alternatives;

    private HtmlSelector(List<List<(char Combinator, Compound Part)>> alternatives) => _alternatives = alternatives;

    /// <summary>Parses <paramref name="selector"/>.</summary>
    /// <exception cref="FormatException">The selector is malformed or uses an unsupported feature.</exception>
    public static HtmlSelector Parse(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var reader = new SelectorReader(selector);
        var alternatives = new List<List<(char, Compound)>>();
        do
        {
            alternatives.Add(reader.ReadComplex());
        }
        while (reader.TryConsume(','));

        reader.ExpectEnd();
        return new HtmlSelector(alternatives);
    }

    /// <summary>True when <paramref name="element"/> matches.</summary>
    public bool Matches(HtmlElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _alternatives.Exists(chain => MatchesChain(element, chain, chain.Count - 1));
    }

    private static bool MatchesChain(HtmlElement element, List<(char Combinator, Compound Part)> chain, int index)
    {
        if (!chain[index].Part.Matches(element))
        {
            return false;
        }

        if (index == 0)
        {
            return true;
        }

        switch (chain[index].Combinator)
        {
            case '>':
                return element.ParentElement is { } parent && MatchesChain(parent, chain, index - 1);
            case '+':
                return PreviousElement(element) is { } previous && MatchesChain(previous, chain, index - 1);
            case '~':
                return PreviousSiblings(element).Any(sibling => MatchesChain(sibling, chain, index - 1));
            default:
                return Ancestors(element).Any(ancestor => MatchesChain(ancestor, chain, index - 1));
        }
    }

    private static IEnumerable<HtmlElement> PreviousSiblings(HtmlElement element)
    {
        for (var sibling = PreviousElement(element); sibling is not null; sibling = PreviousElement(sibling))
        {
            yield return sibling;
        }
    }

    private static IEnumerable<HtmlElement> Ancestors(HtmlElement element)
    {
        for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            yield return ancestor;
        }
    }

    internal static HtmlElement? PreviousElement(HtmlElement element)
    {
        for (var node = element.PreviousSibling; node is not null; node = node.PreviousSibling)
        {
            if (node is HtmlElement sibling)
            {
                return sibling;
            }
        }

        return null;
    }

    internal static HtmlElement? NextElement(HtmlElement element)
    {
        for (var node = element.NextSibling; node is not null; node = node.NextSibling)
        {
            if (node is HtmlElement sibling)
            {
                return sibling;
            }
        }

        return null;
    }

    /// <summary>A compound selector: simple selectors that must all match the same element.</summary>
    internal sealed class Compound
    {
        public List<Func<HtmlElement, bool>> Tests { get; } = [];

        public bool Matches(HtmlElement element) => Tests.TrueForAll(test => test(element));
    }
}

/// <summary>Reads the selector grammar.</summary>
internal sealed class SelectorReader(string text)
{
    private int _position;

    public bool TryConsume(char c)
    {
        SkipSpaces();
        if (_position < text.Length && text[_position] == c)
        {
            _position++;
            SkipSpaces();
            return true;
        }

        return false;
    }

    public void ExpectEnd()
    {
        SkipSpaces();
        if (_position < text.Length)
        {
            throw Error("unexpected character");
        }
    }

    public List<(char Combinator, HtmlSelector.Compound Part)> ReadComplex()
    {
        SkipSpaces();
        var chain = new List<(char, HtmlSelector.Compound)> { (' ', ReadCompound()) };
        while (true)
        {
            var hadSpace = SkipSpaces();
            if (_position >= text.Length || text[_position] is ',' or ')')
            {
                return chain;
            }

            var combinator = text[_position] is '>' or '+' or '~' ? text[_position++] : hadSpace ? ' ' : throw Error("expected a combinator");
            SkipSpaces();
            chain.Add((combinator, ReadCompound()));
        }
    }

    private HtmlSelector.Compound ReadCompound()
    {
        var compound = new HtmlSelector.Compound();
        var universal = Peek() == '*';
        if (universal)
        {
            _position++;
        }
        else if (IsNameStart(Peek()))
        {
            var tag = ReadName().ToLowerInvariant();
            compound.Tests.Add(e => e.TagName == tag);
        }

        while (_position < text.Length)
        {
            var c = text[_position];
            if (c == '#')
            {
                _position++;
                var id = ReadName();
                compound.Tests.Add(e => e.Id == id);
            }
            else if (c == '.')
            {
                _position++;
                var name = ReadName();
                compound.Tests.Add(e => e.ClassList.Contains(name));
            }
            else if (c == '[')
            {
                compound.Tests.Add(ReadAttribute());
            }
            else if (c == ':')
            {
                compound.Tests.Add(ReadPseudo());
            }
            else
            {
                break;
            }
        }

        if (compound.Tests.Count == 0 && !universal)
        {
            throw Error("empty selector");
        }

        return compound;
    }

    private Func<HtmlElement, bool> ReadAttribute()
    {
        _position++;
        SkipSpaces();
        var name = ReadName();
        SkipSpaces();
        if (TryConsume(']'))
        {
            return e => e.HasAttribute(name);
        }

        if (_position >= text.Length)
        {
            throw Error("expected ]");
        }

        var op = text[_position] == '=' ? "=" : text.Substring(_position, Math.Min(2, text.Length - _position));
        if (op is not ("=" or "~=" or "|=" or "^=" or "$=" or "*="))
        {
            throw Error("unknown attribute operator");
        }

        _position += op.Length;
        SkipSpaces();
        var value = ReadValue();
        SkipSpaces();
        var comparison = StringComparison.Ordinal;
        if (Peek() is 'i' or 'I')
        {
            _position++;
            comparison = StringComparison.OrdinalIgnoreCase;
        }

        if (!TryConsume(']'))
        {
            throw Error("expected ]");
        }

        return e => e.GetAttribute(name) is { } actual && AttributeMatches(op, actual, value, comparison);
    }

    private static bool AttributeMatches(string op, string actual, string value, StringComparison comparison) => op switch
    {
        "=" => actual.Equals(value, comparison),
        "~=" => actual.Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries).Any(w => w.Equals(value, comparison)),
        "|=" => actual.Equals(value, comparison) || actual.StartsWith(value + "-", comparison),
        "^=" => value.Length > 0 && actual.StartsWith(value, comparison),
        "$=" => value.Length > 0 && actual.EndsWith(value, comparison),
        _ => value.Length > 0 && actual.Contains(value, comparison),
    };

    private Func<HtmlElement, bool> ReadPseudo()
    {
        _position++;
        var name = ReadName().ToLowerInvariant();
        switch (name)
        {
            case "first-child":
                return e => HtmlSelector.PreviousElement(e) is null;
            case "last-child":
                return e => HtmlSelector.NextElement(e) is null;
            case "only-child":
                return e => HtmlSelector.PreviousElement(e) is null && HtmlSelector.NextElement(e) is null;
            case "empty":
                return e => !e.ChildNodes.Any(n => n is HtmlElement || (n is HtmlText t && t.Data.Length > 0));
            case "nth-child":
                return ReadNthChild();
            case "not":
                return ReadNot();
            default:
                throw Error($"unsupported pseudo-class :{name}");
        }
    }

    private Func<HtmlElement, bool> ReadNthChild()
    {
        if (!TryConsume('('))
        {
            throw Error("expected (");
        }

        var start = _position;
        while (_position < text.Length && text[_position] != ')')
        {
            _position++;
        }

        var argument = text[start.._position].Trim().ToLowerInvariant();
        TryConsume(')');
        var (a, b) = argument switch
        {
            "odd" => (2, 1),
            "even" => (2, 0),
            _ when int.TryParse(argument, System.Globalization.CultureInfo.InvariantCulture, out var n) => (0, n),
            _ => throw Error("only odd, even and a number are supported in :nth-child"),
        };
        return e =>
        {
            var position = 1;
            for (var s = HtmlSelector.PreviousElement(e); s is not null; s = HtmlSelector.PreviousElement(s))
            {
                position++;
            }

            return a == 0 ? position == b : (position - b) % a == 0 && (position - b) / a >= 0;
        };
    }

    private Func<HtmlElement, bool> ReadNot()
    {
        if (!TryConsume('('))
        {
            throw Error("expected (");
        }

        var inner = ReadComplex();
        if (!TryConsume(')'))
        {
            throw Error("expected )");
        }

        if (inner.Count != 1)
        {
            throw Error(":not() accepts a compound selector only");
        }

        var part = inner[0].Part;
        return e => !part.Matches(e);
    }

    private string ReadValue()
    {
        var quote = Peek();
        if (quote is '"' or '\'')
        {
            var close = text.IndexOf(quote, _position + 1);
            if (close < 0)
            {
                throw Error("unterminated string");
            }

            var value = text[(_position + 1)..close];
            _position = close + 1;
            return value;
        }

        return ReadName();
    }

    private string ReadName()
    {
        var start = _position;
        while (_position < text.Length && (IsNameStart(text[_position]) || char.IsAsciiDigit(text[_position]) || text[_position] == '-'))
        {
            _position++;
        }

        if (_position == start)
        {
            throw Error("expected a name");
        }

        return text[start.._position];
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c is '_' or '-' || c > 127;

    private char Peek() => _position < text.Length ? text[_position] : '\0';

    private bool SkipSpaces()
    {
        var start = _position;
        while (_position < text.Length && char.IsWhiteSpace(text[_position]))
        {
            _position++;
        }

        return _position > start;
    }

    private FormatException Error(string message) => new($"Invalid selector '{text}' at {_position}: {message}.");
}
