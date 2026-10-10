// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Text;

/// <summary>
/// One isolating run sequence of UAX #9 resolved: weak types (W1 to W7), paired brackets (N0, with BD16), neutrals
/// (N1, N2) and implicit levels (I1, I2). The sequence's types are worked on in a copy, then its levels are written
/// back to the paragraph.
/// </summary>
internal sealed class BidiSequence
{
    private const int MaxBracketDepth = 63;
    private readonly string _text;
    private readonly BidiClass[] _initial;
    private readonly byte[] _levels;
    private readonly List<int> _indices;
    private readonly BidiClass[] _t;
    private readonly BidiClass _sos;
    private readonly BidiClass _eos;
    private readonly BidiClass _embedding;

    public BidiSequence(string text, BidiClass[] initial, BidiClass[] types, byte[] levels, List<int> indices, BidiClass sos, BidiClass eos)
    {
        _text = text;
        _initial = initial;
        _levels = levels;
        _indices = indices;
        _t = indices.Select(i => types[i]).ToArray();
        _sos = sos;
        _eos = eos;
        _embedding = (levels[indices[0]] & 1) == 0 ? BidiClass.L : BidiClass.R;
    }

    public void Resolve()
    {
        Weak();
        Brackets();
        Neutrals();
        Implicit();
    }

    private static bool IsNeutralOrIsolate(BidiClass type) =>
        type is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;

    // The strong direction a type counts as for N0 and N1: numbers count as R; null for a neutral.
    private static BidiClass? Strong(BidiClass type) => type switch
    {
        BidiClass.L => BidiClass.L,
        BidiClass.R or BidiClass.AL or BidiClass.EN or BidiClass.AN => BidiClass.R,
        _ => null,
    };

    private void Weak()
    {
        Marks();
        Numbers();
        Separators();
        Terminators();
        Neutralised();
    }

    // W1: a non-spacing mark takes the type of what it follows, ON after an isolate initiator or PDI.
    private void Marks()
    {
        for (var k = 0; k < _t.Length; k++)
        {
            if (_t[k] == BidiClass.NSM)
            {
                _t[k] = k == 0 ? _sos : _t[k - 1] is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI ? BidiClass.ON : _t[k - 1];
            }
        }
    }

    // W2: a European number after Arabic letters is an Arabic number; W3: Arabic letters are R.
    private void Numbers()
    {
        for (var k = 0; k < _t.Length; k++)
        {
            if (_t[k] == BidiClass.EN && PreviousStrong(k, withArabic: true) == BidiClass.AL)
            {
                _t[k] = BidiClass.AN;
            }
        }

        for (var k = 0; k < _t.Length; k++)
        {
            _t[k] = _t[k] == BidiClass.AL ? BidiClass.R : _t[k];
        }
    }

    // W6: separators and terminators left are neutral; W7: a European number after left-to-right text is L.
    private void Neutralised()
    {
        for (var k = 0; k < _t.Length; k++)
        {
            _t[k] = _t[k] is BidiClass.ES or BidiClass.ET or BidiClass.CS ? BidiClass.ON : _t[k];
            if (_t[k] == BidiClass.EN && PreviousStrong(k, withArabic: false) == BidiClass.L)
            {
                _t[k] = BidiClass.L;
            }
        }
    }

    // The first strong type before position k (R, L, and AL when asked), or sos.
    private BidiClass PreviousStrong(int k, bool withArabic)
    {
        for (var j = k - 1; j >= 0; j--)
        {
            if (_t[j] is BidiClass.L or BidiClass.R || (withArabic && _t[j] == BidiClass.AL))
            {
                return _t[j];
            }
        }

        return _sos;
    }

    // W4: one separator between two numbers of the same kind joins them.
    private void Separators()
    {
        for (var k = 1; k < _t.Length - 1; k++)
        {
            var (before, after) = (_t[k - 1], _t[k + 1]);
            if (_t[k] == BidiClass.ES && before == BidiClass.EN && after == BidiClass.EN)
            {
                _t[k] = BidiClass.EN;
            }
            else if (_t[k] == BidiClass.CS && before == after && before is BidiClass.EN or BidiClass.AN)
            {
                _t[k] = before;
            }
        }
    }

    // W5: terminators next to a European number become European numbers.
    private void Terminators()
    {
        for (var k = 0; k < _t.Length; k++)
        {
            if (_t[k] != BidiClass.ET)
            {
                continue;
            }

            var end = k;
            while (end + 1 < _t.Length && _t[end + 1] == BidiClass.ET)
            {
                end++;
            }

            if ((k > 0 && _t[k - 1] == BidiClass.EN) || (end + 1 < _t.Length && _t[end + 1] == BidiClass.EN))
            {
                Array.Fill(_t, BidiClass.EN, k, end - k + 1);
            }

            k = end;
        }
    }

    // N0: a bracket pair takes the embedding direction when it holds text of that direction, else the opposite
    // direction when it holds that and the text before the pair has it too.
    private void Brackets()
    {
        foreach (var (open, close) in BracketPairs())
        {
            var inside = Enumerable.Range(open + 1, close - open - 1).Select(k => Strong(_t[k])).Where(s => s is not null).ToList();
            if (inside.Count == 0)
            {
                continue;
            }

            var opposite = _embedding == BidiClass.L ? BidiClass.R : BidiClass.L;
            var direction = inside.Contains(_embedding) ? _embedding : ContextBefore(open) == opposite ? opposite : _embedding;
            SetBracket(open, direction);
            SetBracket(close, direction);
        }
    }

    private BidiClass ContextBefore(int open)
    {
        for (var k = open - 1; k >= 0; k--)
        {
            if (Strong(_t[k]) is { } strong)
            {
                return strong;
            }
        }

        return _sos;
    }

    // The bracket and the non-spacing marks that followed it in the text.
    private void SetBracket(int k, BidiClass direction)
    {
        _t[k] = direction;
        for (var next = k + 1; next < _t.Length && _initial[_indices[next]] == BidiClass.NSM; next++)
        {
            _t[next] = direction;
        }
    }

    // BD16: opening brackets on a stack of 63, each closing bracket matched with the nearest opener of its kind.
    private List<(int Open, int Close)> BracketPairs()
    {
        var pairs = new List<(int, int)>();
        var stack = new List<(int Closing, int Position)>();
        for (var k = 0; k < _t.Length; k++)
        {
            if (_t[k] != BidiClass.ON)
            {
                continue;
            }

            int codePoint = _text[_indices[k]];
            if (BidiBrackets.ClosingOf(codePoint) is { } closing)
            {
                if (stack.Count == MaxBracketDepth)
                {
                    break;
                }

                stack.Add((BidiBrackets.Canonical(closing), k));
            }
            else if (BidiBrackets.IsClosing(codePoint))
            {
                var match = stack.FindLastIndex(s => s.Closing == BidiBrackets.Canonical(codePoint));
                if (match >= 0)
                {
                    pairs.Add((stack[match].Position, k));
                    stack.RemoveRange(match, stack.Count - match);
                }
            }
        }

        pairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return pairs;
    }

    // N1 and N2: neutrals between text of one direction take it, other neutrals the embedding direction.
    private void Neutrals()
    {
        for (var k = 0; k < _t.Length; k++)
        {
            if (!IsNeutralOrIsolate(_t[k]))
            {
                continue;
            }

            var end = k;
            while (end + 1 < _t.Length && IsNeutralOrIsolate(_t[end + 1]))
            {
                end++;
            }

            var before = k == 0 ? _sos : Strong(_t[k - 1]);
            var after = end + 1 >= _t.Length ? _eos : Strong(_t[end + 1]);
            Array.Fill(_t, before is not null && before == after ? before.Value : _embedding, k, end - k + 1);
            k = end;
        }
    }

    // I1 and I2.
    private void Implicit()
    {
        for (var k = 0; k < _t.Length; k++)
        {
            var i = _indices[k];
            var level = _levels[i];
            _levels[i] = (byte)(level + ((level & 1) == 0
                ? _t[k] switch { BidiClass.R => 1, BidiClass.AN or BidiClass.EN => 2, _ => 0 }
                : _t[k] is BidiClass.L or BidiClass.EN or BidiClass.AN ? 1 : 0));
        }
    }
}
