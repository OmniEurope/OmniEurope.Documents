// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordMarkdown;

/// <summary>Shared state of one Markdown conversion: the document, the notes referred to (in order of first
/// reference), the pictures, the list counters and the gaps met.</summary>
internal sealed class MarkdownContext(WordDocument document, WordMarkdownOptions options)
{
    private readonly Dictionary<(WordNoteKind Kind, int Id), string> _notes = [];
    private int _footnotes;
    private int _endnotes;

    public WordDocument Document { get; } = document;

    public MarkdownImages Images { get; } = new(options.ImageFolder);

    public MarkdownListCounter Lists { get; } = new(document.Numbering);

    public WordListCounter HeadingLabels { get; } = new(document.Numbering);

    public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

    /// <summary>The notes referred to, with their labels, in order of first reference.</summary>
    public List<(WordNoteKind Kind, int Id, string Label)> Notes { get; } = [];

    /// <summary>Tracked deletions are left out: the document reads as if every change were accepted.</summary>
    public static bool Skips(WordRevision? revision) => revision?.Kind == WordRevisionKind.Deleted;

    /// <summary>The footnote label of a note (<c>1</c> for footnotes, <c>e1</c> for endnotes), given on first
    /// reference; null when the document does not hold the note.</summary>
    public string? NoteLabel(WordNoteKind kind, int id)
    {
        if (_notes.TryGetValue((kind, id), out var label))
        {
            return label;
        }

        var notes = kind == WordNoteKind.Footnote ? Document.Footnotes : Document.Endnotes;
        if (!notes.ContainsKey(id))
        {
            Gaps.Add("references to notes the document does not hold are left out");
            return null;
        }

        label = kind == WordNoteKind.Footnote
            ? (++_footnotes).ToString(CultureInfo.InvariantCulture)
            : "e" + (++_endnotes).ToString(CultureInfo.InvariantCulture);
        if (kind == WordNoteKind.Endnote)
        {
            Gaps.Add("endnotes are written as footnotes");
        }

        _notes[(kind, id)] = label;
        Notes.Add((kind, id, label));
        return label;
    }
}

/// <summary>The pictures of the Markdown, named in order of first use; a picture used twice keeps its name.</summary>
internal sealed class MarkdownImages(string folder)
{
    private readonly Dictionary<WordImage, WordMarkdownImage> _images = new(ReferenceEqualityComparer.Instance);
    private readonly List<WordMarkdownImage> _all = [];

    public IReadOnlyList<WordMarkdownImage> All => _all;

    /// <summary>The reference name of a picture (<c>image1</c>), its file added on first use.</summary>
    public string Reference(WordImage image)
    {
        if (!_images.TryGetValue(image, out var entry))
        {
            var name = "image" + (_all.Count + 1).ToString(CultureInfo.InvariantCulture) + "." + Extension(image.ContentType);
            var trimmed = folder.Trim('/', '\\');
            entry = new WordMarkdownImage(trimmed.Length == 0 ? name : trimmed + "/" + name, image.ContentType, image.Data);
            _images[image] = entry;
            _all.Add(entry);
        }

        return Name(entry);
    }

    /// <summary>The reference name of an image file (its file name without extension).</summary>
    public static string Name(WordMarkdownImage image) => System.IO.Path.GetFileNameWithoutExtension(image.Path);

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "png", ["image/jpeg"] = "jpg", ["image/gif"] = "gif", ["image/bmp"] = "bmp", ["image/tiff"] = "tif",
        ["image/svg+xml"] = "svg", ["image/x-emf"] = "emf", ["image/emf"] = "emf", ["image/x-wmf"] = "wmf", ["image/wmf"] = "wmf",
    };

    private static string Extension(string contentType) => Extensions.GetValueOrDefault(contentType, "bin");
}

/// <summary>The number of each ordered list item: a level starts at its start value, goes on with each item
/// of the same list and starts again when a shallower item of that list comes.</summary>
internal sealed class MarkdownListCounter(WordNumbering numbering)
{
    private readonly Dictionary<int, int?[]> _counters = [];

    /// <summary>The level definition and the item number, or null when the paragraph is not in a list.</summary>
    public (WordNumberingLevel Level, int Number)? Next(int numberingId, int level)
    {
        level = Math.Clamp(level, 0, 8);
        if (numbering.GetLevel(numberingId, level) is not { } definition)
        {
            return null;
        }

        if (!_counters.TryGetValue(numberingId, out var counters))
        {
            counters = new int?[9];
            _counters[numberingId] = counters;
        }

        counters[level] = counters[level] + 1 ?? definition.Start;
        Array.Fill(counters, null, level + 1, 8 - level);
        return (definition, counters[level]!.Value);
    }
}
