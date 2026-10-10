// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Text;

/// <summary>
/// The paragraph-level part of UAX #9: matching isolates (BD9), explicit levels and directions (X1 to X8), removal of
/// embedding controls and boundary neutrals (X9), then the isolating run sequences (BD13, X10), each resolved by
/// <see cref="BidiSequence"/>. Removed characters take the level of the character before them.
/// </summary>
internal sealed class BidiParagraph
{
    private const int MaxDepth = 125;
    private readonly string _text;
    private readonly BidiClass[] _initial;
    private readonly BidiClass[] _types;
    private readonly byte[] _levels;
    private readonly byte _paragraphLevel;
    private readonly int[] _matchingPdi;
    private readonly HashSet<int> _matchedPdis = [];

    public BidiParagraph(string text, BidiClass[] initial, byte paragraphLevel)
    {
        _text = text;
        _initial = initial;
        _types = (BidiClass[])initial.Clone();
        _levels = new byte[initial.Length];
        _paragraphLevel = paragraphLevel;
        _matchingPdi = MatchIsolates();
    }

    public byte[] Resolve()
    {
        Explicit();
        foreach (var sequence in IsolatingRunSequences())
        {
            new BidiSequence(_text, _initial, _types, _levels, sequence, Sos(sequence), Eos(sequence)).Resolve();
        }

        for (var i = 0; i < _levels.Length; i++)
        {
            if (IsRemoved(i))
            {
                _levels[i] = i == 0 ? _paragraphLevel : _levels[i - 1];
            }
        }

        return _levels;
    }

    private static bool IsInitiator(BidiClass type) => type is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;

    private bool IsRemoved(int i) => _initial[i] is BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO or BidiClass.PDF or BidiClass.BN;

    // BD9: an isolate initiator matches the first PDI after it at the same isolate depth, before the paragraph end.
    private int[] MatchIsolates()
    {
        var matching = Enumerable.Repeat(-1, _initial.Length).ToArray();
        var open = new Stack<int>();
        for (var i = 0; i < _initial.Length; i++)
        {
            if (IsInitiator(_initial[i]))
            {
                open.Push(i);
            }
            else if (_initial[i] == BidiClass.PDI && open.Count > 0)
            {
                matching[open.Pop()] = i;
                _matchedPdis.Add(i);
            }
            else if (_initial[i] == BidiClass.B)
            {
                open.Clear();
            }
        }

        return matching;
    }

    // X1 to X8: the directional status stack.
    private void Explicit()
    {
        var state = new ExplicitState(_paragraphLevel);
        for (var i = 0; i < _initial.Length; i++)
        {
            var type = _initial[i];
            switch (type)
            {
                case BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO:
                    _levels[i] = state.Top.Level;
                    state.Embed(type);
                    break;
                case BidiClass.RLI or BidiClass.LRI or BidiClass.FSI:
                    Apply(i, state.Top);
                    state.Isolate(type == BidiClass.RLI || (type == BidiClass.FSI && FirstStrongIsRtl(i + 1, _matchingPdi[i])));
                    break;
                case BidiClass.PDI:
                    state.CloseIsolate();
                    Apply(i, state.Top);
                    break;
                case BidiClass.PDF:
                    state.Pop();
                    _levels[i] = state.Top.Level;
                    break;
                case BidiClass.B:
                    _levels[i] = _paragraphLevel;
                    break;
                case BidiClass.BN:
                    _levels[i] = state.Top.Level;
                    break;
                default:
                    Apply(i, state.Top);
                    break;
            }
        }
    }

    private void Apply(int i, ExplicitState.Entry entry)
    {
        _levels[i] = entry.Level;
        if (entry.Override != BidiClass.ON)
        {
            _types[i] = entry.Override;
        }
    }

    // P2 and P3 for an FSI: the first strong character up to its matching PDI, nested isolates skipped.
    private bool FirstStrongIsRtl(int start, int end)
    {
        var stop = end < 0 ? _initial.Length : end;
        for (var i = start; i < stop; i++)
        {
            var type = _initial[i];
            if (IsInitiator(type))
            {
                if (_matchingPdi[i] < 0)
                {
                    return false;
                }

                i = _matchingPdi[i];
                continue;
            }

            if (type == BidiClass.L)
            {
                return false;
            }

            if (BidiClasses.IsRightToLeft(type))
            {
                return true;
            }
        }

        return false;
    }

    // BD13: level runs chained across isolates, a run ending with an isolate initiator continuing with its PDI's run.
    private List<List<int>> IsolatingRunSequences()
    {
        var runs = LevelRuns();
        var startingWith = new Dictionary<int, List<int>>();
        foreach (var run in runs)
        {
            startingWith[run[0]] = run;
        }

        var sequences = new List<List<int>>();
        foreach (var run in runs)
        {
            if (_initial[run[0]] == BidiClass.PDI && _matchedPdis.Contains(run[0]))
            {
                continue;
            }

            var sequence = new List<int>(run);
            var last = run;
            while (IsInitiator(_initial[last[^1]]) && _matchingPdi[last[^1]] is var pdi and >= 0 && startingWith.TryGetValue(pdi, out var next))
            {
                sequence.AddRange(next);
                last = next;
            }

            sequences.Add(sequence);
        }

        return sequences;
    }

    private List<List<int>> LevelRuns()
    {
        var runs = new List<List<int>>();
        List<int>? current = null;
        for (var i = 0; i < _initial.Length; i++)
        {
            if (IsRemoved(i))
            {
                continue;
            }

            if (current is null || _levels[current[^1]] != _levels[i])
            {
                current = [];
                runs.Add(current);
            }

            current.Add(i);
        }

        return runs;
    }

    private BidiClass Sos(List<int> sequence)
    {
        var before = sequence[0] - 1;
        while (before >= 0 && IsRemoved(before))
        {
            before--;
        }

        var level = before < 0 ? _paragraphLevel : _levels[before];
        return Direction(Math.Max(level, _levels[sequence[0]]));
    }

    private BidiClass Eos(List<int> sequence)
    {
        var last = sequence[^1];
        var after = last + 1;
        while (after < _initial.Length && IsRemoved(after))
        {
            after++;
        }

        var level = IsInitiator(_initial[last]) || after >= _initial.Length ? _paragraphLevel : _levels[after];
        return Direction(Math.Max(level, _levels[last]));
    }

    private static BidiClass Direction(int level) => (level & 1) == 0 ? BidiClass.L : BidiClass.R;

    /// <summary>The directional status stack of X1 to X8, with its overflow counters.</summary>
    private sealed class ExplicitState
    {
        private readonly List<Entry> _stack;
        private int _overflowIsolates;
        private int _overflowEmbeddings;
        private int _validIsolates;

        public ExplicitState(byte paragraphLevel) => _stack = [new Entry(paragraphLevel, BidiClass.ON, false)];

        public Entry Top => _stack[^1];

        // X2 to X5: an embedding or override one level up (odd or even), unless the stack is full.
        public void Embed(BidiClass type)
        {
            var level = Next(Top.Level, type is BidiClass.RLE or BidiClass.RLO);
            if (level <= MaxDepth && _overflowIsolates == 0 && _overflowEmbeddings == 0)
            {
                _stack.Add(new Entry((byte)level, type switch { BidiClass.RLO => BidiClass.R, BidiClass.LRO => BidiClass.L, _ => BidiClass.ON }, false));
            }
            else if (_overflowIsolates == 0)
            {
                _overflowEmbeddings++;
            }
        }

        // X5a to X5c.
        public void Isolate(bool rtl)
        {
            var level = Next(Top.Level, rtl);
            if (level <= MaxDepth && _overflowIsolates == 0 && _overflowEmbeddings == 0)
            {
                _validIsolates++;
                _stack.Add(new Entry((byte)level, BidiClass.ON, true));
            }
            else
            {
                _overflowIsolates++;
            }
        }

        // X6a.
        public void CloseIsolate()
        {
            if (_overflowIsolates > 0)
            {
                _overflowIsolates--;
                return;
            }

            if (_validIsolates == 0)
            {
                return;
            }

            _overflowEmbeddings = 0;
            while (!Top.Isolate)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }

            _stack.RemoveAt(_stack.Count - 1);
            _validIsolates--;
        }

        // X7.
        public void Pop()
        {
            if (_overflowIsolates > 0)
            {
                return;
            }

            if (_overflowEmbeddings > 0)
            {
                _overflowEmbeddings--;
            }
            else if (!Top.Isolate && _stack.Count >= 2)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }
        }

        private static int Next(int level, bool odd) => odd ? (level + 1) | 1 : (level + 2) & ~1;

        public readonly record struct Entry(byte Level, BidiClass Override, bool Isolate);
    }
}
