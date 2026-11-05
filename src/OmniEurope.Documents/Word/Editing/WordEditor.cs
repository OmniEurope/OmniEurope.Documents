// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Word.Reading;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// Edits an existing .docx in place: everything the file holds is kept, and saving rewrites only the parts
/// that changed (the other parts are copied byte for byte). Opening applies the same checks as
/// <see cref="WordDocument.Load(Stream, PackageLimits?)"/>: size limits, no DTD, macros refused.
/// </summary>
public sealed class WordEditor
{
    private static readonly (string Type, WordPartKind Kind)[] StoryTypes =
    [
        (HeaderType, WordPartKind.Header), (FooterType, WordPartKind.Footer), (FootnotesType, WordPartKind.Footnote),
        (EndnotesType, WordPartKind.Endnote), (CommentsType, WordPartKind.Comment),
    ];

    private readonly OpcPackage _package;
    private readonly string _main;

    private WordEditor(OpcPackage package)
    {
        _package = package;
        _main = WordDocumentReader.MainPart(package);
    }

    /// <summary>Opens a document for editing.</summary>
    public static WordEditor Open(Stream stream, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new WordEditor(OpcPackage.Open(stream, limits));
    }

    /// <summary>Opens a document for editing from bytes.</summary>
    public static WordEditor Open(byte[] bytes, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Open(new MemoryStream(bytes, writable: false), limits);
    }

    /// <summary>The parts added or modified so far.</summary>
    public IReadOnlyCollection<string> ChangedParts => _package.ChangedParts.ToList();

    internal OpcPackage Package => _package;

    internal string MainPart => _main;

    /// <summary>
    /// The text-bearing parts in a fixed order: body, headers, footers, footnotes, endnotes, comments
    /// (headers and footers sorted by part name).
    /// </summary>
    public IReadOnlyList<string> TextParts => Stories().Select(s => s.PartName).ToList();

    /// <summary>
    /// Every paragraph of the text parts, in <see cref="TextParts"/> order then document order: a paragraph comes
    /// before the paragraphs of its own text boxes, and paragraphs inside tables, content controls, custom XML
    /// and text boxes are included (alternate-content fallbacks are not). The order, addresses and locations are
    /// the same for the document saved again without change.
    /// </summary>
    public IReadOnlyList<WordEditableParagraph> Paragraphs()
    {
        var context = new WordEditContext(_package, _main);
        var result = new List<WordEditableParagraph>();
        foreach (var story in Stories())
        {
            var root = _package.GetXml(story.PartName)?.Root;
            if (root is null)
            {
                continue;
            }

            var locator = new WordParagraphLocator(story);
            var index = 0;
            foreach (var paragraph in WordRunScanner.Paragraphs(root))
            {
                result.Add(new WordEditableParagraph(context, story.PartName, index++, paragraph, locator.Locate(paragraph)));
            }
        }

        return result;
    }

    /// <summary>The paragraph at an address from <see cref="WordEditableParagraph.Address"/>, or null.</summary>
    public WordEditableParagraph? FindParagraph(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return Paragraphs().FirstOrDefault(p => string.Equals(p.Address, address, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The paragraph at a location from <see cref="WordEditableParagraph.Location"/>, or null.</summary>
    public WordEditableParagraph? FindParagraph(WordParagraphLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Paragraphs().FirstOrDefault(p => p.Location == location);
    }

    // The text parts with their kinds; a part referred to several times appears once, with its first id.
    private List<WordStory> Stories()
    {
        var relationships = _package.Relationships(_main).Where(r => !r.External && _package.Contains(r.Target)).ToList();
        var stories = new List<WordStory> { new(_main, WordPartKind.Body, null) };
        foreach (var (type, kind) in StoryTypes)
        {
            stories.AddRange(relationships.Where(r => r.Type == type)
                .GroupBy(r => r.Target, StringComparer.OrdinalIgnoreCase)
                .Select(g => new WordStory(g.Key, kind, kind is WordPartKind.Header or WordPartKind.Footer ? g.First().Id : null))
                .OrderBy(s => s.PartName, StringComparer.Ordinal));
        }

        return stories;
    }

    /// <summary>Replaces every occurrence of <paramref name="search"/> in all text parts, also across run
    /// boundaries; returns the number of replacements.</summary>
    public int ReplaceText(string search, string replacement, StringComparison comparison = StringComparison.Ordinal)
    {
        ArgumentException.ThrowIfNullOrEmpty(search);
        ArgumentNullException.ThrowIfNull(replacement);
        var count = 0;
        foreach (var part in TextParts)
        {
            var root = _package.GetXml(part)?.Root;
            foreach (var paragraph in root is null ? [] : WordRunScanner.Paragraphs(root).ToList())
            {
                count += WordTextReplacer.Replace(paragraph, search, replacement, comparison);
            }
        }

        return count;
    }

    /// <summary>Replaces each key by its value (for example <c>{{name}}</c> placeholders); returns the total count.</summary>
    public int ReplaceText(IReadOnlyDictionary<string, string> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        return replacements.Sum(pair => ReplaceText(pair.Key, pair.Value));
    }

    /// <summary>
    /// The content controls (<c>w:sdt</c>) of the text parts, in <see cref="TextParts"/> order then document order
    /// (an outer control before the controls it holds): block, inline, row and cell level, with their kind,
    /// current text, placeholder state, date settings, list entries and check state.
    /// </summary>
    public IReadOnlyList<WordContentControl> ContentControls() => Controls().Select(c => c.Info).ToList();

    /// <summary>
    /// Fills the content controls whose tag is a key of <paramref name="valuesByTag"/> (all controls sharing a
    /// tag are filled): the content becomes the value in the run formatting of the control's first run, the
    /// placeholder state is cleared, a date is parsed (<c>yyyy-MM-dd</c> or the control's own format in its
    /// language) and written in that format with <c>w:fullDate</c> updated, a list takes a value or display text
    /// and writes the display text (a combo box also takes free text, written as it is; a drop-down list refuses a value outside its entries), a check box takes a boolean. A value that does not fit leaves its control
    /// unchanged and is reported in <see cref="WordContentControlFill.Gaps"/>; a tag matching no control is
    /// reported in <see cref="WordContentControlFill.UnknownTags"/>.
    /// </summary>
    public WordContentControlFill FillContentControls(IReadOnlyDictionary<string, string> valuesByTag)
    {
        ArgumentNullException.ThrowIfNull(valuesByTag);
        return Filler().Fill(valuesByTag);
    }

    /// <summary>Sets the date of the date controls tagged <paramref name="tag"/>, written in their own format and language.</summary>
    public WordContentControlFill SetDate(string tag, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return Filler().SetDate(tag, date);
    }

    /// <summary>Selects an entry, by value or display text, in the combo boxes and drop-down lists tagged <paramref name="tag"/>; text matching no entry is written as free text in a combo box, as Word does, and refused in a drop-down list (reported as a gap).</summary>
    public WordContentControlFill SelectItem(string tag, string valueOrDisplayText)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(valueOrDisplayText);
        return Filler().SelectItem(tag, valueOrDisplayText);
    }

    /// <summary>Checks or unchecks the check boxes tagged <paramref name="tag"/>: <c>w14:checked</c> and the glyph
    /// follow the control's own checked and unchecked states (U+2612 and U+2610 by default).</summary>
    public WordContentControlFill SetChecked(string tag, bool value)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return Filler().SetChecked(tag, value);
    }

    private List<ScannedControl> Controls() =>
        WordContentControlScanner.Scan(Stories().Select(s => (s.PartName, Root: _package.GetXml(s.PartName)?.Root)).Where(p => p.Root is not null).Select(p => (p.PartName, p.Root!)));

    private WordContentControlFiller Filler() => new(Controls(), PlaceholderStyle);

    // The character style of the text a content control shows as a placeholder.
    private bool PlaceholderStyle(string styleId)
    {
        var styles = WordDocumentReader.Part(_package, _package.Relationships(_main), StylesType)?.Root;
        var style = styles?.Elements(W + "style").FirstOrDefault(s => Attr(s, "styleId") == styleId);
        return styleId == "PlaceholderText" || string.Equals(Val(style, "name"), "Placeholder Text", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The tracked changes of the text parts (in <see cref="TextParts"/> order), then of the styles and list
    /// definitions, each part in document order; changes inside alternate-content fallbacks are not listed.
    /// </summary>
    public IReadOnlyList<WordTrackedChange> TrackedChanges() => Revisions().List();

    /// <summary>Accepts every tracked change, alternate-content fallbacks included; returns the number listed by
    /// <see cref="TrackedChanges"/> before.</summary>
    public int AcceptAllChanges() => Revisions().ApplyAll(accept: true);

    /// <summary>Rejects every tracked change, alternate-content fallbacks included; returns the number listed by
    /// <see cref="TrackedChanges"/> before.</summary>
    public int RejectAllChanges() => Revisions().ApplyAll(accept: false);

    /// <summary>Accepts one change of the current <see cref="TrackedChanges"/> list (its copy in an alternate-content
    /// fallback, if any, is left as it is).</summary>
    /// <exception cref="ArgumentException">The list has changed since <paramref name="change"/> was read.</exception>
    public void AcceptChange(WordTrackedChange change) => Revisions().Apply(change, accept: true);

    /// <summary>Rejects one change of the current <see cref="TrackedChanges"/> list.</summary>
    /// <exception cref="ArgumentException">The list has changed since <paramref name="change"/> was read.</exception>
    public void RejectChange(WordTrackedChange change) => Revisions().Apply(change, accept: false);

    private WordRevisionEditor Revisions() => new(_package, _main, TextParts);

    /// <summary>The core properties; setting them rewrites only the elements that changed.</summary>
    public WordInformation Information
    {
        get => WordPartsReader.Information(CorePart() is { } part ? _package.GetXml(part) : null);
        set => WordCoreEditor.Apply(_package, CorePart(), value ?? throw new ArgumentNullException(nameof(value)));
    }

    private string? CorePart() => _package.Relationships(string.Empty).Find(r => r.Type == CorePropertiesType && !r.External)?.Target;

    /// <summary>
    /// Appends the body of <paramref name="other"/> as new sections, with the pictures, links, headers,
    /// footers, footnotes, endnotes, styles and lists it uses. Styles whose id already exists keep this
    /// document's definition; comments of <paramref name="other"/> are not carried over (their anchors are
    /// removed).
    /// </summary>
    public void Append(WordEditor other, bool startOnNewPage = true)
    {
        ArgumentNullException.ThrowIfNull(other);
        new WordMerger(this, other).Append(startOnNewPage);
    }

    /// <summary>Merges documents into the first one, each starting a new section.</summary>
    public static byte[] Merge(params IEnumerable<byte[]> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var list = documents.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Nothing to merge.", nameof(documents));
        }

        var target = Open(list[0]);
        foreach (var bytes in list.Skip(1))
        {
            target.Append(Open(bytes));
        }

        return target.ToArray();
    }

    /// <summary>A model of the document as currently edited.</summary>
    public WordDocument ToDocument() => WordDocumentReader.Read(_package);

    /// <summary>Writes the package; untouched parts keep their exact bytes.</summary>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _package.Save(stream);
    }

    /// <summary>The package as bytes.</summary>
    public byte[] ToArray()
    {
        using var stream = new MemoryStream();
        Save(stream);
        return stream.ToArray();
    }

    /// <summary>True when the main part is a template (<c>.dotx</c>): <see cref="Save(Stream)"/> keeps it a template.</summary>
    public bool IsTemplate => _package.ContentTypeOf(_main) == TemplateContentType;

    /// <summary>
    /// Writes the package as a document: a template (<c>.dotx</c>) gets the document content type for its main
    /// part and the editor then holds a document (<see cref="IsTemplate"/> is false); a document is written as
    /// <see cref="Save(Stream)"/> does.
    /// </summary>
    public void SaveAsDocument(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _package.SetContentType(_main, MainContentType);
        Save(stream);
    }

    /// <summary>The package as document bytes, see <see cref="SaveAsDocument(Stream)"/>.</summary>
    public byte[] ToDocumentArray()
    {
        using var stream = new MemoryStream();
        SaveAsDocument(stream);
        return stream.ToArray();
    }

    internal XElement Body => _package.GetXml(_main)!.Root!.Element(W + "body") ?? throw new DocumentFormatException("The document has no body.");
}
