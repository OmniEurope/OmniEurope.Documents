// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Internal;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>
/// Writes a <see cref="WordDocument"/> as a .docx package. A section other than the last ends on its last
/// paragraph (an empty paragraph is added when the section ends with a table or is empty).
/// </summary>
internal static class WordDocumentWriter
{
    private const string DocumentPart = "word/document.xml";

    public static void Write(WordDocument document, Stream stream)
    {
        if (document.Sections.Count == 0)
        {
            throw new InvalidOperationException("A document needs at least one section.");
        }

        var context = new WordWriteContext();
        var relationships = new PartRelationships(DocumentPart);
        var parts = new List<(string Name, XElement Root, string ContentType)>();
        var writer = new WordContentWriter(context, relationships);

        var body = Body(document, writer, relationships, context, parts);
        parts.Insert(0, (DocumentPart, new XElement(W + "document", WordPartsWriter.Namespaces(), body), MainContentType));
        AddPart(parts, relationships, "word/styles.xml", WordPartsWriter.Styles(document.Styles), StylesType, StylesContentType);
        var footnotes = document.Footnotes.Count > 0;
        var endnotes = document.Endnotes.Count > 0;
        AddPart(parts, relationships, "word/settings.xml", WordPartsWriter.Settings(document.Settings, footnotes, endnotes), SettingsType, SettingsContentType);
        if (!document.Numbering.IsEmpty)
        {
            AddPart(parts, relationships, "word/numbering.xml", WordPartsWriter.Numbering(document.Numbering), NumberingType, NumberingContentType);
        }

        AddAuxiliary(document, context, relationships, parts);
        Package(document, context, relationships, parts, stream);
    }

    private static XElement Body(WordDocument document, WordContentWriter writer, PartRelationships relationships, WordWriteContext context, List<(string, XElement, string)> parts)
    {
        var body = new XElement(W + "body");
        var headerParts = new Dictionary<(WordHeaderFooter Content, string Kind), string>();
        for (var i = 0; i < document.Sections.Count; i++)
        {
            var section = document.Sections[i];
            context.ContentWidth = section.Page.ContentWidth;
            var references = References(section, relationships, context, headerParts, parts);
            var sectPr = WordStructureWriter.Section(section.Page, references);
            var blocks = section.Blocks;
            var last = i == document.Sections.Count - 1;
            for (var b = 0; b < blocks.Count; b++)
            {
                var endsSection = !last && b == blocks.Count - 1 && blocks[b] is WordParagraph;
                body.Add(endsSection ? writer.Paragraph((WordParagraph)blocks[b], sectPr) : writer.Block(blocks[b]));
            }

            if (last)
            {
                body.Add(sectPr);
            }
            else if (blocks.Count == 0 || blocks[^1] is not WordParagraph)
            {
                body.Add(writer.Paragraph(new WordParagraph(), sectPr));
            }
        }

        return body;
    }

    private static List<XElement> References(WordSection section, PartRelationships relationships, WordWriteContext context, Dictionary<(WordHeaderFooter Content, string Kind), string> headerParts, List<(string, XElement, string)> parts)
    {
        var references = new List<XElement>();
        foreach (var (kind, content) in section.Headers.OrderBy(h => h.Key))
        {
            references.Add(Reference("headerReference", kind, HeaderFooterPart(content, "header", relationships, context, headerParts, parts)));
        }

        foreach (var (kind, content) in section.Footers.OrderBy(h => h.Key))
        {
            references.Add(Reference("footerReference", kind, HeaderFooterPart(content, "footer", relationships, context, headerParts, parts)));
        }

        return references;
    }

    private static XElement Reference(string name, WordHeaderFooterKind kind, string relationshipId) => new(
        W + name,
        new XAttribute(W + "type", kind switch
        {
            WordHeaderFooterKind.First => "first",
            WordHeaderFooterKind.Even => "even",
            _ => "default",
        }),
        new XAttribute(R + "id", relationshipId));

    private static string HeaderFooterPart(WordHeaderFooter content, string kind, PartRelationships relationships, WordWriteContext context, Dictionary<(WordHeaderFooter Content, string Kind), string> headerParts, List<(string Name, XElement Root, string ContentType)> parts)
    {
        if (!headerParts.TryGetValue((content, kind), out var name))
        {
            name = "word/" + kind + (parts.Count(p => p.Name.StartsWith("word/" + kind, StringComparison.Ordinal)) + 1).ToString(CultureInfo.InvariantCulture) + ".xml";
            var partRelationships = new PartRelationships(name);
            var writer = new WordContentWriter(context, partRelationships);
            var root = new XElement(W + (kind == "header" ? "hdr" : "ftr"), WordPartsWriter.Namespaces(), writer.Container(content.Blocks));
            parts.Add((name, root, kind == "header" ? HeaderContentType : FooterContentType));
            AddRelationshipsPart(parts, partRelationships);
            headerParts[(content, kind)] = name;
        }

        return relationships.Add(kind == "header" ? HeaderType : FooterType, name["word/".Length..]);
    }

    private static void AddAuxiliary(WordDocument document, WordWriteContext context, PartRelationships relationships, List<(string, XElement, string)> parts)
    {
        if (document.Footnotes.Count > 0)
        {
            AddWrittenPart(parts, relationships, context, "word/footnotes.xml", w => WordPartsWriter.Notes("footnote", document.Footnotes.Values, w), FootnotesType, FootnotesContentType);
        }

        if (document.Endnotes.Count > 0)
        {
            AddWrittenPart(parts, relationships, context, "word/endnotes.xml", w => WordPartsWriter.Notes("endnote", document.Endnotes.Values, w), EndnotesType, EndnotesContentType);
        }

        if (document.Comments.Count > 0)
        {
            AddWrittenPart(parts, relationships, context, "word/comments.xml", w => WordPartsWriter.Comments(document.Comments, w), CommentsType, CommentsContentType);
        }
    }

    // A part with content of its own (pictures, links) gets its own relationships.
    private static void AddWrittenPart(List<(string, XElement, string)> parts, PartRelationships relationships, WordWriteContext context, string name, Func<WordContentWriter, XElement> build, string type, string contentType)
    {
        var partRelationships = new PartRelationships(name);
        AddPart(parts, relationships, name, build(new WordContentWriter(context, partRelationships)), type, contentType);
        AddRelationshipsPart(parts, partRelationships);
    }

    private static void AddPart(List<(string, XElement, string)> parts, PartRelationships relationships, string name, XElement root, string type, string contentType)
    {
        parts.Add((name, root, contentType));
        relationships.Add(type, name["word/".Length..]);
    }

    private static void AddRelationshipsPart(List<(string, XElement, string)> parts, PartRelationships relationships)
    {
        if (relationships.Count > 0)
        {
            parts.Add((OpcRelationships.RelationshipsPath(relationships.PartName), relationships.ToXml(), RelationshipsContentType));
        }
    }

    private static void Package(WordDocument document, WordWriteContext context, PartRelationships relationships, List<(string Name, XElement Root, string ContentType)> parts, Stream stream)
    {
        AddRelationshipsPart(parts, relationships);
        var packageRelationships = new PartRelationships(string.Empty);
        packageRelationships.Add(OfficeDocumentType, DocumentPart);
        if (!document.Information.IsEmpty)
        {
            parts.Add(("docProps/core.xml", WordPartsWriter.Core(document.Information), CoreContentType));
            packageRelationships.Add(CorePropertiesType, "docProps/core.xml");
        }

        var types = new XElement(
            Ct + "Types",
            new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", RelationshipsContentType)),
            new XElement(Ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")));
        foreach (var (extension, contentType) in context.Media.Select(m => (Path.GetExtension(m.Name).TrimStart('.'), m.ContentType)).Distinct())
        {
            types.Add(new XElement(Ct + "Default", new XAttribute("Extension", extension), new XAttribute("ContentType", contentType)));
        }

        foreach (var part in parts.Where(p => p.ContentType != RelationshipsContentType))
        {
            types.Add(new XElement(Ct + "Override", new XAttribute("PartName", "/" + part.Name), new XAttribute("ContentType", part.ContentType)));
        }

        using var package = new OpenXmlPackageWriter(stream);
        package.WriteXml(OpcPackage.ContentTypesPart, types.WriteTo);
        package.WriteXml("_rels/.rels", packageRelationships.ToXml().WriteTo);
        foreach (var part in parts)
        {
            package.WriteXml(part.Name, part.Root.WriteTo);
        }

        foreach (var media in context.Media)
        {
            package.WriteBytes(media.Name, media.Data, compress: media.ContentType is not ("image/jpeg" or "image/png" or "image/gif"));
        }
    }
}
