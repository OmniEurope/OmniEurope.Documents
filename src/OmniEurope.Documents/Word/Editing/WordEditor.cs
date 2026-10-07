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
    public IReadOnlyList<string> TextParts
    {
        get
        {
            var relationships = _package.Relationships(_main).Where(r => !r.External && _package.Contains(r.Target)).ToList();
            var parts = new List<string> { _main };
            foreach (var type in new[] { HeaderType, FooterType, FootnotesType, EndnotesType, CommentsType })
            {
                parts.AddRange(relationships.Where(r => r.Type == type).Select(r => r.Target).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal));
            }

            return parts;
        }
    }

    /// <summary>Every paragraph of the text parts, in <see cref="TextParts"/> order then document order.</summary>
    public IReadOnlyList<WordEditableParagraph> Paragraphs()
    {
        var context = new WordReadContext(_package);
        var result = new List<WordEditableParagraph>();
        foreach (var part in TextParts)
        {
            var root = _package.GetXml(part)?.Root;
            if (root is null)
            {
                continue;
            }

            var index = 0;
            foreach (var paragraph in WordRunScanner.Paragraphs(root))
            {
                result.Add(new WordEditableParagraph(context, part, index++, paragraph));
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

    internal XElement Body => _package.GetXml(_main)!.Root!.Element(W + "body") ?? throw new DocumentFormatException("The document has no body.");
}
