// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Layout;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.Tests.Integration;

/// <summary>
/// A Word document through its whole life: written, saved, reread, filled in, extended with other documents,
/// merged, converted to PDF, and that PDF read back, analysed and rendered.
/// </summary>
public sealed class WordWorkflowTests
{
    private static readonly byte[] Logo = Samples.Png(40, 30, 220, 20, 20);

    [Fact]
    public void A_report_survives_saving_and_reading_back()
    {
        var reread = WordDocument.Load(Report().ToArray());

        Assert.Empty(reread.Gaps);
        Assert.Equal(("Rapport annuel", "Service qualité"), (reread.Information.Title, reread.Information.Author));
        Assert.Equal(2, reread.Sections.Count);
        Assert.True(reread.Sections[1].Page.Landscape);
        Samples.InOrder(reread.Text, "Rapport annuel", "Client : {{client}}", "Premier point", "Troisième point", "Région", "Nord", "Annexe");
        var picture = reread.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordPicture>().Single();
        Assert.Equal(Logo, picture.Image.Data);
        var note = reread.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordNoteReference>().Single();
        Assert.Equal("Chiffres provisoires.", reread.Footnotes[note.Id].Text.Trim());

        var counter = new WordListCounter(reread.Numbering);
        var labels = reread.Blocks.OfType<WordParagraph>()
            .Where(p => p.Properties.NumberingId is not null)
            .Select(p => counter.Next(p.Properties.NumberingId!.Value, p.Properties.NumberingLevel ?? 0)!.Value.Label);
        Assert.Equal(["1.", "2.", "3."], labels);
    }

    [Fact]
    public void Filling_in_and_appending_keep_both_documents_whole()
    {
        var editor = WordEditor.Open(Report().ToArray());

        var replaced = editor.ReplaceText(new Dictionary<string, string> { ["{{client}}"] = "Société Exemple", ["{{date}}"] = "7 octobre 2026" });
        editor.Append(WordEditor.Open(Conclusion().ToArray()));
        var result = WordDocument.Load(editor.ToArray());

        Assert.Equal(2, replaced);
        Assert.DoesNotContain("{{", result.Text, StringComparison.Ordinal);
        Samples.InOrder(result.Text, "Rapport annuel", "Client : Société Exemple", "le 7 octobre 2026", "Annexe", "Conclusion", "Action A", "Action B");
        var notes = result.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordNoteReference>()
            .Select(n => result.Footnotes[n.Id].Text.Trim());
        Assert.Equal(["Chiffres provisoires.", "Validé en comité."], notes);
        Assert.Equal(Logo, result.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordPicture>().Single().Image.Data);
    }

    [Fact]
    public void Three_documents_merge_in_order_and_convert_to_a_readable_pdf()
    {
        var merged = WordEditor.Merge(Report().ToArray(), Conclusion().ToArray(), Letter("Annexe B : glossaire").ToArray());

        var document = WordDocument.Load(merged);
        Samples.InOrder(document.Text, "Rapport annuel", "Annexe", "Conclusion", "Annexe B : glossaire");

        var conversion = WordToPdf.Convert(merged);
        var pdf = PdfDocument.Open(conversion.Pdf);

        Assert.Empty(conversion.Gaps);
        Assert.Equal(conversion.PageCount, pdf.PageCount);
        Assert.Equal("Rapport annuel", pdf.Information.Title);
        Samples.InOrder(Samples.AllText(pdf), "Rapport annuel", "Premier point", "Nord", "Annexe", "Conclusion", "Annexe B : glossaire");

        // The second section is landscape; the merged documents start on new portrait pages.
        var annex = pdf.Pages.First(p => p.Text.Contains("Annexe", StringComparison.Ordinal) && !p.Text.Contains("Rapport annuel", StringComparison.Ordinal));
        Assert.True(annex.Width > annex.Height);
        var conclusion = pdf.Pages.First(p => p.Text.Contains("Conclusion", StringComparison.Ordinal));
        Assert.True(conclusion.Width < conclusion.Height);
        Assert.True(conclusion.Number > annex.Number);
    }

    [Fact]
    public void The_converted_report_shows_its_picture_header_and_heading()
    {
        var pdf = PdfDocument.Open(WordToPdf.Convert(Report()).Pdf);
        var first = pdf.GetPage(1);

        // The red logo is 40 x 30 points: about 20 x 15 pixels at 36 dpi.
        Assert.InRange(Samples.Pixels(first, 220, 20, 20), 200, 400);
        Assert.Contains("Document interne", first.Text, StringComparison.Ordinal);
        Assert.Contains("Page 1 sur 2", first.Text, StringComparison.Ordinal);

        var layout = PdfLayoutAnalyzer.AnalyzePage(first);
        var heading = layout.Blocks.SelectMany(b => b.Lines).Single(l => l.Text == "Rapport annuel");
        var body = layout.Blocks.SelectMany(b => b.Lines).First(l => l.Text.StartsWith("Client", StringComparison.Ordinal));
        Assert.True(heading.FontSize > body.FontSize);
        Assert.True(heading.Baseline > body.Baseline);
    }

    [Fact]
    public void Editing_one_paragraph_touches_only_the_main_part()
    {
        var original = Report().ToArray();
        var editor = WordEditor.Open(original);

        var target = editor.Paragraphs().Single(p => p.Read().Text == "Deuxième point");
        target.SetText("Deuxième point, révisé");
        var edited = editor.ToArray();

        Assert.Equal(["word/document.xml"], editor.ChangedParts);
        var reread = WordDocument.Load(edited);
        Samples.InOrder(reread.Text, "Premier point", "Deuxième point, révisé", "Troisième point");
        var item = reread.Blocks.OfType<WordParagraph>().Single(p => p.Text == "Deuxième point, révisé");
        Assert.NotNull(item.Properties.NumberingId);
        Assert.Equal(WordDocument.Load(original).Sections.Count, reread.Sections.Count);
        Assert.Equal("Deuxième point, révisé", WordEditor.Open(edited).FindParagraph(target.Address)?.Text);
    }

    private static WordDocument Report()
    {
        var document = new WordDocument { Information = new WordInformation { Title = "Rapport annuel", Author = "Service qualité" } };
        var section = document.Sections[0];
        section.Headers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("Document interne", "Header"));
        section.Footers[WordHeaderFooterKind.Default] = new WordHeaderFooter(
            new WordParagraph("Page ", "Footer").Add(WordField.Page()).AddText(" sur ").Add(WordField.NumPages()));

        document.AddHeading("Rapport annuel");
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(Logo), 40, 30) { Description = "Logo" });
        document.AddParagraph("Client : {{client}}, rapport établi le {{date}}.")
            .Add(document.AddFootnote("Chiffres provisoires."));

        var list = document.Numbering.AddNumberedList();
        document.AddParagraph("Premier point").AsListItem(list);
        document.AddParagraph("Deuxième point").AsListItem(list);
        document.AddParagraph("Troisième point").AsListItem(list);

        var table = new WordTable(150, 100, 100) { Properties = new WordTableProperties { StyleId = "TableGrid" } };
        table.AddRow("Région", "2025", "2026").Properties = new WordTableRowProperties { IsHeader = true };
        table.AddRow("Nord", "120", "135");
        table.AddRow("Sud", "98", "110");
        document.AddTable(table);

        document.AddSection(WordPageSetup.A4.ToLandscape());
        document.AddHeading("Annexe", 2);
        document.AddParagraph("Détail des mesures en paysage.");
        return document;
    }

    private static WordDocument Conclusion()
    {
        var document = new WordDocument();
        document.AddHeading("Conclusion");
        document.AddParagraph("Décisions prises.").Add(document.AddFootnote("Validé en comité."));
        var list = document.Numbering.AddBulletList();
        document.AddParagraph("Action A").AsListItem(list);
        document.AddParagraph("Action B").AsListItem(list);
        return document;
    }

    private static WordDocument Letter(string title)
    {
        var document = new WordDocument();
        document.AddHeading(title);
        document.AddParagraph("Terme : définition.");
        return document;
    }
}
