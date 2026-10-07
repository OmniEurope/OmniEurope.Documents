// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Internal;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>Builds a <see cref="WordDocument"/> from a package.</summary>
internal static class WordDocumentReader
{
    /// <summary>
    /// The main document part, after the safety checks: macro-enabled documents and templates are refused,
    /// as is any package carrying a VBA project; Strict Open XML is not supported.
    /// </summary>
    public static string MainPart(OpcPackage package)
    {
        if (package.DeclaredContentTypes.Any(t => t.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) || t.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))
            || package.PartNames.Any(n => n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DocumentFormatException("Macro-enabled documents are refused.");
        }

        var main = package.Relationships(string.Empty).Find(r => r.Type == OfficeDocumentType && !r.External)?.Target;
        if (main is null || !package.Contains(main))
        {
            throw new DocumentFormatException("The package has no main document part.");
        }

        if (package.ContentTypeOf(main) is not (MainContentType or TemplateContentType))
        {
            throw new DocumentFormatException("The package is not a Word document.");
        }

        if (package.GetXml(main)?.Root?.Name.Namespace == StrictW)
        {
            throw new DocumentFormatException("Strict Open XML documents are not supported.");
        }

        return main;
    }

    public static WordDocument Read(OpcPackage package)
    {
        var main = MainPart(package);
        var context = new WordReadContext(package);
        var relationships = package.Relationships(main);
        var document = new WordDocument(empty: true)
        {
            Styles = WordPartsReader.Styles(Part(package, relationships, StylesType), Part(package, relationships, ThemeType)),
            Settings = WordPartsReader.Settings(Part(package, relationships, SettingsType)),
            Information = WordPartsReader.Information(Part(package, package.Relationships(string.Empty), CorePropertiesType)),
        };
        document.Numbering = WordPartsReader.Numbering(Part(package, relationships, NumberingType), document.Styles);
        ReadNotes(context, relationships, FootnotesType, "footnote", document.Footnotes);
        ReadNotes(context, relationships, EndnotesType, "endnote", document.Endnotes);
        ReadComments(context, relationships, document.Comments);
        ReadBody(context, main, document);
        document.Gaps.AddRange(context.Gaps);
        return document;
    }

    private static XDocument? Part(OpcPackage package, List<OpcRelationship> relationships, string type)
    {
        var target = relationships.Find(r => r.Type == type && !r.External)?.Target;
        return target is null ? null : package.GetXml(target);
    }

    private static void ReadBody(WordReadContext context, string main, WordDocument document)
    {
        var body = context.Package.GetXml(main)!.Root?.Element(W + "body") ?? throw new DocumentFormatException("The document has no body.");
        var reader = new WordContentReader(context, main);
        var headers = new Dictionary<string, WordHeaderFooter>(StringComparer.OrdinalIgnoreCase);
        var blocks = new List<WordBlock>();
        XElement? last = null;
        foreach (var child in body.Elements())
        {
            if (child.Name == W + "sectPr")
            {
                last = child;
                continue;
            }

            reader.AddBlock(child, blocks);
            if (child.Name == W + "p" && child.Element(W + "pPr")?.Element(W + "sectPr") is { } end)
            {
                document.Sections.Add(Section(reader, end, blocks, document.Sections.LastOrDefault(), headers));
                blocks = [];
            }
        }

        document.Sections.Add(Section(reader, last, blocks, document.Sections.LastOrDefault(), headers));
    }

    private static WordSection Section(WordContentReader reader, XElement? sectPr, List<WordBlock> blocks, WordSection? previous, Dictionary<string, WordHeaderFooter> cache)
    {
        var section = new WordSection(WordStructureReader.Page(sectPr));
        section.Blocks.AddRange(blocks);
        ReadHeadersFooters(reader, sectPr, "headerReference", section.Headers, cache);
        ReadHeadersFooters(reader, sectPr, "footerReference", section.Footers, cache);
        foreach (var kind in Enum.GetValues<WordHeaderFooterKind>())
        {
            if (previous?.Headers.TryGetValue(kind, out var header) == true)
            {
                section.Headers.TryAdd(kind, header);
            }

            if (previous?.Footers.TryGetValue(kind, out var footer) == true)
            {
                section.Footers.TryAdd(kind, footer);
            }
        }

        return section;
    }

    private static void ReadHeadersFooters(WordContentReader reader, XElement? sectPr, string element, Dictionary<WordHeaderFooterKind, WordHeaderFooter> target, Dictionary<string, WordHeaderFooter> cache)
    {
        foreach (var reference in sectPr?.Elements(W + element) ?? [])
        {
            var relationship = reader.Relationship(reference.Attribute(R + "id")?.Value);
            if (relationship is null || relationship.External)
            {
                continue;
            }

            if (!cache.TryGetValue(relationship.Target, out var content))
            {
                var root = reader.Context.Package.GetXml(relationship.Target)?.Root;
                content = new WordHeaderFooter { PartName = relationship.Target };
                if (root is not null)
                {
                    content.Blocks.AddRange(new WordContentReader(reader.Context, relationship.Target).Blocks(root));
                }

                cache[relationship.Target] = content;
            }

            var kind = Attr(reference, "type") switch
            {
                "first" => WordHeaderFooterKind.First,
                "even" => WordHeaderFooterKind.Even,
                _ => WordHeaderFooterKind.Default,
            };
            target[kind] = content;
        }
    }

    private static void ReadNotes(WordReadContext context, List<OpcRelationship> relationships, string type, string element, Dictionary<int, WordNote> notes)
    {
        var part = relationships.Find(r => r.Type == type && !r.External)?.Target;
        var root = part is null ? null : context.Package.GetXml(part)?.Root;
        if (root is null)
        {
            return;
        }

        foreach (var note in root.Elements(W + element))
        {
            if (Int(Attr(note, "id")) is not { } id || Attr(note, "type") is "separator" or "continuationSeparator" or "continuationNotice")
            {
                continue;
            }

            var result = new WordNote(id);
            result.Blocks.AddRange(new WordContentReader(context, part!, id).Blocks(note));
            notes[id] = result;
        }
    }

    private static void ReadComments(WordReadContext context, List<OpcRelationship> relationships, List<WordComment> comments)
    {
        var part = relationships.Find(r => r.Type == CommentsType && !r.External)?.Target;
        var root = part is null ? null : context.Package.GetXml(part)?.Root;
        foreach (var element in root?.Elements(W + "comment") ?? [])
        {
            if (Int(Attr(element, "id")) is not { } id)
            {
                continue;
            }

            var comment = new WordComment(id)
            {
                Author = Attr(element, "author"),
                Initials = Attr(element, "initials"),
                Date = WordContentReader.Revision(element, WordRevisionKind.Inserted).Date,
            };
            comment.Blocks.AddRange(new WordContentReader(context, part!).Blocks(element));
            comments.Add(comment);
        }
    }
}
