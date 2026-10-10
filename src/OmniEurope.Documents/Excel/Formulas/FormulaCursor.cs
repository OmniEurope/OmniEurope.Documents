// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>A position in the text of a formula, with the small reads the parser and the reference reader share.</summary>
internal sealed class FormulaCursor(string text)
{
    public string Text { get; } = text;

    public int Position { get; set; }

    public bool AtEnd => Position >= Text.Length;

    /// <summary>The character at the position, or <c>'\0'</c> at the end.</summary>
    public char Current => AtEnd ? '\0' : Text[Position];

    /// <summary>The character after the position, or <c>'\0'</c>.</summary>
    public char Next => Position + 1 < Text.Length ? Text[Position + 1] : '\0';

    public bool Is(char c) => !AtEnd && Text[Position] == c;

    /// <summary>Moves past <paramref name="c"/> when it is the current character.</summary>
    public bool Skip(char c)
    {
        if (!Is(c))
        {
            return false;
        }

        Position++;
        return true;
    }

    /// <summary>Moves past the characters <paramref name="accept"/> takes, and returns how many.</summary>
    public int SkipWhile(Func<char, bool> accept, int limit = int.MaxValue)
    {
        var count = 0;
        while (count < limit && !AtEnd && accept(Text[Position]))
        {
            Position++;
            count++;
        }

        return count;
    }

    public void SkipSpaces() => SkipWhile(char.IsWhiteSpace);

    /// <summary>The first of <paramref name="operators"/> found after spaces, moved past; or null.</summary>
    public string? Match(string[] operators)
    {
        SkipSpaces();
        foreach (var op in operators)
        {
            if (string.CompareOrdinal(Text, Position, op, 0, op.Length) == 0)
            {
                Position += op.Length;
                return op;
            }
        }

        return null;
    }

    public void Expect(char c)
    {
        SkipSpaces();
        if (!Skip(c))
        {
            throw Error($"'{c}' expected");
        }
    }

    /// <summary>The text from <paramref name="start"/> to the position.</summary>
    public string Since(int start) => Text[start..Position];

    public FormatException Error(string reason) => new($"Formula '{Text}': {reason} at {Position + 1}.");
}
