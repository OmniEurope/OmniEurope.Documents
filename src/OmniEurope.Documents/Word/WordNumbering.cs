// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Word;

/// <summary>Number style of a list level, page numbers or notes.</summary>
public enum WordNumberFormat
{
    /// <summary>1, 2, 3.</summary>
    Decimal,

    /// <summary>01, 02, 03.</summary>
    DecimalZero,

    /// <summary>a, b, c ... aa.</summary>
    LowerLetter,

    /// <summary>A, B, C ... AA.</summary>
    UpperLetter,

    /// <summary>i, ii, iii.</summary>
    LowerRoman,

    /// <summary>I, II, III.</summary>
    UpperRoman,

    /// <summary>1st, 2nd (English ordinal suffixes).</summary>
    Ordinal,

    /// <summary>A bullet: the level text is shown as is.</summary>
    Bullet,

    /// <summary>No number.</summary>
    None,

    /// <summary>Note reference symbols (*, †, ‡, §).</summary>
    Chicago,
}

/// <summary>What follows a list label.</summary>
public enum WordLabelSuffix
{
    /// <summary>A tab to the next stop (or the hanging indent).</summary>
    Tab,

    /// <summary>A space.</summary>
    Space,

    /// <summary>Nothing.</summary>
    Nothing,
}

/// <summary>One level of a numbering definition.</summary>
public sealed record WordNumberingLevel(int Level)
{
    /// <summary>First number.</summary>
    public int Start { get; init; } = 1;

    /// <summary>Number style.</summary>
    public WordNumberFormat Format { get; init; } = WordNumberFormat.Decimal;

    /// <summary>Label template, <c>%1</c> to <c>%9</c> standing for the numbers of levels 1 to 9 (<c>%1.%2.</c>).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>What follows the label.</summary>
    public WordLabelSuffix Suffix { get; init; } = WordLabelSuffix.Tab;

    /// <summary>Alignment of the label in its space.</summary>
    public WordAlignment Alignment { get; init; } = WordAlignment.Left;

    /// <summary>Show all levels in decimal in the label (legal numbering).</summary>
    public bool IsLegal { get; init; }

    /// <summary>Restart after this 1-based level (0 never restarts, null restarts after any higher level).</summary>
    public int? RestartAfter { get; init; }

    /// <summary>The paragraph style tied to this level.</summary>
    public string? StyleId { get; init; }

    /// <summary>Indents of the list paragraph.</summary>
    public WordParagraphProperties? ParagraphProperties { get; init; }

    /// <summary>Formatting of the label.</summary>
    public WordRunProperties? RunProperties { get; init; }
}

/// <summary>An abstract numbering definition: up to nine levels.</summary>
public sealed class WordAbstractNumbering(int id)
{
    /// <summary>Its id.</summary>
    public int Id { get; } = id;

    /// <summary>Levels by number (0 to 8).</summary>
    public SortedDictionary<int, WordNumberingLevel> Levels { get; } = [];
}

/// <summary>A numbering instance used by paragraphs: an abstract definition plus overrides.</summary>
public sealed class WordNumberingInstance(int id, int abstractId)
{
    /// <summary>Its id (the <c>numId</c> of paragraphs).</summary>
    public int Id { get; } = id;

    /// <summary>The abstract definition.</summary>
    public int AbstractId { get; } = abstractId;

    /// <summary>Start values that override the definition, by level.</summary>
    public Dictionary<int, int> StartOverrides { get; } = [];

    /// <summary>Levels that replace the definition's, by level.</summary>
    public Dictionary<int, WordNumberingLevel> LevelOverrides { get; } = [];
}

/// <summary>The numbering definitions of a document.</summary>
public sealed class WordNumbering
{
    /// <summary>Abstract definitions by id.</summary>
    public SortedDictionary<int, WordAbstractNumbering> Abstracts { get; } = [];

    /// <summary>Instances by id.</summary>
    public SortedDictionary<int, WordNumberingInstance> Instances { get; } = [];

    /// <summary>True when nothing is defined.</summary>
    public bool IsEmpty => Instances.Count == 0 && Abstracts.Count == 0;

    /// <summary>The effective level <paramref name="level"/> of instance <paramref name="numberingId"/>, or null.</summary>
    public WordNumberingLevel? GetLevel(int numberingId, int level)
    {
        if (!Instances.TryGetValue(numberingId, out var instance))
        {
            return null;
        }

        if (instance.LevelOverrides.TryGetValue(level, out var replaced))
        {
            return replaced;
        }

        if (!Abstracts.TryGetValue(instance.AbstractId, out var definition) || !definition.Levels.TryGetValue(level, out var found))
        {
            return null;
        }

        return instance.StartOverrides.TryGetValue(level, out var start) ? found with { Start = start } : found;
    }

    /// <summary>Adds a bulleted list (nine levels, alternating bullets, 18 pt indent per level); returns its id.</summary>
    public int AddBulletList()
    {
        string[] bullets = ["•", "◦", "▪"];
        return Add(level => new WordNumberingLevel(level)
        {
            Format = WordNumberFormat.Bullet,
            Text = bullets[level % bullets.Length],
            ParagraphProperties = Indent(level),
        });
    }

    /// <summary>Adds a numbered list (<c>1.</c>, then <c>a.</c>, <c>i.</c> by level); returns its id.</summary>
    public int AddNumberedList(WordNumberFormat format = WordNumberFormat.Decimal)
    {
        WordNumberFormat[] cycle = [format, WordNumberFormat.LowerLetter, WordNumberFormat.LowerRoman];
        return Add(level => new WordNumberingLevel(level)
        {
            Format = cycle[level % cycle.Length],
            Text = "%" + (level + 1).ToString(CultureInfo.InvariantCulture) + ".",
            ParagraphProperties = Indent(level),
        });
    }

    /// <summary>Adds an instance that restarts the numbering of instance <paramref name="numberingId"/>; returns its id.</summary>
    public int Restart(int numberingId, int start = 1)
    {
        var source = Instances[numberingId];
        var instance = new WordNumberingInstance(NextId(Instances.Keys), source.AbstractId);
        instance.StartOverrides[0] = start;
        Instances[instance.Id] = instance;
        return instance.Id;
    }

    private int Add(Func<int, WordNumberingLevel> level)
    {
        var definition = new WordAbstractNumbering(NextId(Abstracts.Keys, from: 0));
        for (var i = 0; i < 9; i++)
        {
            definition.Levels[i] = level(i);
        }

        Abstracts[definition.Id] = definition;
        var instance = new WordNumberingInstance(NextId(Instances.Keys), definition.Id);
        Instances[instance.Id] = instance;
        return instance.Id;
    }

    private static WordParagraphProperties Indent(int level) => new() { IndentLeft = 18 * (level + 1), FirstLineIndent = -18 };

    private static int NextId(IEnumerable<int> used, int from = 1) => Math.Max(from, used.DefaultIfEmpty(from - 1).Max() + 1);

    /// <summary>A number written in a numbering format (<c>4</c> as <c>iv</c>, <c>d</c>, <c>04</c>...).</summary>
    public static string FormatNumber(int value, WordNumberFormat format) => format switch
    {
        WordNumberFormat.DecimalZero => value is >= 0 and < 10 ? "0" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture),
        WordNumberFormat.LowerLetter => Letters(value).ToLowerInvariant(),
        WordNumberFormat.UpperLetter => Letters(value),
        WordNumberFormat.LowerRoman => Roman(value).ToLowerInvariant(),
        WordNumberFormat.UpperRoman => Roman(value),
        WordNumberFormat.Ordinal => value.ToString(CultureInfo.InvariantCulture) + OrdinalSuffix(value),
        WordNumberFormat.Chicago => Chicago(value),
        WordNumberFormat.None or WordNumberFormat.Bullet => string.Empty,
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    // Word repeats the letter: 27 is "aa", 28 "bb".
    private static string Letters(int value)
    {
        if (value <= 0)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var letter = (char)('A' + ((value - 1) % 26));
        return new string(letter, ((value - 1) / 26) + 1);
    }

    private static string Roman(int value)
    {
        if (value is <= 0 or >= 4000)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        (int Value, string Text)[] table = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var builder = new StringBuilder();
        foreach (var (amount, text) in table)
        {
            for (; value >= amount; value -= amount)
            {
                builder.Append(text);
            }
        }

        return builder.ToString();
    }

    private static string OrdinalSuffix(int value) => (value % 100) is 11 or 12 or 13 ? "th" : (value % 10) switch
    {
        1 => "st",
        2 => "nd",
        3 => "rd",
        _ => "th",
    };

    private static string Chicago(int value)
    {
        if (value <= 0)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        string[] symbols = ["*", "†", "‡", "§"];
        return string.Concat(Enumerable.Repeat(symbols[(value - 1) % 4], ((value - 1) / 4) + 1));
    }
}

/// <summary>
/// Computes list labels while paragraphs are read in document order. Counters belong to the abstract
/// definition, so instances sharing one continue each other, unless an instance overrides a start value:
/// its first use then restarts that level.
/// </summary>
public sealed class WordListCounter(WordNumbering numbering)
{
    private readonly WordNumbering _numbering = numbering ?? throw new ArgumentNullException(nameof(numbering));
    private readonly Dictionary<int, int[]> _counters = [];
    private readonly HashSet<int> _started = [];

    /// <summary>
    /// The label of the next paragraph of instance <paramref name="numberingId"/> at <paramref name="level"/>
    /// (for example <c>2.1.</c>), with the level definition; null when the paragraph has no numbering.
    /// </summary>
    public (string Label, WordNumberingLevel Level)? Next(int numberingId, int level)
    {
        level = Math.Clamp(level, 0, 8);
        var definition = _numbering.GetLevel(numberingId, level);
        if (definition is null || !_numbering.Instances.TryGetValue(numberingId, out var instance))
        {
            return null;
        }

        var counters = Counters(instance);
        counters[level] = counters[level] == int.MinValue ? definition.Start : counters[level] + 1;
        for (var deeper = level + 1; deeper < 9; deeper++)
        {
            var below = _numbering.GetLevel(numberingId, deeper);
            if (below?.RestartAfter is not 0 && (below?.RestartAfter is null || level <= below.RestartAfter.Value - 1))
            {
                counters[deeper] = int.MinValue;
            }
        }

        return (Label(numberingId, definition, counters), definition);
    }

    private int[] Counters(WordNumberingInstance instance)
    {
        if (!_counters.TryGetValue(instance.AbstractId, out var counters))
        {
            counters = Enumerable.Repeat(int.MinValue, 9).ToArray();
            _counters[instance.AbstractId] = counters;
        }

        if (instance.StartOverrides.Count > 0 && _started.Add(instance.Id))
        {
            foreach (var level in instance.StartOverrides.Keys)
            {
                counters[level] = int.MinValue;
            }
        }

        return counters;
    }

    private string Label(int numberingId, WordNumberingLevel definition, int[] counters)
    {
        if (definition.Format == WordNumberFormat.Bullet)
        {
            return definition.Text;
        }

        var builder = new StringBuilder();
        var text = definition.Text;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '%' && i + 1 < text.Length && text[i + 1] is >= '1' and <= '9')
            {
                var referenced = text[++i] - '1';
                var level = _numbering.GetLevel(numberingId, referenced);
                var value = counters[referenced] == int.MinValue ? level?.Start ?? 1 : counters[referenced];
                var format = definition.IsLegal ? WordNumberFormat.Decimal : level?.Format ?? WordNumberFormat.Decimal;
                builder.Append(WordNumbering.FormatNumber(value, format));
            }
            else
            {
                builder.Append(text[i]);
            }
        }

        return builder.ToString();
    }
}
