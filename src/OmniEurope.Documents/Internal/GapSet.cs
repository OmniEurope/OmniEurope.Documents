// SPDX-License-Identifier: EUPL-1.2
using System.Collections;

namespace OmniEurope.Documents.Internal;

/// <summary>The gaps met by a read or a conversion: each English text once, in ordinal order of the texts.</summary>
internal sealed class GapSet : IEnumerable<DocumentGap>
{
    private readonly SortedDictionary<string, DocumentGap> _gaps = new(StringComparer.Ordinal);

    public GapSet()
    {
    }

    public GapSet(IEnumerable<DocumentGap> gaps) => UnionWith(gaps);

    public int Count => _gaps.Count;

    /// <summary>The English texts, in order.</summary>
    public IReadOnlyList<string> Messages => _gaps.Keys.ToList();

    /// <summary>Adds a gap unless one with the same English text is already there.</summary>
    public bool Add(DocumentGap gap) => _gaps.TryAdd(gap.Message, gap);

    // TRANSITIONAL: removed once every engine gap has a code.
    public bool Add(string text) => Add(DocumentGap.FromText(text));

    public void UnionWith(IEnumerable<DocumentGap> gaps)
    {
        foreach (var gap in gaps)
        {
            Add(gap);
        }
    }

    public bool Contains(DocumentGap gap) => _gaps.ContainsKey(gap.Message);

    /// <summary>The gaps of a public list of texts that the caller may have changed, in its order, each with its
    /// detail: a text that no detail carries is a gap without a code.</summary>
    public static IEnumerable<DocumentGap> Of(IEnumerable<string> texts, IEnumerable<DocumentGap> details)
    {
        var known = new Dictionary<string, DocumentGap>(StringComparer.Ordinal);
        foreach (var detail in details)
        {
            known.TryAdd(detail.Message, detail);
        }

        return texts.Select(t => known.TryGetValue(t, out var detail) ? detail : DocumentGap.FromText(t));
    }

    public IEnumerator<DocumentGap> GetEnumerator() => _gaps.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
