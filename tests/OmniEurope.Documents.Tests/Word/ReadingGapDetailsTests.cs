// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Conversion.WordFields;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using OmniEurope.Documents.Word.Reading;
using static OmniEurope.Documents.Tests.Charts.ChartFactory;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>
/// The gaps of the Word reading, the workbook reading, the field update and the content controls carry a code, keep
/// their English text, and read in French.
/// </summary>
public sealed class ReadingGapDetailsTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    [Fact]
    public void A_document_read_lists_its_gaps_with_a_code_an_english_text_and_a_french_text()
    {
        var body = """<w:p><w:r><w:drawing><wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"><wp:extent cx="100" cy="100"/><a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"/></a:graphic></wp:inline></w:drawing></w:r></w:p><w:altChunk r:id="rId9"/>""";

        var document = WordDocument.Load(DocxFactory.Build(body));

        Assert.Equal(["imported content (altChunk) skipped", "linked or missing chart skipped"], document.Gaps);
        Assert.Equal(["ReadAltChunkSkipped", "ReadChartLinkedSkipped"], document.GapDetails.Select(g => g.Code).ToList());
        Assert.Equal(document.Gaps, document.GapDetails.Select(g => g.Message).ToList());
        Assert.Equal(["contenu importé (altChunk) ignoré", "graphique lié ou manquant ignoré"], document.GapDetails.Select(g => g.Describe(French)).ToList());
    }

    [Fact]
    public void A_drawing_of_an_unsupported_kind_is_named_by_its_own_code()
    {
        var body = """<w:p><w:r><w:drawing><wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"><wp:extent cx="100" cy="100"/><a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:graphicData uri="http://schemas.microsoft.com/office/drawing/2014/chartex"/></a:graphic></wp:inline></w:drawing></w:r></w:p>""";

        var gap = Assert.Single(WordDocument.Load(DocxFactory.Build(body)).GapDetails);

        Assert.Equal("ReadChartExSkipped", gap.Code);
        Assert.Equal("chart of a type introduced by Office 2016 (waterfall, treemap, funnel...) skipped", gap.Message);
        Assert.Equal("graphique d'un type introduit par Office 2016 (cascade, treemap, entonnoir...) ignoré", gap.Describe(French));
    }

    [Fact]
    public void An_unreadable_ink_part_names_its_exception_in_english_and_leaves_it_out_in_french()
    {
        var gap = DocumentGap.Of(ReadGapCodes.ReadInkPartUnreadable, "bad trace");

        Assert.Equal("unreadable ink part: bad trace", gap.Message);
        Assert.Equal("partie d'encre illisible", gap.Describe(French));
    }

    [Fact]
    public void A_workbook_lists_its_gaps_with_a_code_and_a_french_text_and_the_conversion_carries_them()
    {
        var workbook = XlsxWorkbook.Load(Xlsx(string.Empty, "<c:chartSpace", TwoCell(3, 1, 9, 16)));

        Assert.Equal(["unreadable chart skipped"], workbook.Gaps);
        var gap = Assert.Single(workbook.GapDetails);
        Assert.Equal("ReadChartUnreadable", gap.Code);
        Assert.Equal("unreadable chart skipped", gap.Message);
        Assert.Equal("graphique illisible ignoré", gap.Describe(French));

        var converted = ExcelToPdf.ToWord(workbook);

        Assert.Equal(["unreadable chart skipped"], converted.Gaps);
        Assert.Equal("ReadChartUnreadable", Assert.Single(converted.GapDetails).Code);
    }

    [Fact]
    public void A_field_update_lists_its_gaps_with_their_values_and_a_french_text()
    {
        var body = """
            <w:p><w:fldSimple w:instr="MERGEFIELD Name"><w:r><w:t>«Name»</w:t></w:r></w:fldSimple></w:p>
            <w:p><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> TOC \c "Figure" </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>figures</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            """;
        var editor = WordEditor.Open(DocxFactory.Build(body));

        var update = WordFieldUpdater.Update(editor);

        Assert.Equal(["MERGEFIELD fields not updated", "table of contents with the \\c switch left as it was"], update.Gaps);
        Assert.Equal(["FieldKindNotUpdated", "FieldTocSwitchLeftAsIs"], update.GapDetails.Select(g => g.Code).ToList());
        Assert.Equal(update.Gaps, update.GapDetails.Select(g => g.Message).ToList());
        Assert.Equal(["MERGEFIELD"], update.GapDetails[0].Arguments);
        Assert.Equal(["champs MERGEFIELD non mis à jour", "table des matières avec le commutateur \\c laissée telle quelle"], update.GapDetails.Select(g => g.Describe(French)).ToList());
    }

    [Fact]
    public void A_refused_content_control_value_gives_its_reason_with_a_code_and_a_french_text()
    {
        var editor = WordEditor.Open(ContentControlTemplate.Build());

        var gap = Assert.Single(editor.FillContentControls(new Dictionary<string, string> { ["recu"] = "peut-être" }).Gaps);

        Assert.Equal("'peut-être' is not a boolean (true, false, 1, 0, yes, no, oui, non)", gap.Reason);
        Assert.Equal("ContentControlNotBoolean", gap.ReasonDetail.Code);
        Assert.Equal(gap.Reason, gap.ReasonDetail.Message);
        Assert.Equal("'peut-être' n'est pas un booléen (true, false, 1, 0, yes, no, oui, non)", gap.ReasonDetail.Describe(French));
    }

    [Fact]
    public void A_control_of_the_wrong_kind_gives_a_reason_with_a_code()
    {
        var editor = WordEditor.Open(ContentControlTemplate.Build());

        var gap = Assert.Single(editor.SetChecked("mois", true).Gaps);

        Assert.Equal("the control is not a check box", gap.Reason);
        Assert.Equal("ContentControlNotCheckbox", gap.ReasonDetail.Code);
        Assert.Equal("le contrôle n'est pas une case à cocher", gap.ReasonDetail.Describe(French));
    }

    [Fact]
    public void A_content_control_gap_built_by_a_caller_from_a_text_has_a_detail_without_a_code()
    {
        var gap = new WordContentControlGap("tag", "some reason");

        Assert.Equal(string.Empty, gap.ReasonDetail.Code);
        Assert.Equal("some reason", gap.ReasonDetail.Message);
    }
}
