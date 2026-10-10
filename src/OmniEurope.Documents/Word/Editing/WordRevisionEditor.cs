// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Internal;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>The tracked changes of an edited document: listed, accepted or rejected all together or one by one.</summary>
internal sealed class WordRevisionEditor(OpcPackage package, string mainPart, IReadOnlyList<string> textParts)
{
    // The text parts, then the styles and list definitions, whose formatting changes are tracked too.
    private IEnumerable<string> Parts() => textParts.Concat(
        package.Relationships(mainPart).Where(r => !r.External && (r.Type == StylesType || r.Type == NumberingType) && package.Contains(r.Target)).Select(r => r.Target)).Distinct(StringComparer.OrdinalIgnoreCase);

    private List<ScannedChange> Scan(bool includeFallback) =>
        Parts().SelectMany(part => package.GetXml(part)?.Root is { } root ? WordTrackedChangeScanner.Scan(root, part, includeFallback) : []).ToList();

    public IReadOnlyList<WordTrackedChange> List() => Scan(includeFallback: false).Select(WordTrackedChangeScanner.Describe).ToList();

    public int ApplyAll(bool accept)
    {
        var changes = Scan(includeFallback: true);
        var listed = changes.Count(c => !c.Element.Ancestors(Mc + "Fallback").Any());
        foreach (var change in changes)
        {
            WordTrackedChangeApplier.Apply(change, accept);
        }

        return listed;
    }

    public void Apply(WordTrackedChange change, bool accept)
    {
        ArgumentNullException.ThrowIfNull(change);
        var changes = Scan(includeFallback: false);
        var found = change.Index >= 0 && change.Index < changes.Count ? changes[change.Index] : null;
        if (found is null || found.Kind != change.Kind || found.Id != change.Id || !string.Equals(found.PartName, change.PartName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The change is not in the document as listed; list the tracked changes again.", nameof(change));
        }

        WordTrackedChangeApplier.Apply(found, accept);
    }
}
