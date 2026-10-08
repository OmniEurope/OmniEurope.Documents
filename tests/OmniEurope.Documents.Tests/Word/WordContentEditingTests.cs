// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using static OmniEurope.Documents.Tests.Word.DocxFactory;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>Reading a paragraph's runs and rewriting its text with <see cref="WordEditableParagraph.SetContent"/>.</summary>
public sealed class WordContentEditingTests
{
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";

    private const string Translatable =
        "<w:p><w:pPr><w:pStyle w:val=\"Body\"/></w:pPr><w:bookmarkStart w:id=\"0\" w:name=\"start\"/>"
        + "<w:r><w:t xml:space=\"preserve\">Plain, </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>bold</w:t></w:r>"
        + "<w:r><w:t xml:space=\"preserve\"> and </w:t></w:r><w:r><w:rPr><w:i/></w:rPr><w:t>italic</w:t></w:r>"
        + "<w:r><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr><w:footnoteReference w:id=\"1\"/></w:r>"
        + "<w:r><w:t xml:space=\"preserve\"> text.</w:t></w:r><w:bookmarkEnd w:id=\"0\"/></w:p>";

    private const string Field =
        "<w:p><w:r><w:t xml:space=\"preserve\">Page </w:t></w:r><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>"
        + "<w:r><w:instrText xml:space=\"preserve\"> PAGE </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>"
        + "<w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r><w:r><w:t xml:space=\"preserve\"> here</w:t></w:r></w:p>";

    private static WordEditor Open(string body) => WordEditor.Open(Build(body));

    private static WordEditableParagraph First(WordEditor editor) => editor.Paragraphs()[0];

    private static XElement Paragraph(WordEditor editor) =>
        XDocument.Parse(Part(editor.ToArray(), "word/document.xml")).Descendants(XName.Get("p", W)).First();

    [Fact]
    public void Runs_give_kind_text_and_formatting()
    {
        var body = "<w:p><w:r><w:rPr><w:b/><w:sz w:val=\"28\"/><w:color w:val=\"FF0000\"/></w:rPr><w:t>a</w:t><w:tab/><w:t>b</w:t><w:br/><w:cr/><w:noBreakHyphen/><w:softHyphen/><w:lastRenderedPageBreak/></w:r>"
            + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText>PAGE</w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>"
            + "<w:fldSimple w:instr=\"NUMPAGES\"><w:r><w:t>2</w:t></w:r></w:fldSimple>"
            + "<w:r><w:footnoteReference w:id=\"1\"/></w:r><w:r><w:endnoteReference w:id=\"2\"/></w:r>"
            + "<w:r><w:drawing/></w:r><w:r><w:pict/></w:r><w:r><w:object/></w:r><w:r><w:sym w:font=\"Symbol\" w:char=\"F0B7\"/></w:r>"
            + "<w:r><w:br w:type=\"page\"/></w:r><w:r><w:br w:type=\"column\"/></w:r><w:r><w:commentReference w:id=\"0\"/></w:r><w:r><w:rPr><w:i/></w:rPr></w:r>"
            + "<w:r><mc:AlternateContent><mc:Choice Requires=\"wps\"><w:drawing/></mc:Choice><mc:Fallback><w:pict/></mc:Fallback></mc:AlternateContent></w:r>"
            + "<w:r><mc:AlternateContent><mc:Choice Requires=\"x\"><w:t>?</w:t></mc:Choice></mc:AlternateContent></w:r>"
            + "<w:r><x:foreign xmlns:x=\"urn:x\"/></w:r>"
            + "<w:del w:id=\"1\" w:author=\"A\"><w:r><w:delText>gone</w:delText></w:r></w:del><w:moveFrom w:id=\"2\" w:author=\"A\"><w:r><w:t>moved</w:t></w:r></w:moveFrom>"
            + "<w:r><w:t>end</w:t></w:r></w:p>";

        var runs = First(Open(body)).Runs();

        Assert.Equal(
            [
                WordRunKind.Text, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.FootnoteReference,
                WordRunKind.EndnoteReference, WordRunKind.Drawing, WordRunKind.Drawing, WordRunKind.Drawing, WordRunKind.Symbol, WordRunKind.PageBreak,
                WordRunKind.PageBreak, WordRunKind.Other, WordRunKind.Other, WordRunKind.Drawing, WordRunKind.Other, WordRunKind.Other, WordRunKind.Text,
            ],
            runs.Select(r => r.Kind));
        Assert.Equal(Enumerable.Range(0, runs.Count), runs.Select(r => r.Index));
        Assert.Equal("a\tb\n\n‑­", runs[0].Text);
        Assert.All(runs.Skip(1).SkipLast(1), r => Assert.Equal(string.Empty, r.Text));
        Assert.Equal([null, null, null, null, null, null, null, null, null, null, null, null, null, "commentReference", null, null, "AlternateContent", "foreign", null], runs.Select(r => r.ElementName));
        Assert.True(runs[0].Properties.Bold);
        Assert.Equal(14, runs[0].Properties.FontSize);
        Assert.Equal("FF0000", runs[0].Properties.Color);
        Assert.True(runs[14].Properties.Italic);
        Assert.Same(WordRunProperties.Empty, runs[1].Properties);
    }

    [Fact]
    public void Translation_keeps_the_footnote_reference_after_the_word_it_follows()
    {
        var editor = Open(Translatable);
        var paragraph = First(editor);
        Assert.Equal(WordRunKind.FootnoteReference, paragraph.Runs()[4].Kind);

        paragraph.SetContent(
            [
                new WordTextPiece("Simple, "), new WordTextPiece("gras", new WordRunProperties { Bold = true }), new WordTextPiece(" et "),
                new WordTextPiece("italique", new WordRunProperties { Italic = true }), new WordKeptRun(4), new WordTextPiece(" texte."),
            ],
            "fr-FR");

        var runs = WordEditor.Open(editor.ToArray()).Paragraphs()[0].Runs();
        Assert.Equal([WordRunKind.Text, WordRunKind.Text, WordRunKind.Text, WordRunKind.Text, WordRunKind.FootnoteReference, WordRunKind.Text], runs.Select(r => r.Kind));
        Assert.Equal(["Simple, ", "gras", " et ", "italique", "", " texte."], runs.Select(r => r.Text));
        Assert.True(runs[1].Properties.Bold);
        Assert.Null(runs[1].Properties.Italic);
        Assert.True(runs[3].Properties.Italic);
        Assert.Null(runs[0].Properties.Bold);
        Assert.Equal(WordVerticalPosition.Superscript, runs[4].Properties.VerticalPosition);
        Assert.All(runs.Where(r => r.Kind == WordRunKind.Text), r => Assert.Equal("fr-FR", r.Properties.Language));
        Assert.Null(runs[4].Properties.Language);
        var xml = Paragraph(editor);
        Assert.Equal(["pPr", "bookmarkStart", "r", "r", "r", "r", "r", "r", "bookmarkEnd"], xml.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("Body", First(editor).StyleId);
        Assert.Equal(["word/document.xml"], editor.ChangedParts);
    }

    [Fact]
    public void A_field_is_kept_whole_where_the_pieces_put_it()
    {
        var editor = Open(Field);
        var paragraph = First(editor);
        Assert.Equal([WordRunKind.Text, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.Text], paragraph.Runs().Select(r => r.Kind));

        paragraph.SetContent([new WordTextPiece("Seite "), new WordKeptRun(4), new WordKeptRun(2), new WordTextPiece(" hier")]);

        var reread = First(WordEditor.Open(editor.ToArray()));
        Assert.Equal("Seite 1 hier", reread.Text);
        Assert.Equal([WordRunKind.Text, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.Field, WordRunKind.Text], reread.Runs().Select(r => r.Kind));
        Assert.Equal(" PAGE ", Paragraph(editor).Descendants(XName.Get("instrText", W)).Single().Value);
    }

    [Fact]
    public void Runs_the_pieces_do_not_name_follow_the_new_text()
    {
        var editor = Open("<w:p><w:r><w:drawing/></w:r><w:r><w:t>Caption</w:t></w:r><w:r><w:br w:type=\"page\"/></w:r><w:r><w:t>more</w:t></w:r></w:p>");
        var paragraph = First(editor);

        paragraph.SetContent([new WordTextPiece("Légende")]);

        Assert.Equal([WordRunKind.Text, WordRunKind.Drawing, WordRunKind.PageBreak], paragraph.Runs().Select(r => r.Kind));
        Assert.Equal("Légende", paragraph.Runs()[0].Text);
    }

    [Fact]
    public void Tracked_deletions_are_left_alone()
    {
        const string deletion = "<w:del w:id=\"1\" w:author=\"A\"><w:r><w:delText xml:space=\"preserve\">gone </w:delText></w:r></w:del>";
        var editor = Open("<w:p><w:r><w:t xml:space=\"preserve\">Keep </w:t></w:r>" + deletion + "<w:r><w:t>this</w:t></w:r></w:p>");
        var paragraph = First(editor);

        paragraph.SetContent([new WordTextPiece("Garde ceci")]);

        var xml = Paragraph(editor);
        Assert.Equal(["r", "del"], xml.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("gone ", xml.Element(XName.Get("del", W))!.Value);
        Assert.Equal(["Garde ceci"], paragraph.Runs().Select(r => r.Text));
    }

    [Fact]
    public void Kept_runs_move_with_the_wrappers_that_hold_only_them()
    {
        var body = "<w:p><w:hyperlink r:id=\"rId1\"><w:r><w:t>Link text</w:t></w:r></w:hyperlink><w:r><w:t xml:space=\"preserve\"> and </w:t></w:r>"
            + "<w:hyperlink w:anchor=\"x\"><w:r><w:drawing/></w:r></w:hyperlink><w:fldSimple w:instr=\" NUMPAGES \"><w:r><w:t>3</w:t></w:r></w:fldSimple>"
            + "<w:ins w:id=\"2\" w:author=\"A\"><w:r><w:t>new</w:t></w:r></w:ins><w:commentRangeStart w:id=\"5\"/></w:p>";
        var editor = Open(body);
        var paragraph = First(editor);
        Assert.Equal([WordRunKind.Text, WordRunKind.Text, WordRunKind.Drawing, WordRunKind.Field, WordRunKind.Text], paragraph.Runs().Select(r => r.Kind));

        paragraph.SetContent([new WordKeptRun(3), new WordTextPiece("Lien"), new WordKeptRun(2)]);

        var xml = Paragraph(editor);
        Assert.Equal(["fldSimple", "r", "hyperlink", "commentRangeStart"], xml.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("x", xml.Element(XName.Get("hyperlink", W))!.Attribute(XName.Get("anchor", W))!.Value);
    }

    [Fact]
    public void Text_in_a_content_control_stays_in_it_and_a_kept_text_run_is_kept()
    {
        var editor = Open("<w:p><w:sdt><w:sdtPr><w:alias w:val=\"Name\"/></w:sdtPr><w:sdtContent><w:r><w:rPr><w:rFonts w:ascii=\"Courier New\"/></w:rPr><w:t>Code</w:t></w:r>"
            + "<w:r><w:t xml:space=\"preserve\"> is fine</w:t></w:r></w:sdtContent></w:sdt></w:p>");
        var paragraph = First(editor);

        paragraph.SetContent([new WordTextPiece("Le "), new WordKeptRun(0), new WordTextPiece(" va bien")]);

        var xml = Paragraph(editor);
        Assert.Equal(["sdt"], xml.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("Le Code va bien", xml.Value);
        Assert.All(paragraph.Runs(), r => Assert.Equal("Courier New", r.Properties.Font));
    }

    [Fact]
    public void An_empty_paragraph_takes_the_text_in_the_paragraph_mark_format()
    {
        var editor = Open("<w:p><w:pPr><w:rPr><w:ins w:id=\"1\" w:author=\"A\"/><w:b/></w:rPr></w:pPr></w:p>");
        var paragraph = First(editor);

        paragraph.SetContent([new WordTextPiece("Titre\tA\nB"), new WordTextPiece(string.Empty)], "fr-BE");

        var run = Assert.Single(paragraph.Runs());
        Assert.Equal("Titre\tA\nB", run.Text);
        Assert.True(run.Properties.Bold);
        Assert.Equal("fr-BE", run.Properties.Language);
        Assert.Equal(["rPr", "t", "tab", "t", "br", "t"], Paragraph(editor).Element(XName.Get("r", W))!.Elements().Select(e => e.Name.LocalName));
        Assert.DoesNotContain("w:ins", Paragraph(editor).Element(XName.Get("r", W))!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_all_content_leaves_no_empty_wrapper()
    {
        var editor = Open("<w:p><w:ins w:id=\"1\" w:author=\"A\"><w:hyperlink w:anchor=\"a\"><w:r><w:t>x</w:t></w:r></w:hyperlink></w:ins><w:bookmarkStart w:id=\"0\" w:name=\"b\"/></w:p>");

        First(editor).SetContent([]);

        Assert.Equal(["bookmarkStart"], Paragraph(editor).Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void Content_already_there_changes_nothing()
    {
        var original = Build(Translatable + Field + "<w:p><w:r><w:drawing/></w:r><w:r><w:t>Caption</w:t></w:r></w:p>");
        var editor = WordEditor.Open(original);
        var paragraphs = editor.Paragraphs();
        _ = paragraphs.Select(p => (p.Location, p.Runs(), p.ResolveProperties(), p.ResolveRunProperties())).ToList();

        paragraphs[0].SetContent([new WordTextPiece("Plain, "), new WordTextPiece("bold and "), new WordTextPiece("italic"), new WordKeptRun(4), new WordTextPiece(" text.")]);
        paragraphs[1].SetContent([new WordTextPiece("Page "), new WordKeptRun(1), new WordKeptRun(5), new WordTextPiece(" here")]);
        paragraphs[2].SetContent([new WordKeptRun(0), new WordTextPiece("Cap"), new WordTextPiece(string.Empty), new WordTextPiece("tion")]);

        Assert.Empty(editor.ChangedParts);
        var before = Entries(original);
        var after = Entries(editor.ToArray());
        Assert.Equal(before.Keys, after.Keys);
        Assert.All(before, entry => Assert.Equal(entry.Value, after[entry.Key]));
    }

    [Fact]
    public void Any_format_or_language_rewrites_even_the_same_text()
    {
        var editor = Open(Field);

        First(editor).SetContent([new WordTextPiece("Page "), new WordKeptRun(1), new WordTextPiece(" here", new WordRunProperties { Bold = true })]);

        Assert.True(First(editor).Runs()[6].Properties.Bold);
        Assert.Equal(["word/document.xml"], editor.ChangedParts);
    }

    [Fact]
    public void Wrong_pieces_are_refused()
    {
        var paragraph = First(Open(Field));

        Assert.Throws<ArgumentNullException>(() => paragraph.SetContent(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.SetContent([new WordKeptRun(7)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.SetContent([new WordKeptRun(-1)]));
        Assert.Throws<ArgumentException>(() => paragraph.SetContent([null!]));
        Assert.Throws<ArgumentNullException>(() => paragraph.SetContent([new WordTextPiece(null!)]));
    }

    [Fact]
    public void Paragraph_and_run_formatting_resolve_through_the_style_sheet()
    {
        var styles = $"<w:styles xmlns:w=\"{W}\"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:asciiTheme=\"minorHAnsi\" w:hAnsiTheme=\"minorHAnsi\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>"
            + "<w:pPrDefault><w:pPr><w:spacing w:after=\"160\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:basedOn w:val=\"Normal\"/><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"240\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"32\"/></w:rPr></w:style>"
            + "<w:style w:type=\"character\" w:styleId=\"Emphasis\"><w:rPr><w:i/></w:rPr></w:style>"
            + "<w:style w:type=\"table\" w:styleId=\"Grid\"><w:rPr><w:color w:val=\"FF0000\"/></w:rPr></w:style></w:styles>";
        var theme = "<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:themeElements><a:fontScheme><a:majorFont><a:latin typeface=\"Cambria\"/></a:majorFont>"
            + "<a:minorFont><a:latin typeface=\"Calibri\"/></a:minorFont></a:fontScheme></a:themeElements></a:theme>";
        var body = "<w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:rPr><w:rStyle w:val=\"Emphasis\"/></w:rPr><w:t>Title</w:t></w:r></w:p>"
            + "<w:tbl><w:tblPr><w:tblStyle w:val=\"Grid\"/></w:tblPr><w:tr><w:tc><w:p><w:r><w:t>Cell</w:t></w:r><w:r><w:pict><w:txbxContent><w:p><w:r><w:t>Box</w:t></w:r></w:p></w:txbxContent></w:pict></w:r></w:p></w:tc></w:tr></w:tbl>";
        var editor = WordEditor.Open(Build(body, extraParts: new Dictionary<string, string> { ["word/styles.xml"] = styles, ["word/theme/theme1.xml"] = theme },
            documentRelationships: $"<Relationship Id=\"rId1\" Type=\"{Rel}styles\" Target=\"styles.xml\"/><Relationship Id=\"rId2\" Type=\"{Rel}theme\" Target=\"theme/theme1.xml\"/>"));
        var paragraphs = editor.Paragraphs();

        var heading = paragraphs[0].ResolveProperties();
        var headingRun = paragraphs[0].ResolveRunProperties();
        var title = paragraphs[0].Runs()[0].ResolvedProperties;

        Assert.Equal(WordAlignment.Center, heading.Alignment);
        Assert.Equal(12, heading.SpacingBefore);
        Assert.Equal(8, heading.SpacingAfter);
        Assert.Equal("Heading1", heading.StyleId);
        Assert.True(headingRun.Bold);
        Assert.Equal(16, headingRun.FontSize);
        Assert.Equal("Calibri", headingRun.Font);
        Assert.Null(headingRun.Italic);
        Assert.True(title.Italic);
        Assert.True(title.Bold);
        Assert.Equal("Normal", paragraphs[1].ResolveProperties().StyleId);
        Assert.Equal("FF0000", paragraphs[1].ResolveRunProperties().Color);
        Assert.Equal("FF0000", paragraphs[1].Runs()[0].ResolvedProperties.Color);
        Assert.Null(paragraphs[2].ResolveRunProperties().Color);
        Assert.Equal(11, paragraphs[2].ResolveRunProperties().FontSize);
        Assert.Empty(editor.ChangedParts);
    }
}
