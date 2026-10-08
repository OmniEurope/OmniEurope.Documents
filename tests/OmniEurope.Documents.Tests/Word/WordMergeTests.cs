// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using static OmniEurope.Documents.Tests.Word.DocxFactory;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>
/// Appending one package to another at XML level (ECMA-376 part 1): comment anchors dropped, parts copied once,
/// unknown ids and missing notes or lists left alone, bookmarks renumbered, sections closed on a paragraph.
/// The packages are written by hand.
/// </summary>
public sealed class WordMergeTests
{
    private const string Rels = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Png = "image/png";

    [Fact]
    public void Appending_copies_shared_parts_once_and_cleans_what_cannot_follow()
    {
        var merged = Merge(Target(endsWithTable: true), Source(), startOnNewPage: false);
        var document = XDocument.Parse(Part(merged, "word/document.xml"));
        var w = (XNamespace)W;
        var body = document.Root!.Element(w + "body")!;

        // Comments are gone with the run that only held the reference.
        Assert.DoesNotContain(body.Descendants(), e => e.Name.LocalName.StartsWith("comment", StringComparison.Ordinal));
        Assert.Single(body.Descendants(w + "p").First(p => p.Value == "commenté").Elements(w + "r"));

        // Both pictures point at one copy of the image; the unknown id is left as it was.
        var imageIds = body.Descendants().Attributes(XName.Get("id", Rels)).Select(a => a.Value).ToList();
        Assert.Contains("rId99", imageIds);
        Assert.Single(Entries(merged).Keys, k => k.StartsWith("word/media/", StringComparison.Ordinal));
        var header = Entries(merged).Keys.Single(k => k.StartsWith("word/header", StringComparison.Ordinal) && k.EndsWith(".xml", StringComparison.Ordinal));
        Assert.Contains("En-tête", Part(merged, header), StringComparison.Ordinal);

        // The table that ended the target is followed by a paragraph carrying its section.
        var table = body.Elements(w + "tbl").Single();
        Assert.NotNull(((XElement)table.NextNode!).Element(w + "pPr")!.Element(w + "sectPr"));

        // Bookmarks are numbered after the target's (4); the appended section continues on the same page.
        Assert.Equal(["4", "5"], body.Descendants(w + "bookmarkStart").Select(b => (string)b.Attribute(w + "id")!));
        Assert.Equal("continuous", (string)body.Element(w + "sectPr")!.Element(w + "type")!.Attribute(w + "val")!);
    }

    [Fact]
    public void Lists_and_notes_that_the_source_lacks_are_left_alone()
    {
        var merged = Merge(Target(endsWithTable: true), Source(), startOnNewPage: true);
        var numbering = XDocument.Parse(Part(merged, "word/numbering.xml")).Root!;
        var w = (XNamespace)W;

        // The valid list gets instance 2 placed before numIdMacAtCleanup; numId 5 has no definition and stays.
        var names = numbering.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.True(names.IndexOf("numIdMacAtCleanup") > names.LastIndexOf("num"));
        var numIds = XDocument.Parse(Part(merged, "word/document.xml")).Descendants(w + "numId").Select(n => (string)n.Attribute(w + "val")!).ToList();
        Assert.Equal(["2", "5"], numIds);
        Assert.Null(XDocument.Parse(Part(merged, "word/document.xml")).Root!.Element(w + "body")!.Element(w + "sectPr")!.Element(w + "type"));

        // Footnote 7 is not in the source's notes part: nothing is copied for it.
        Assert.DoesNotContain(Entries(merged).Keys, k => k == "word/footnotes.xml" && Part(merged, k).Contains("Note absente", StringComparison.Ordinal));
    }

    [Fact]
    public void A_tracked_paragraph_change_keeps_the_section_first()
    {
        var target = Build($"<w:p><w:pPr><w:pPrChange w:id=\"1\" w:author=\"x\"><w:pPr/></w:pPrChange></w:pPr><w:r><w:t>suivi</w:t></w:r></w:p><w:sectPr/>");

        var merged = Merge(target, Build("<w:p><w:r><w:t>ajout</w:t></w:r></w:p>"), startOnNewPage: true);
        var w = (XNamespace)W;
        var pPr = XDocument.Parse(Part(merged, "word/document.xml")).Descendants(w + "pPr").First();

        Assert.Equal(["sectPr", "pPrChange"], pPr.Elements().Select(e => e.Name.LocalName));
        Assert.Contains("ajout", WordDocument.Load(merged).Text, StringComparison.Ordinal);
    }

    private static byte[] Merge(byte[] target, byte[] source, bool startOnNewPage)
    {
        var editor = WordEditor.Open(target);
        editor.Append(WordEditor.Open(source), startOnNewPage);
        return editor.ToArray();
    }

    // Ends with a table (or a paragraph); a bookmark numbered 4; a numbering part with numIdMacAtCleanup.
    private static byte[] Target(bool endsWithTable)
    {
        var numbering = $"<w:numbering xmlns:w=\"{W}\"><w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num><w:numIdMacAtCleanup w:val=\"1\"/></w:numbering>";
        var last = endsWithTable ? "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>cellule</w:t></w:r></w:p></w:tc></w:tr></w:tbl>" : "<w:p/>";
        return Build(
            $"<w:p><w:bookmarkStart w:id=\"4\" w:name=\"cible\"/><w:r><w:t>début</w:t></w:r><w:bookmarkEnd w:id=\"4\"/></w:p>{last}<w:sectPr/>",
            extraParts: new Dictionary<string, string> { ["word/numbering.xml"] = numbering },
            documentRelationships: "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering\" Target=\"numbering.xml\"/>",
            contentTypes: new Dictionary<string, string> { ["word/numbering.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml" });
    }

    // Comments, two pictures of one image, an unknown id, a missing footnote, a valid and a missing list, a
    // bookmark, and a section with only a header reference.
    private static byte[] Source()
    {
        const string vml = "xmlns:v=\"urn:schemas-microsoft-com:vml\"";
        var body = "<w:p><w:commentRangeStart w:id=\"0\"/><w:r><w:t>commenté</w:t></w:r><w:commentRangeEnd w:id=\"0\"/><w:r><w:commentReference w:id=\"0\"/></w:r></w:p>"
            + $"<w:p><w:r><w:pict {vml}><v:imagedata r:id=\"rId1\"/></w:pict></w:r><w:r><w:pict {vml}><v:imagedata r:id=\"rId2\"/></w:pict></w:r><w:hyperlink r:id=\"rId99\"><w:r><w:t>lien</w:t></w:r></w:hyperlink></w:p>"
            + "<w:p><w:r><w:t>note</w:t></w:r><w:r><w:footnoteReference w:id=\"7\"/></w:r></w:p>"
            + "<w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>liste</w:t></w:r></w:p>"
            + "<w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"5\"/></w:numPr></w:pPr><w:r><w:t>sans liste</w:t></w:r></w:p>"
            + "<w:p><w:bookmarkStart w:id=\"0\" w:name=\"source\"/><w:r><w:t>marque</w:t></w:r><w:bookmarkEnd w:id=\"0\"/></w:p>"
            + "<w:sectPr><w:headerReference w:type=\"default\" r:id=\"rId3\"/></w:sectPr>";
        var numbering = $"<w:numbering xmlns:w=\"{W}\"><w:abstractNum w:abstractNumId=\"3\"><w:nsid w:val=\"12345678\"/><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"•\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"3\"/></w:num></w:numbering>";
        var footnotes = $"<w:footnotes xmlns:w=\"{W}\"><w:footnote w:id=\"1\"><w:p><w:r><w:t>Note absente</w:t></w:r></w:p></w:footnote></w:footnotes>";
        var header = $"<w:hdr xmlns:w=\"{W}\"><w:p><w:r><w:t>En-tête</w:t></w:r></w:p></w:hdr>";
        const string type = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        return Build(
            body,
            extraParts: new Dictionary<string, string> { ["word/numbering.xml"] = numbering, ["word/footnotes.xml"] = footnotes, ["word/header1.xml"] = header },
            documentRelationships: $"<Relationship Id=\"rId1\" Type=\"{type}image\" Target=\"media/image1.png\"/><Relationship Id=\"rId2\" Type=\"{type}image\" Target=\"media/image1.png\"/>"
                + $"<Relationship Id=\"rId3\" Type=\"{type}header\" Target=\"header1.xml\"/><Relationship Id=\"rId4\" Type=\"{type}numbering\" Target=\"numbering.xml\"/><Relationship Id=\"rId5\" Type=\"{type}footnotes\" Target=\"footnotes.xml\"/>",
            binaryParts: new Dictionary<string, byte[]> { ["word/media/image1.png"] = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "gray.png")) },
            contentTypes: new Dictionary<string, string>
            {
                ["word/header1.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml",
                ["word/numbering.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml",
                ["word/footnotes.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.footnotes+xml",
            });
    }

    [Fact]
    public void A_copied_header_brings_its_own_styles_and_lists()
    {
        // The header alone uses style "Exclusif" and list 1; the target has a list 1 of its own and no such style.
        const string type = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        const string stylesType = "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml";
        const string numberingType = "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml";
        var styles = $"<w:styles xmlns:w=\"{W}\"><w:style w:type=\"paragraph\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style></w:styles>";
        var sourceStyles = $"<w:styles xmlns:w=\"{W}\"><w:style w:type=\"paragraph\" w:styleId=\"Exclusif\"><w:name w:val=\"Exclusif\"/><w:rPr><w:b/></w:rPr></w:style></w:styles>";
        var numbering = $"<w:numbering xmlns:w=\"{W}\"><w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num></w:numbering>";
        var target = Build(
            "<w:p><w:r><w:t>cible</w:t></w:r></w:p><w:sectPr/>",
            extraParts: new Dictionary<string, string> { ["word/styles.xml"] = styles, ["word/numbering.xml"] = numbering },
            documentRelationships: $"<Relationship Id=\"rId1\" Type=\"{type}styles\" Target=\"styles.xml\"/><Relationship Id=\"rId2\" Type=\"{type}numbering\" Target=\"numbering.xml\"/>",
            contentTypes: new Dictionary<string, string> { ["word/styles.xml"] = stylesType, ["word/numbering.xml"] = numberingType });
        var header = $"<w:hdr xmlns:w=\"{W}\"><w:p><w:pPr><w:pStyle w:val=\"Exclusif\"/><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>En-tête</w:t></w:r></w:p></w:hdr>";
        var source = Build(
            "<w:p><w:r><w:t>ajout</w:t></w:r></w:p><w:sectPr><w:headerReference w:type=\"default\" r:id=\"rId3\"/></w:sectPr>",
            extraParts: new Dictionary<string, string> { ["word/styles.xml"] = sourceStyles, ["word/numbering.xml"] = numbering, ["word/header1.xml"] = header },
            documentRelationships: $"<Relationship Id=\"rId1\" Type=\"{type}styles\" Target=\"styles.xml\"/><Relationship Id=\"rId2\" Type=\"{type}numbering\" Target=\"numbering.xml\"/><Relationship Id=\"rId3\" Type=\"{type}header\" Target=\"header1.xml\"/>",
            contentTypes: new Dictionary<string, string> { ["word/styles.xml"] = stylesType, ["word/numbering.xml"] = numberingType, ["word/header1.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml" });

        var merged = Merge(target, source, startOnNewPage: true);
        var w = (XNamespace)W;

        var copied = XDocument.Parse(Part(merged, Entries(merged).Keys.Single(k => k.StartsWith("word/header", StringComparison.Ordinal) && k.EndsWith(".xml", StringComparison.Ordinal))));
        var mergedStyles = XDocument.Parse(Part(merged, "word/styles.xml"));
        var mergedNumbering = XDocument.Parse(Part(merged, "word/numbering.xml"));
        Assert.Contains(mergedStyles.Root!.Elements(w + "style"), s => (string?)s.Attribute(w + "styleId") == "Exclusif");
        var numId = (string)copied.Descendants(w + "numId").Single().Attribute(w + "val")!;
        Assert.NotEqual("1", numId);
        Assert.Contains(mergedNumbering.Root!.Elements(w + "num"), n => (string?)n.Attribute(w + "numId") == numId);
    }
}
