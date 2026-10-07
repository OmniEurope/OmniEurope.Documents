// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>
/// Numbers note references in reading order. Footnotes follow the document settings, overridden member by
/// member by the footnote numbering of the section holding the reference: the number style, the first
/// number, and the restart rule (at each section, or at each page once the pages are known through
/// <see cref="FootnotePages"/>). Endnotes are numbered continuously with the document settings.
/// </summary>
internal sealed class NoteNumbering(WordDocument document)
{
    private readonly Dictionary<(WordNoteKind, int), string> _labels = [];
    private int _counter;
    private int _lastSection = -1;
    private int _lastPage = -1;

    /// <summary>The index of the section whose content is being read; set before reading each section.</summary>
    public int Section { get; set; }

    /// <summary>The page (index from 0) each footnote landed on in a previous layout; null before any layout,
    /// when a restart at each page cannot be applied yet.</summary>
    public IReadOnlyDictionary<int, int>? FootnotePages { get; private set; }

    /// <summary>The order in which footnotes were referenced (their reading order).</summary>
    public List<int> FootnoteOrder { get; } = [];

    /// <summary>The order in which endnotes were referenced.</summary>
    public List<int> EndnoteOrder { get; } = [];

    /// <summary>True when a section restarts footnote numbering at each page.</summary>
    public bool RestartsEachPage => Enumerable.Range(0, document.Sections.Count).Any(s => Rule(s).Restart == WordNoteRestart.EachPage);

    /// <summary>The label of a note reference; the first reference of a note fixes it.</summary>
    public string Label(WordNoteKind kind, int id)
    {
        if (_labels.TryGetValue((kind, id), out var label))
        {
            return label;
        }

        label = kind == WordNoteKind.Footnote ? NextFootnote(id) : NextEndnote(id);
        _labels[(kind, id)] = label;
        return label;
    }

    /// <summary>The effective footnote numbering of section <paramref name="section"/>.</summary>
    public (WordNumberFormat Format, int Start, WordNoteRestart Restart) Rule(int section)
    {
        var settings = document.Settings;
        var own = section >= 0 && section < document.Sections.Count ? document.Sections[section].Page.FootnoteNumbering : null;
        return (own?.Format ?? settings.FootnoteFormat, own?.Start ?? settings.FootnoteStart, own?.Restart ?? settings.FootnoteRestart);
    }

    /// <summary>
    /// Starts numbering again for a new layout in which each footnote restarting at each page takes the page
    /// it landed on in the layout just made (<paramref name="pages"/>: footnote id to page index). Returns
    /// false, changing nothing, when no section restarts at each page or when every footnote is already
    /// numbered after the page it landed on.
    /// </summary>
    public bool Restart(IReadOnlyDictionary<int, int> pages)
    {
        if (!RestartsEachPage || (FootnotePages is not null && SamePages(FootnotePages, pages)))
        {
            return false;
        }

        FootnotePages = pages;
        _labels.Clear();
        FootnoteOrder.Clear();
        EndnoteOrder.Clear();
        (_counter, _lastSection, _lastPage, Section) = (0, -1, -1, 0);
        return true;
    }

    private static bool SamePages(IReadOnlyDictionary<int, int> used, IReadOnlyDictionary<int, int> found) =>
        found.All(pair => used.TryGetValue(pair.Key, out var page) && page == pair.Value);

    private string NextFootnote(int id)
    {
        FootnoteOrder.Add(id);
        var (format, start, restart) = Rule(Section);
        var page = FootnotePages is not null && FootnotePages.TryGetValue(id, out var found) ? found : -1;
        var restarts = FootnoteOrder.Count == 1
            || (restart == WordNoteRestart.EachSection && Section != _lastSection)
            || (restart == WordNoteRestart.EachPage && page != _lastPage);
        _counter = restarts ? start : _counter + 1;
        (_lastSection, _lastPage) = (Section, page);
        return WordNumbering.FormatNumber(_counter, format);
    }

    private string NextEndnote(int id)
    {
        EndnoteOrder.Add(id);
        var settings = document.Settings;
        return WordNumbering.FormatNumber(settings.EndnoteStart + EndnoteOrder.Count - 1, settings.EndnoteFormat);
    }
}
