// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class WordToHtmlTests
{
    [Fact]
    public void Writes_a_standalone_page_at_the_section_width_with_running_header_and_footer()
    {
        var document = new WordDocument { Information = new WordInformation { Title = "Rapport annuel" } };
        var section = document.Sections[0];
        section.Page = WordPageSetup.A4 with { MarginLeft = 50, MarginRight = 40, MarginTop = 60, MarginBottom = 30, HeaderDistance = 20, FooterDistance = 10 };
        section.Headers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("En-tête ", "Header").Add(WordField.Page()));
        section.Footers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("Pied de page", "Footer"));
        document.AddParagraph("Corps");

        var result = WordToHtml.Convert(document);
        var page = HtmlParser.ParseDocument(result.Html);

        Assert.StartsWith("<!DOCTYPE html>\n<html lang=\"fr-FR\">", result.Html, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", result.Html, StringComparison.Ordinal);
        Assert.Equal("Rapport annuel", page.Title);
        Assert.NotNull(page.Head.QuerySelector("style"));
        var header = page.Body.QuerySelector("header.omni-header")!;
        var body = page.Body.QuerySelector("div.omni-section")!;
        var footer = page.Body.QuerySelector("footer.omni-footer")!;
        Assert.Equal("En-tête 1", header.TextContent.Trim());
        Assert.Equal("Pied de page", footer.TextContent.Trim());
        Assert.Equal("width:595.3pt;padding:20pt 40pt 0pt 50pt", header.GetAttribute("style"));
        Assert.Equal("width:595.3pt;padding:40pt 40pt 20pt 50pt", body.GetAttribute("style"));
        Assert.Equal("width:595.3pt;padding:0pt 40pt 10pt 50pt", footer.GetAttribute("style"));
        Assert.Contains("page number fields show their last computed value", result.Gaps);
        Assert.Contains("line and page breaks are computed by the browser", result.Gaps);
        Assert.True(result.Html.IndexOf("omni-header", StringComparison.Ordinal) < result.Html.IndexOf("Corps", StringComparison.Ordinal));
    }

    [Fact]
    public void Paragraphs_carry_alignment_indents_spacing_line_spacing_and_page_breaks()
    {
        var document = new WordDocument();
        document.AddHeading("Titre", 2);
        document.Body.Add(new WordParagraph("Centré")
        {
            Properties = new WordParagraphProperties
            {
                Alignment = WordAlignment.Center,
                IndentLeft = 36,
                IndentRight = 18,
                FirstLineIndent = -18,
                SpacingBefore = 6,
                SpacingAfter = 12,
                LineSpacing = 18,
                LineSpacingRule = WordLineSpacingRule.Exact,
                Shading = "EEEEEE",
                Borders = new WordParagraphBorders(Top: new WordBorder("double", 1, 0, "FF0000")),
            },
        });
        document.Body.Add(new WordParagraph("Justifié") { Properties = new WordParagraphProperties { Alignment = WordAlignment.Justify, LineSpacing = 2, LineSpacingRule = WordLineSpacingRule.Multiple } });
        document.Body.Add(new WordParagraph("Droite") { Properties = new WordParagraphProperties { Alignment = WordAlignment.Right, LineSpacing = 14, LineSpacingRule = WordLineSpacingRule.AtLeast, PageBreakBefore = true } });
        document.AddParagraph("Avant").Add(new WordBreak(WordBreakKind.Page)).AddText("Après");

        var page = Parse(WordToHtml.Convert(document).Html);
        var paragraphs = page.Body.QuerySelectorAll(".omni-section > *");

        Assert.Equal("h2", paragraphs[0].TagName);
        Assert.Equal("Titre", paragraphs[0].TextContent);
        Assert.Equal(
            "text-align:center;margin-left:36pt;margin-right:18pt;text-indent:-18pt;line-height:18pt;background-color:#EEEEEE;border-top:1pt double #FF0000;margin-top:6pt;margin-bottom:12pt;font-family:'Calibri',sans-serif;font-size:11pt",
            paragraphs[1].GetAttribute("style"));
        Assert.Contains("text-align:justify;line-height:2.3;", paragraphs[2].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("line-height:max(14pt,1.15em);break-before:page", paragraphs[3].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("p", paragraphs[4].TagName);
        Assert.NotNull(paragraphs[4].QuerySelector("span.omni-page-break"));
        Assert.Equal("AvantAprès", paragraphs[4].TextContent);
    }

    [Fact]
    public void Runs_show_their_formatting_resolved_through_the_style_sheet()
    {
        var document = new WordDocument();
        document.Styles.Add(new WordStyle("Accent", WordStyleType.Character) { RunProperties = new WordRunProperties { Italic = true, Color = "00AA00" } });
        var paragraph = document.AddParagraph("normal ");
        paragraph.AddText("gras", new WordRunProperties { Bold = true })
            .AddText("souligné", new WordRunProperties { Underline = WordUnderline.Double })
            .AddText("barré", new WordRunProperties { Strike = true })
            .AddText("exposant", new WordRunProperties { VerticalPosition = WordVerticalPosition.Superscript })
            .AddText("indice", new WordRunProperties { VerticalPosition = WordVerticalPosition.Subscript })
            .AddText("grand", new WordRunProperties { FontSize = 14, Color = "FF0000", Highlight = "yellow" })
            .AddText("styleé", new WordRunProperties { StyleId = "Accent" })
            .AddText("capitales", new WordRunProperties { Caps = true, CharacterSpacing = 1, Font = "Arial" })
            .AddText("caché", new WordRunProperties { Hidden = true });
        document.AddHeading("Titre stylé");

        var html = WordToHtml.Convert(document).Html;

        Assert.Contains(">normal <strong>gras</strong><u style=\"text-decoration-style:double\">souligné</u><s>barré</s><sup>exposant</sup><sub>indice</sub>", html, StringComparison.Ordinal);
        Assert.Contains("<span style=\"font-size:14pt;color:#FF0000;background-color:#FFFF00\">grand</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span style=\"color:#00AA00\"><em>styleé</em></span>", html, StringComparison.Ordinal);
        Assert.Contains("<span style=\"font-family:'Arial',sans-serif;letter-spacing:1pt;text-transform:uppercase\">capitales</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("caché", html, StringComparison.Ordinal);
        var heading = Parse(html).Body.QuerySelector("h1")!;
        Assert.Contains("font-size:16pt;color:#1F3864", heading.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("<strong>Titre stylé</strong>", heading.InnerHtml);
    }

    [Fact]
    public void Tables_keep_spans_vertical_merges_shading_and_widths()
    {
        var document = new WordDocument();
        var table = new WordTable(100, 150, 200) { Properties = new WordTableProperties { StyleId = "TableGrid", Alignment = WordAlignment.Center } };
        var top = table.AddRow("Large", "Seule");
        top.Cells[0].Properties = new WordTableCellProperties { GridSpan = 2, Shading = "D9E2F3" };
        var first = table.AddRow("Fusion", "B2", "C2");
        first.Cells[0].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Restart, VerticalAlignment = WordCellAlignment.Center };
        var second = table.AddRow(string.Empty, "B3", "C3");
        second.Cells[0].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Continue };
        var shifted = table.AddRow("Décalée");
        shifted.Properties = new WordTableRowProperties { GridBefore = 1, GridAfter = 1, Height = 30 };
        document.AddTable(table);

        var element = Parse(WordToHtml.Convert(document).Html).Body.QuerySelector("table.omni-table")!;
        var rows = element.QuerySelectorAll("tr");

        Assert.Contains("width:450pt", element.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("margin-left:auto;margin-right:auto", element.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(3, element.QuerySelectorAll("col").Count);
        var wide = rows[0].QuerySelectorAll("td")[0];
        Assert.Equal("2", wide.GetAttribute("colspan"));
        Assert.Contains("width:250pt", wide.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("background-color:#D9E2F3", wide.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("border-top:0.5pt solid #000000", wide.GetAttribute("style"), StringComparison.Ordinal);
        var merged = rows[1].QuerySelectorAll("td")[0];
        Assert.Equal("2", merged.GetAttribute("rowspan"));
        Assert.Contains("vertical-align:middle", merged.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(["B3", "C3"], rows[2].QuerySelectorAll("td").Select(c => c.TextContent.Trim()));
        Assert.Equal("height:30pt", rows[3].GetAttribute("style"));
        Assert.Equal(["", "Décalée", ""], rows[3].QuerySelectorAll("td").Select(c => c.TextContent.Trim()));
        Assert.Equal("omni-grid-skip", rows[3].QuerySelectorAll("td")[0].GetAttribute("class"));
    }

    [Fact]
    public void Table_styles_format_their_header_row_and_percent_widths_stay_relative()
    {
        var document = new WordDocument();
        document.Styles.Add(new WordStyle("Bandes", WordStyleType.Table)
        {
            BasedOn = "TableNormal",
            CellProperties = new WordTableCellProperties { Margins = new WordCellMargins(2, 4, 2, 4) },
            ConditionalFormats = [new WordTableConditionalFormat(WordTableRegion.FirstRow, RunProperties: new WordRunProperties { Bold = true }, CellProperties: new WordTableCellProperties { Shading = "112233" })],
        });
        var table = new WordTable { Properties = new WordTableProperties { StyleId = "Bandes", Width = WordWidth.Percent(80), FixedLayout = true, Indent = 12, Shading = "FAFAFA" } };
        table.AddRow("Entête", "Deux");
        table.AddRow("Valeur", "Autre");
        document.AddTable(table);

        var element = Parse(WordToHtml.Convert(document).Html).Body.QuerySelector("table")!;
        var cells = element.QuerySelectorAll("td");

        Assert.Equal("width:80%;table-layout:fixed;margin-left:12pt;background-color:#FAFAFA", element.GetAttribute("style"));
        Assert.Contains("background-color:#112233", cells[0].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("padding:2pt 4pt 2pt 4pt", cells[0].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("<strong>Entête</strong>", cells[0].QuerySelector("p")!.InnerHtml);
        Assert.Equal("Valeur", cells[2].QuerySelector("p")!.InnerHtml);
        Assert.Null(element.QuerySelector("colgroup"));
    }

    [Fact]
    public void Lists_show_their_computed_labels_and_levels()
    {
        var document = new WordDocument();
        var numbered = document.Numbering.AddNumberedList();
        var bullets = document.Numbering.AddBulletList();
        document.AddParagraph("Un").AsListItem(numbered);
        document.AddParagraph("Un a").AsListItem(numbered, 1);
        document.AddParagraph("Un b").AsListItem(numbered, 1);
        document.AddParagraph("Deux").AsListItem(numbered);
        document.AddParagraph("Puce").AsListItem(bullets);
        document.Numbering.Abstracts[document.Numbering.Instances[bullets].AbstractId].Levels[1] = new WordNumberingLevel(1) { Format = WordNumberFormat.Bullet, Text = "", Suffix = WordLabelSuffix.Space, RunProperties = new WordRunProperties { Font = "Symbol" } };
        document.AddParagraph("Symbole").AsListItem(bullets, 1);

        var paragraphs = Parse(WordToHtml.Convert(document).Html).Body.QuerySelectorAll(".omni-section > p");

        Assert.Equal(["1.", "a.", "b.", "2.", "•", "•"], paragraphs.Select(p => p.QuerySelector(".omni-label")!.TextContent));
        Assert.Equal("display:inline-block;min-width:18pt", paragraphs[0].QuerySelector(".omni-label")!.GetAttribute("style"));
        Assert.Contains("margin-left:36pt;text-indent:-18pt", paragraphs[1].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("• Symbole", paragraphs[5].TextContent);
        Assert.DoesNotContain("Symbol'", paragraphs[5].InnerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Notes_are_linked_both_ways_and_listed_footnotes_first()
    {
        var document = new WordDocument();
        var endnote = document.AddEndnote("Fin du document.");
        var footnote = document.AddFootnote("Bas de page.");
        document.AddParagraph("Texte").Add(endnote).AddText(" puis").Add(footnote).Add(new WordNoteReference(WordNoteKind.Footnote, footnote.Id));
        document.Footnotes[2] = new WordNote(2);
        document.Footnotes[2].Blocks.Add(new WordParagraph("Sans marque"));
        document.AddParagraph("Encore").Add(new WordNoteReference(WordNoteKind.Footnote, 2));

        var result = WordToHtml.Convert(document);
        var page = Parse(result.Html);

        var references = page.Body.QuerySelectorAll(".omni-section sup.omni-noteref a");
        Assert.Equal(["#omni-en-1", "#omni-fn-1", "#omni-fn-1", "#omni-fn-2"], references.Select(a => a.GetAttribute("href")));
        Assert.Equal(["i", "1", "1", "2"], references.Select(a => a.TextContent));
        Assert.Equal(["omni-enref-1", "omni-fnref-1", string.Empty, "omni-fnref-2"], references.Select(a => a.Id));
        var sections = page.Body.QuerySelectorAll("section.omni-notes");
        Assert.Equal(["omni-notes omni-footnotes", "omni-notes omni-endnotes"], sections.Select(s => s.GetAttribute("class")));
        var notes = sections[0].QuerySelectorAll(".omni-note");
        Assert.Equal(["omni-fn-1", "omni-fn-2"], notes.Select(n => n.Id));
        Assert.Equal("#omni-fnref-1", notes[0].QuerySelector("a.omni-backlink")!.GetAttribute("href"));
        Assert.Equal("1 Bas de page.", notes[0].TextContent.Trim());
        Assert.Equal("#omni-fnref-2", notes[1].QuerySelector("a.omni-backlink")!.GetAttribute("href"));
        Assert.Equal("2Sans marque", notes[1].TextContent.Trim());
        Assert.Equal("i Fin du document.", sections[1].QuerySelector("#omni-en-1")!.TextContent.Trim());
        Assert.Contains("footnotes are listed at the end of the page", result.Gaps);
    }

    [Fact]
    public void Footnote_numbers_restart_at_each_section_that_asks_for_it()
    {
        var document = new WordDocument();
        document.AddParagraph("Un").Add(document.AddFootnote("a"));
        document.AddParagraph("Deux").Add(document.AddFootnote("b"));
        document.AddSection(WordPageSetup.A4 with { Start = WordSectionStart.Continuous, FootnoteNumbering = new WordNoteNumbering { Restart = WordNoteRestart.EachSection, Format = WordNumberFormat.UpperRoman } });
        document.AddParagraph("Trois").Add(document.AddFootnote("c"));
        document.AddSection(WordPageSetup.A4 with { FootnoteNumbering = new WordNoteNumbering { Restart = WordNoteRestart.EachPage } });
        document.AddParagraph("Quatre").Add(document.AddFootnote("d"));

        var result = WordToHtml.Convert(document);
        var page = Parse(result.Html);

        Assert.Equal(["1", "2", "I", "2"], page.Body.QuerySelectorAll(".omni-section sup a").Select(a => a.TextContent));
        Assert.Contains("break-before:page", page.Body.QuerySelectorAll(".omni-section")[2].GetAttribute("style"), StringComparison.Ordinal);
        Assert.DoesNotContain("break-before", page.Body.QuerySelectorAll(".omni-section")[1].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("footnote numbering restarting at each page continues, the page has no pages", result.Gaps);
    }

    [Fact]
    public void Tracked_changes_are_accepted_by_default_or_marked_on_request()
    {
        var document = new WordDocument();
        var paragraph = document.AddParagraph("Texte ");
        paragraph.Add(new WordText("ajouté") { Revision = new WordRevision(WordRevisionKind.Inserted, "A") });
        paragraph.Add(new WordText("retiré") { Revision = new WordRevision(WordRevisionKind.Deleted, "B") });
        var table = new WordTable(100);
        table.AddRow("Gardée");
        table.AddRow("Supprimée").Revision = new WordRevision(WordRevisionKind.Deleted);
        document.AddTable(table);

        var accepted = WordToHtml.Convert(document).Html;
        var marked = WordToHtml.Convert(document, new WordHtmlOptions { Revisions = WordHtmlRevisions.Marked }).Html;

        Assert.Contains(">Texte ajouté</p>", accepted, StringComparison.Ordinal);
        Assert.DoesNotContain("retiré", accepted, StringComparison.Ordinal);
        Assert.DoesNotContain("Supprimée", accepted, StringComparison.Ordinal);
        Assert.Contains(">Texte <ins>ajouté</ins><del>retiré</del></p>", marked, StringComparison.Ordinal);
        Assert.Contains("<tr class=\"omni-row-deleted\">", marked, StringComparison.Ordinal);
    }

    [Fact]
    public void Paragraph_addresses_are_those_of_the_editor_in_every_part()
    {
        var bytes = WithEndnote(SampleDocuments.Report().ToArray());
        var model = WordDocument.Load(bytes);
        var header = model.Sections[0].Headers[WordHeaderFooterKind.Default].PartName;
        var footer = model.Sections[0].Footers[WordHeaderFooterKind.Default].PartName;

        var page = Parse(WordToHtml.Convert(bytes).Html);
        var editor = WordEditor.Open(bytes).Paragraphs().ToDictionary(p => p.Address);
        var addressed = page.Body.QuerySelectorAll("[data-address]");

        Assert.All(addressed, element =>
        {
            var paragraph = editor[element.GetAttribute("data-address")!];
            Assert.Contains(paragraph.Text.Replace("\t", string.Empty, StringComparison.Ordinal), element.TextContent.Replace("\t", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        });
        // Every paragraph with text in the parts the page shows is there; empty ones too, but for the empty
        // paragraph of a cell merged into the one above, which the page does not show.
        var shown = editor.Values.Where(p => p.PartName is "word/document.xml" or "word/footnotes.xml" or "word/endnotes.xml" || p.PartName == header || p.PartName == footer).ToList();
        var addresses = addressed.Select(e => e.GetAttribute("data-address")!).ToHashSet();
        Assert.All(shown.Where(p => p.Text.Length > 0), p => Assert.Contains(p.Address, addresses));
        Assert.Single(shown, p => p.PartName == "word/document.xml" && !addresses.Contains(p.Address));
        Assert.Contains(shown, p => p.PartName == "word/document.xml" && p.Text.Length == 0 && addresses.Contains(p.Address));
        Assert.Equal(
            new[] { "word/document.xml", "word/endnotes.xml", "word/footnotes.xml", footer!, header! }.Order(StringComparer.Ordinal),
            addressed.Select(e => e.GetAttribute("data-address")!.Split('#')[0]).Distinct().Order(StringComparer.Ordinal));
        Assert.NotNull(page.Body.QuerySelector("td > .omni-cell > p[data-address]"));
        Assert.NotNull(page.Body.QuerySelector(".omni-textbox > p[data-address]"));
        Assert.NotNull(page.Body.QuerySelector(".omni-footnotes p[data-address]"));
    }

    [Fact]
    public void The_highlighted_address_gets_its_class_its_id_and_a_style_rule()
    {
        var bytes = SampleDocuments.Report().ToArray();
        var target = WordEditor.Open(bytes).Paragraphs().First(p => p.Text == "Deux a").Address;

        var html = WordToHtml.Convert(bytes, new WordHtmlOptions { HighlightAddress = target.ToUpperInvariant() }).Html;
        var page = Parse(html);

        var highlighted = Assert.Single(page.Body.QuerySelectorAll("." + WordToHtml.HighlightClass));
        Assert.Equal(WordToHtml.HighlightId, highlighted.Id);
        Assert.Equal(target, highlighted.GetAttribute("data-address"));
        Assert.Contains("Deux a", highlighted.TextContent, StringComparison.Ordinal);
        Assert.Contains(".omni-highlight{", page.Head.QuerySelector("style")!.TextContent, StringComparison.Ordinal);
        Assert.Empty(Parse(WordToHtml.Convert(bytes).Html).Body.QuerySelectorAll(".omni-highlight"));
    }

    [Fact]
    public void A_document_built_in_code_has_no_addresses_and_says_so()
    {
        var document = new WordDocument();
        document.AddParagraph("Sans adresse");

        var result = WordToHtml.Convert(document);

        Assert.Empty(Parse(result.Html).Body.QuerySelectorAll("[data-address]"));
        Assert.Contains("paragraphs without a source address (built in code, or read from alternate-content fallback markup) have no data-address", result.Gaps);
        Assert.Equal("word/document.xml#0", Parse(WordToHtml.Convert(document.ToArray()).Html).Body.QuerySelector("p")!.GetAttribute("data-address"));
    }

    [Fact]
    public void A_report_round_trips_through_html_back_to_word_without_losing_text_or_structure()
    {
        var original = SampleDocuments.Report();

        var html = WordToHtml.Convert(original.ToArray()).Html;
        var back = HtmlToWord.Convert(html);

        var text = back.Text;
        foreach (var paragraph in original.Blocks.OfType<WordParagraph>().Where(p => p.Text.Length > 0))
        {
            Assert.Contains(paragraph.Text.Replace('\t', ' '), text, StringComparison.Ordinal);
        }

        Assert.Equal("Rapport", back.Information.Title);
        var table = Assert.Single(back.Blocks.OfType<WordTable>());
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("Fusion", table.Rows[1].Cells[0].Text);
        Assert.Equal(2, table.Rows[1].Cells[0].Properties.GridSpan);
        Assert.Contains(back.Blocks.OfType<WordParagraph>(), p => p.StyleId == "Heading1" && p.Text == "Introduction");
        Assert.Contains(back.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordHyperlink>(), l => l.Target == "https://example.org/");
        Assert.Contains("Source de la note.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_from_another_producer_converts_with_its_addresses()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "external.docx"));

        var result = WordToHtml.Convert(bytes);
        var page = Parse(result.Html);

        var editor = WordEditor.Open(bytes).Paragraphs().Where(p => p.Text.Trim().Length > 0).ToList();
        Assert.NotEmpty(editor);
        var addressed = page.Body.QuerySelectorAll("[data-address]").ToDictionary(e => e.GetAttribute("data-address")!);
        Assert.All(editor, p => Assert.Contains(p.Text.Trim(), addressed[p.Address].TextContent, StringComparison.Ordinal));
        Assert.Contains("line and page breaks are computed by the browser", result.Gaps);
    }

    [Fact]
    public void Headers_and_footers_other_than_the_first_default_ones_and_comments_are_reported()
    {
        var document = SampleDocuments.Report();
        document.Comments.Add(new WordComment(1));
        document.Sections[0].Page = document.Sections[0].Page with { Columns = 2, ColumnSeparator = true, ColumnWidths = [100, 200] };

        var result = WordToHtml.Convert(document);

        Assert.Contains("headers and footers other than the first section's default ones are left out", result.Gaps);
        Assert.Contains("comments are left out", result.Gaps);
        Assert.Contains("unequal columns shown as equal columns", result.Gaps);
        Assert.Contains("floating shapes are shown in the text flow", result.Gaps);
        Assert.Contains("column-count:2;column-gap:36pt;column-rule:0.5pt solid #000000", result.Html, StringComparison.Ordinal);
    }

    private static HtmlDocument Parse(string html) => HtmlParser.ParseDocument(html);

    // The report has no endnote: one is added to its body so the endnotes part is addressed too.
    private static byte[] WithEndnote(byte[] bytes)
    {
        var document = WordDocument.Load(bytes);
        var paragraph = new WordParagraph("Avec renvoi");
        paragraph.Add(document.AddEndnote("Renvoi final."));
        document.Sections[^1].Blocks.Add(paragraph);
        return document.ToArray();
    }
}
