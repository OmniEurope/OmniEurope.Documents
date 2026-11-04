// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// Opens and closes the wrapper elements of content controls (<see cref="WordHtmlOptions.MarkContentControls"/>)
/// around a run of blocks (<c>div</c>) or inlines (<c>span</c>): items that share the same control marks, outermost
/// first, sit in the same wrapper. The first <paramref name="baseDepth"/> marks of every item are already written
/// by an enclosing writer and are skipped.
/// </summary>
internal sealed class HtmlControlWrappers(HtmlContext context, HtmlOutput output, string tag, int baseDepth, Action? beforeChange = null)
{
    private readonly List<WordContentControlMark> _open = [];

    /// <summary>Closes and opens wrappers so exactly the marks of <paramref name="marks"/> are open.</summary>
    public void Sync(IReadOnlyList<WordContentControlMark> marks)
    {
        if (!context.Options.MarkContentControls)
        {
            return;
        }

        var wanted = marks.Count > baseDepth ? marks.Skip(baseDepth).ToList() : [];
        var common = 0;
        while (common < _open.Count && common < wanted.Count && ReferenceEquals(_open[common], wanted[common]))
        {
            common++;
        }

        if (common == _open.Count && common == wanted.Count)
        {
            return;
        }

        beforeChange?.Invoke();
        while (_open.Count > common)
        {
            output.Close(tag);
            _open.RemoveAt(_open.Count - 1);
        }

        foreach (var mark in wanted.Skip(common))
        {
            Open(mark);
            _open.Add(mark);
        }
    }

    /// <summary>Closes every wrapper still open.</summary>
    public void End() => Sync([]);

    private void Open(WordContentControlMark mark) =>
        output.Open(
            tag,
            ("data-oe-field", string.IsNullOrEmpty(mark.Tag) ? null : mark.Tag),
            ("data-oe-field-id", string.IsNullOrEmpty(mark.Tag) ? mark.Id?.ToString(CultureInfo.InvariantCulture) : null),
            ("data-oe-field-kind", mark.Kind.ToString().ToLowerInvariant()),
            ("data-oe-field-alias", string.IsNullOrEmpty(mark.Alias) ? null : mark.Alias),
            ("data-oe-placeholder", mark.ShowingPlaceholder ? "true" : null));
}
