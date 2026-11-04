// SPDX-License-Identifier: EUPL-1.2
using System.Text.RegularExpressions;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Tests.Word;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class WordHtmlContentControlTests
{
    private static readonly WordHtmlOptions Marked = new() { MarkContentControls = true };

    [Fact]
    public void Off_by_default_the_output_equals_the_document_without_controls()
    {
        var withControls = WordDocument.Load(Build(controls: true));
        var without = WordDocument.Load(Build(controls: false));
        var revisions = new WordHtmlOptions { Revisions = WordHtmlRevisions.Marked };

        Assert.Equal(WordToHtml.Convert(without).Html, WordToHtml.Convert(withControls).Html);
        Assert.Equal(WordToHtml.Convert(without, revisions).Html, WordToHtml.Convert(withControls, revisions).Html);
        Assert.DoesNotContain("data-oe-", WordToHtml.Convert(withControls).Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Marking_controls_keeps_the_text_and_the_paragraphs()
    {
        var marked = HtmlParser.ParseDocument(WordToHtml.Convert(Build(controls: true), Marked).Html);
        var plain = HtmlParser.ParseDocument(WordToHtml.Convert(Build(controls: false)).Html);

        Assert.Equal(plain.QuerySelectorAll("p").Count, marked.QuerySelectorAll("p").Count);
        Assert.Equal(plain.QuerySelectorAll("table").Count, marked.QuerySelectorAll("table").Count);
        Assert.Equal(Regex.Replace(plain.Body!.TextContent, @"\s+", " "), Regex.Replace(marked.Body!.TextContent, @"\s+", " "));
    }

    [Fact]
    public void An_inline_control_becomes_a_span_with_tag_kind_alias_and_placeholder()
    {
        var page = HtmlParser.ParseDocument(WordToHtml.Convert(Build(controls: true), Marked).Html);

        var date = Assert.Single(page.QuerySelectorAll("span[data-oe-field=debut]"));
        Assert.Equal("date", date.GetAttribute("data-oe-field-kind"));
        Assert.Equal("Début \"<x>\"", date.GetAttribute("data-oe-field-alias"));
        Assert.Null(date.GetAttribute("data-oe-placeholder"));
        Assert.Contains("01/01/2026", date.TextContent, StringComparison.Ordinal);

        var placeholder = Assert.Single(page.QuerySelectorAll("span[data-oe-field=paiement]"));
        Assert.Equal("true", placeholder.GetAttribute("data-oe-placeholder"));
        Assert.Equal("combobox", Assert.Single(page.QuerySelectorAll("span[data-oe-field=mois]")).GetAttribute("data-oe-field-kind"));
        Assert.Equal("checkbox", Assert.Single(page.QuerySelectorAll("span[data-oe-field=recu]")).GetAttribute("data-oe-field-kind"));
        Assert.Equal("text", Assert.Single(page.QuerySelectorAll("span[data-oe-field=nom]")).GetAttribute("data-oe-field-kind"));
    }

    [Fact]
    public void A_control_without_tag_carries_its_id_and_one_without_type_is_rich_text()
    {
        var page = HtmlParser.ParseDocument(WordToHtml.Convert(Build(controls: true), Marked).Html);

        var anonymous = Assert.Single(page.QuerySelectorAll("span[data-oe-field-id='77']"));
        Assert.Null(anonymous.GetAttribute("data-oe-field"));
        Assert.Equal("richtext", anonymous.GetAttribute("data-oe-field-kind"));
    }

    [Fact]
    public void A_block_control_wraps_its_paragraphs_and_tables_in_a_div()
    {
        var page = HtmlParser.ParseDocument(WordToHtml.Convert(Build(controls: true), Marked).Html);

        var block = Assert.Single(page.QuerySelectorAll("div[data-oe-field=bloc]"));
        Assert.Equal("richtext", block.GetAttribute("data-oe-field-kind"));
        Assert.Equal(2, block.QuerySelectorAll("p").Count);
        Assert.Single(block.QuerySelectorAll("table"));
        Assert.Contains("Avant le tableau", block.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Apres le bloc", block.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_controls_nest_their_wrappers_and_a_cell_control_stays_in_its_cell()
    {
        var page = HtmlParser.ParseDocument(WordToHtml.Convert(Build(controls: true), Marked).Html);

        var outer = Assert.Single(page.QuerySelectorAll("span[data-oe-field=externe]"));
        var inner = Assert.Single(outer.QuerySelectorAll("span[data-oe-field=interne]"));
        Assert.Equal("dropdownlist", inner.GetAttribute("data-oe-field-kind"));
        Assert.Contains("avant", outer.TextContent, StringComparison.Ordinal);
        Assert.Contains("apres", outer.TextContent, StringComparison.Ordinal);

        var cell = Assert.Single(page.QuerySelectorAll("td span[data-oe-field=cellule]"));
        Assert.Equal("text", cell.GetAttribute("data-oe-field-kind"));
        Assert.Single(Assert.Single(page.QuerySelectorAll("div[data-oe-field=bloc]")).QuerySelectorAll("span[data-oe-field=cellule]"));
    }

    [Fact]
    public void Pdf_markdown_and_text_are_unchanged_by_controls()
    {
        var withControls = Build(controls: true);
        var without = Build(controls: false);

        Assert.Equal(WordToMarkdown.Convert(without).Markdown, WordToMarkdown.Convert(withControls).Markdown);
        var pdfWith = WordToPdf.Convert(withControls);
        var pdfWithout = WordToPdf.Convert(without);
        Assert.Equal(pdfWithout.PageCount, pdfWith.PageCount);
        Assert.Equal(pdfWithout.Pdf.Length, pdfWith.Pdf.Length);
        Assert.Equal(WordDocument.Load(without).Text, WordDocument.Load(withControls).Text);
    }

    [Fact]
    public void Saving_does_not_write_marks_and_loses_the_same_as_before()
    {
        var saved = WordDocument.Load(WordDocument.Load(Build(controls: true)).ToArray());
        var savedWithout = WordDocument.Load(WordDocument.Load(Build(controls: false)).ToArray());

        Assert.Equal(savedWithout.Text, saved.Text);
        Assert.Equal(WordToHtml.Convert(savedWithout).Html, WordToHtml.Convert(saved).Html);
        Assert.DoesNotContain("data-oe-", WordToHtml.Convert(saved, Marked).Html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fixture_controls_are_marked_and_the_default_output_has_none()
    {
        var docx = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.docx"));
        var marked = WordToHtml.Convert(docx, Marked).Html;
        var plain = WordToHtml.Convert(docx).Html;

        var fields = Regex.Matches(marked, "<(span|div) data-oe-field[^>]*>").Select(m => m.Value).ToList();
        Assert.Equal(6, fields.Count);
        Assert.DoesNotContain("data-oe-", plain, StringComparison.Ordinal);
        Assert.Equal(6, fields.Count(f => f.Contains("data-oe-field-kind=", StringComparison.Ordinal)));
        Assert.Equal(["mois", "annee", "debut", "fin", "paiement", "signature"], fields.Select(f => Regex.Match(f, "data-oe-field=\"([^\"]*)\"").Groups[1].Value));
    }

    private static byte[] Build(bool controls)
    {
        string Sdt(string properties, string content) =>
            controls ? $"<w:sdt><w:sdtPr>{properties}</w:sdtPr><w:sdtContent>{content}</w:sdtContent></w:sdt>" : content;
        string Run(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";
        const string W14 = "http://schemas.microsoft.com/office/word/2010/wordml";
        var body = $"""
            <w:p>{Run("Du ")}{Sdt("""<w:alias w:val="Début &quot;&lt;x&gt;&quot;"/><w:tag w:val="debut"/><w:id w:val="1"/><w:date><w:dateFormat w:val="dd/MM/yyyy"/></w:date>""", Run("01/01/2026"))}{Run(" mois ")}{Sdt("""<w:tag w:val="mois"/><w:comboBox><w:listItem w:displayText="a" w:value="1"/></w:comboBox>""", Run("a"))}</w:p>
            <w:p>{Sdt("""<w:tag w:val="paiement"/><w:showingPlcHdr/><w:date/>""", Run("Choisissez"))}{Sdt($"""<w:tag w:val="recu"/><w14:checkbox xmlns:w14="{W14}"><w14:checked w14:val="1"/></w14:checkbox>""", Run("X"))}{Sdt("""<w:tag w:val="nom"/><w:text/>""", Run("Durand"))}{Sdt("""<w:id w:val="77"/>""", Run("sans etiquette"))}</w:p>
            <w:p>{Sdt("""<w:tag w:val="externe"/><w:richText/>""", Run("avant ") + Sdt("""<w:tag w:val="interne"/><w:dropDownList><w:listItem w:displayText="b" w:value="2"/></w:dropDownList>""", Run("b")) + Run(" apres"))}</w:p>
            {Sdt("""<w:tag w:val="bloc"/><w:alias w:val="Bloc"/>""", $"<w:p>{Run("Avant le tableau")}</w:p>" + Table(Sdt))}
            <w:p>{Run("Apres le bloc")}</w:p>
            """;
        return DocxFactory.Build(body);
    }

    private static string Table(Func<string, string, string> sdt) =>
        $"""<w:tbl><w:tblPr/><w:tblGrid><w:gridCol w:w="4000"/></w:tblGrid><w:tr><w:tc><w:p>{sdt("""<w:tag w:val="cellule"/><w:text/>""", "<w:r><w:t>dans la cellule</w:t></w:r>")}</w:p></w:tc></w:tr></w:tbl>""";
}
