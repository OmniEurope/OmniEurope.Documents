// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>A text part of the document with its kind and, for a header or footer, its relationship id.</summary>
internal sealed record WordStory(string PartName, WordPartKind Kind, string? RelationshipId);

/// <summary>
/// Computes the <see cref="WordParagraphLocation"/> of paragraphs of one part. Positions are counted once per
/// container and cached, so locating every paragraph of a large part stays linear.
/// </summary>
internal sealed class WordParagraphLocator(WordStory story)
{
    private enum Level
    {
        Blocks,
        Table,
        Inline,
    }

    private static readonly HashSet<XName> Roots = [W + "body", W + "hdr", W + "ftr", W + "footnote", W + "endnote", W + "comment"];

    private static readonly HashSet<XName> Blocks = [W + "p", W + "tbl", W + "sdt", W + "customXml"];

    private readonly Dictionary<XElement, Dictionary<XElement, int>> _indexes = [];

    public WordParagraphLocation Locate(XElement paragraph)
    {
        var chain = paragraph.AncestorsAndSelf().TakeWhile(e => e.Parent is not null && !Roots.Contains(e.Name)).Reverse().ToList();
        var root = chain[0].Parent!;
        var isNote = root.Name == W + "footnote" || root.Name == W + "endnote";
        var id = Int(Attr(root, "id"));
        return new WordParagraphLocation(story.PartName, story.Kind, Steps(chain))
        {
            RelationshipId = story.RelationshipId,
            NoteId = isNote ? id : null,
            IsSeparatorNote = isNote && (Attr(root, "type") is { } type && type != "normal" || id is -1 or 0),
            CommentId = root.Name == W + "comment" ? id : null,
        };
    }

    private List<WordPathStep> Steps(List<XElement> chain)
    {
        var steps = new List<WordPathStep>();
        var level = Level.Blocks;
        XElement? owner = null;
        foreach (var element in chain)
        {
            (level, owner) = level switch
            {
                Level.Blocks => BlockStep(element, steps),
                Level.Table => TableStep(element, steps, owner!),
                _ => InlineStep(element, steps, owner!),
            };
        }

        return steps;
    }

    // Wrappers that are not blocks (sdtContent, alternate content) are transparent.
    private (Level, XElement?) BlockStep(XElement element, List<WordPathStep> steps)
    {
        if (!Blocks.Contains(element.Name))
        {
            return (Level.Blocks, null);
        }

        steps.Add(WordPathStep.Block(IndexOf(element.Parent!, element, parent => parent.Elements().Where(e => Blocks.Contains(e.Name)))));
        switch (element.Name.LocalName)
        {
            case "tbl":
                return (Level.Table, element);
            case "sdt":
                steps.Add(WordPathStep.ContentControl);
                return (Level.Blocks, null);
            case "customXml":
                steps.Add(WordPathStep.CustomXml);
                return (Level.Blocks, null);
            default:
                return (Level.Inline, element);
        }
    }

    // Row and cell wrappers (content controls, custom XML) are transparent: rows count among the rows of the
    // table, cells among the cells of the row.
    private (Level, XElement?) TableStep(XElement element, List<WordPathStep> steps, XElement owner)
    {
        if (element.Name == W + "tr")
        {
            steps.Add(WordPathStep.Row(IndexOf(owner, element, table => Nearest(table, W + "tr", W + "tbl"))));
            return (Level.Table, element);
        }

        if (element.Name == W + "tc")
        {
            steps.Add(WordPathStep.Cell(IndexOf(owner, element, row => Nearest(row, W + "tc", W + "tr"))));
            return (Level.Blocks, null);
        }

        return (Level.Table, owner);
    }

    private (Level, XElement?) InlineStep(XElement element, List<WordPathStep> steps, XElement owner)
    {
        if (element.Name != W + "txbxContent")
        {
            return (Level.Inline, owner);
        }

        steps.Add(WordPathStep.TextBox(IndexOf(owner, element, TextBoxes)));
        return (Level.Blocks, null);
    }

    /// <summary>The text boxes anchored in a paragraph itself, alternate-content fallbacks excluded.</summary>
    private static IEnumerable<XElement> TextBoxes(XElement paragraph) =>
        Nearest(paragraph, W + "txbxContent", W + "p").Where(t => !t.Ancestors(Mc + "Fallback").Any());

    private static IEnumerable<XElement> Nearest(XElement owner, XName name, XName ownerName) =>
        owner.Descendants(name).Where(d => d.Ancestors(ownerName).First() == owner);

    private int IndexOf(XElement owner, XElement member, Func<XElement, IEnumerable<XElement>> members)
    {
        if (!_indexes.TryGetValue(owner, out var indexes))
        {
            indexes = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
            foreach (var element in members(owner))
            {
                indexes[element] = indexes.Count;
            }

            _indexes[owner] = indexes;
        }

        return indexes[member];
    }
}
