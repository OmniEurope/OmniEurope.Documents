// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.Tests.Word;

public sealed class WordContentControlTests
{
    private const string MainType = DocxFactory.MainType;

    private static WordEditor Template() => WordEditor.Open(ContentControlTemplate.Build());

    private static WordContentControl Find(WordEditor editor, string tag, int occurrence = 0) =>
        editor.ContentControls().Where(c => c.Tag == tag).ElementAt(occurrence);

    private static string MainXml(WordEditor editor) => DocxFactory.Part(editor.ToArray(), "word/document.xml");

    [Fact]
    public void Lists_every_control_with_kind_text_and_settings()
    {
        var controls = Template().ContentControls();

        Assert.Equal(
            ["debut", "fin", "mois", "annee", "paiement", "signature", "locataire", "recu", "notes", "annee"],
            controls.Select(c => c.Tag).ToList());
        Assert.Equal(
            [WordContentControlKind.Date, WordContentControlKind.Date, WordContentControlKind.ComboBox, WordContentControlKind.ComboBox, WordContentControlKind.Date,
                WordContentControlKind.Date, WordContentControlKind.Text, WordContentControlKind.Checkbox, WordContentControlKind.RichText, WordContentControlKind.ComboBox],
            controls.Select(c => c.Kind).ToList());
        Assert.Equal("word/header1.xml", controls[^1].PartName);
        Assert.All(controls.Take(9), c => Assert.Equal("word/document.xml", c.PartName));

        var start = controls[0];
        Assert.Equal("Début", start.Alias);
        Assert.Equal("01/01/2026", start.Text);
        Assert.Equal(WordContentControlLevel.Inline, start.Level);
        Assert.False(start.IsBlock);
        Assert.Equal(new WordContentControlDate("dd/MM/yyyy", "fr-FR", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), start.Date);
        Assert.NotNull(start.Id);

        var month = controls[2];
        Assert.Equal(12, month.ListItems.Count);
        Assert.Equal(new WordContentControlItem("de décembre", "12"), month.ListItems[^1]);
        Assert.Equal("de janvier", month.Text);
        Assert.Null(month.Date);
        Assert.Equal(5, controls[3].ListItems.Count);

        var placeholder = controls[4];
        Assert.True(placeholder.ShowingPlaceholder);
        Assert.Null(placeholder.Date!.FullDate);
        Assert.All(controls.Where(c => c.Tag != "paiement"), c => Assert.False(c.ShowingPlaceholder));

        Assert.False(controls[7].Checked);
        Assert.Equal("☐", controls[7].Text);
        Assert.Null(controls[0].Checked);

        var notes = controls[8];
        Assert.True(notes.IsBlock);
        Assert.Equal("Première note\nSeconde note", notes.Text);
    }

    [Fact]
    public void Lists_row_and_cell_level_controls_and_a_control_without_type_as_rich_text()
    {
        var body = """
            <w:tbl><w:tr><w:sdt><w:sdtPr><w:tag w:val="cell"/></w:sdtPr><w:sdtContent><w:tc><w:p><w:r><w:t>c</w:t></w:r></w:p></w:tc></w:sdtContent></w:sdt></w:tr>
            <w:sdt><w:sdtPr><w:tag w:val="row"/></w:sdtPr><w:sdtContent><w:tr><w:tc><w:p><w:r><w:t>r</w:t></w:r></w:p></w:tc></w:tr></w:sdtContent></w:sdt></w:tbl>
            <w:p><w:sdt><w:sdtPr><w:tag w:val="citation"/><w:citation/></w:sdtPr><w:sdtContent><w:r><w:t>x</w:t></w:r></w:sdtContent></w:sdt></w:p>
            """;
        var editor = WordEditor.Open(DocxFactory.Build(body));

        var controls = editor.ContentControls();

        Assert.Equal([WordContentControlLevel.Cell, WordContentControlLevel.Row, WordContentControlLevel.Inline], controls.Select(c => c.Level).ToList());
        Assert.Equal([WordContentControlKind.RichText, WordContentControlKind.RichText, WordContentControlKind.Other], controls.Select(c => c.Kind).ToList());
        var gaps = editor.FillContentControls(new Dictionary<string, string> { ["cell"] = "a", ["row"] = "b", ["citation"] = "c" }).Gaps;
        Assert.Equal(["cell", "row", "citation"], gaps.Select(g => g.Tag).ToList());
        Assert.Empty(editor.ChangedParts);
    }

    [Fact]
    public void Fills_text_keeping_the_run_properties_of_the_first_run()
    {
        var editor = Template();

        var result = editor.FillContentControls(new Dictionary<string, string> { ["locataire"] = "Martin" });

        Assert.Equal(["locataire"], result.Filled);
        Assert.Empty(result.UnknownTags);
        Assert.Empty(result.Gaps);
        Assert.Equal(1, result.ControlsFilled);
        var control = Find(editor, "locataire");
        Assert.Equal("Martin", control.Text);
        var xml = MainXml(editor);
        var index = xml.IndexOf("Martin", StringComparison.Ordinal);
        var run = xml[xml.LastIndexOf("<w:r>", index, StringComparison.Ordinal)..index];
        Assert.Contains("<w:b", run, StringComparison.Ordinal);
        Assert.Contains("<w:lang w:val=\"fr-FR\"", run, StringComparison.Ordinal);
    }

    [Fact]
    public void Fills_a_block_control_with_one_paragraph_per_line_in_the_first_paragraph_format()
    {
        var editor = Template();

        editor.FillContentControls(new Dictionary<string, string> { ["notes"] = "A\nB\nC" });

        Assert.Equal("A\nB\nC", Find(editor, "notes").Text);
        var xml = MainXml(editor);
        Assert.Equal(3, xml.Split("<w:jc w:val=\"both\"").Length - 1);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(xml, @"<w:i[ ]*/>"));
    }

    [Theory]
    [InlineData("3", "de mars")]
    [InlineData("de mars", "de mars")]
    [InlineData("DE MARS", "de mars")]
    public void Fills_a_combo_box_by_value_or_display_text_and_writes_the_display_text(string given, string shown)
    {
        var editor = Template();

        var result = editor.FillContentControls(new Dictionary<string, string> { ["mois"] = given });

        Assert.Equal(["mois"], result.Filled);
        Assert.Equal(shown, Find(editor, "mois").Text);
    }

    [Fact]
    public void Refuses_a_value_that_is_not_in_a_drop_down_list_and_leaves_the_control_unchanged()
    {
        var editor = ListEditor("dropDownList");

        var result = editor.FillContentControls(new Dictionary<string, string> { ["couleur"] = "13" });

        Assert.Empty(result.Filled);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal("couleur", gap.Tag);
        Assert.Equal("Rouge", Find(editor, "couleur").Text);
        Assert.Empty(editor.ChangedParts);
    }

    [Fact]
    public void Fills_every_control_sharing_a_tag_across_parts()
    {
        var editor = Template();

        var result = editor.FillContentControls(new Dictionary<string, string> { ["annee"] = "2025" });

        Assert.Equal(2, result.ControlsFilled);
        Assert.Equal(["annee"], result.Filled);
        Assert.Equal(["2025", "2025"], editor.ContentControls().Where(c => c.Tag == "annee").Select(c => c.Text).ToList());
        Assert.Contains("word/header1.xml", editor.ChangedParts);
    }

    [Theory]
    [InlineData("2026-04-15")]
    [InlineData("15/04/2026")]
    public void Fills_a_date_in_the_format_of_the_control_and_updates_the_full_date(string given)
    {
        var editor = Template();

        var result = editor.FillContentControls(new Dictionary<string, string> { ["debut"] = given });

        Assert.Equal(["debut"], result.Filled);
        var date = Find(editor, "debut");
        Assert.Equal("15/04/2026", date.Text);
        Assert.Equal(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero), date.Date!.FullDate);
        Assert.Contains("w:fullDate=\"2026-04-15T00:00:00Z\"", MainXml(editor), StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_a_date_with_the_language_of_the_control()
    {
        var editor = WordEditor.Open(DocxFactory.Build("""<w:p><w:sdt><w:sdtPr><w:tag w:val="d"/><w:date><w:dateFormat w:val="d MMMM yyyy"/><w:lid w:val="fr-FR"/></w:date></w:sdtPr><w:sdtContent><w:r><w:t>x</w:t></w:r></w:sdtContent></w:sdt></w:p>"""));

        var result = editor.SetDate("d", new DateOnly(2026, 3, 1));

        Assert.Empty(result.Gaps);
        Assert.Equal("1 mars 2026", Find(editor, "d").Text);
        Assert.Equal("1 mars 2026", Find(WordEditor.Open(editor.ToArray()), "d").Text);
        Assert.Equal(new DateOnly(2026, 3, 1), DateOnly.FromDateTime(Find(editor, "d").Date!.FullDate!.Value.DateTime));
    }

    [Fact]
    public void Removes_the_placeholder_state_and_its_style_when_filling()
    {
        var editor = Template();

        var result = editor.SetDate("paiement", new DateOnly(2026, 2, 5));

        Assert.Equal(["paiement"], result.Filled);
        var control = Find(editor, "paiement");
        Assert.False(control.ShowingPlaceholder);
        Assert.Equal("05/02/2026", control.Text);
        var xml = MainXml(editor);
        Assert.DoesNotContain("showingPlcHdr", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("Textedelespacerserv", xml, StringComparison.Ordinal);
        Assert.Contains("w:fullDate=\"2026-02-05T00:00:00Z\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_run_properties_when_filling_a_date()
    {
        var editor = Template();

        editor.FillContentControls(new Dictionary<string, string> { ["fin"] = "28/02/2026" });

        var xml = MainXml(editor);
        Assert.Contains("28/02/2026</w:t>", xml, StringComparison.Ordinal);
        var index = xml.IndexOf("28/02/2026", StringComparison.Ordinal);
        var run = xml[xml.LastIndexOf("<w:r>", index, StringComparison.Ordinal)..index];
        Assert.Contains("w:ascii=\"Calibri\"", run, StringComparison.Ordinal);
        Assert.Contains("<w:lang w:val=\"fr-FR\"", run, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("not a date")]
    [InlineData("2026-13-01")]
    public void Reports_an_invalid_date_and_leaves_the_control_unchanged(string given)
    {
        var editor = Template();
        var before = Find(editor, "debut");

        var result = editor.FillContentControls(new Dictionary<string, string> { ["debut"] = given });

        Assert.Empty(result.Filled);
        Assert.Equal("debut", Assert.Single(result.Gaps).Tag);
        var after = Find(editor, "debut");
        Assert.Equal((before.Text, before.Date, before.ShowingPlaceholder), (after.Text, after.Date, after.ShowingPlaceholder));
        Assert.Empty(editor.ChangedParts);
    }

    [Fact]
    public void Reports_unknown_tags_and_still_fills_the_known_ones()
    {
        var editor = Template();

        var result = editor.FillContentControls(new Dictionary<string, string> { ["locataire"] = "Martin", ["absent"] = "x", ["autre"] = "y" });

        Assert.Equal(["locataire"], result.Filled);
        Assert.Equal(["absent", "autre"], result.UnknownTags);
        Assert.Empty(result.Gaps);
        Assert.Equal(["zzz"], editor.SelectItem("zzz", "x").UnknownTags);
    }

    [Fact]
    public void Selects_an_item_and_refuses_a_control_that_is_not_a_list()
    {
        var editor = Template();

        Assert.Equal(["annee"], editor.SelectItem("annee", "2024").Filled);
        Assert.Equal("2024", Find(editor, "annee").Text);
        Assert.Single(editor.SelectItem("locataire", "x").Gaps);
        Assert.Single(editor.SetDate("mois", new DateOnly(2026, 1, 1)).Gaps);
        Assert.Single(editor.SetChecked("mois", true).Gaps);
    }

    [Fact]
    public void Toggles_a_check_box_and_its_glyph()
    {
        var editor = Template();

        var checkedResult = editor.SetChecked("recu", true);

        Assert.Equal(["recu"], checkedResult.Filled);
        var control = Find(editor, "recu");
        Assert.True(control.Checked);
        Assert.Equal("☒", control.Text);
        Assert.Contains("w14:val=\"1\"", MainXml(editor), StringComparison.Ordinal);

        editor.FillContentControls(new Dictionary<string, string> { ["recu"] = "non" });
        Assert.False(Find(editor, "recu").Checked);
        Assert.Equal("☐", Find(editor, "recu").Text);
        Assert.Single(editor.FillContentControls(new Dictionary<string, string> { ["recu"] = "peut-être" }).Gaps);
    }

    [Fact]
    public void Uses_default_glyphs_when_the_check_box_declares_no_states()
    {
        var editor = WordEditor.Open(DocxFactory.Build($"""<w:p><w:sdt xmlns:w14="{ContentControlTemplate.W14}"><w:sdtPr><w:tag w:val="c"/><w14:checkbox/></w:sdtPr><w:sdtContent><w:r><w:t>x</w:t></w:r></w:sdtContent></w:sdt></w:p>"""));

        editor.SetChecked("c", true);

        Assert.True(Find(editor, "c").Checked);
        Assert.Equal("☒", Find(editor, "c").Text);
    }

    [Fact]
    public void Saves_a_template_as_a_document_that_loads_and_converts()
    {
        var editor = Template();
        Assert.True(editor.IsTemplate);
        editor.FillContentControls(new Dictionary<string, string>
        {
            ["debut"] = "2026-03-01", ["fin"] = "31/03/2026", ["mois"] = "3", ["annee"] = "2026", ["paiement"] = "2026-03-05", ["signature"] = "2026-03-06", ["locataire"] = "Martin",
        });
        editor.SetChecked("recu", true);

        var bytes = editor.ToDocumentArray();

        Assert.False(editor.IsTemplate);
        Assert.Contains("document.main+xml", DocxFactory.Part(bytes, "[Content_Types].xml"), StringComparison.Ordinal);
        Assert.DoesNotContain("template.main+xml", DocxFactory.Part(bytes, "[Content_Types].xml"), StringComparison.Ordinal);
        var document = WordDocument.Load(bytes);
        var pdf = WordToPdf.Convert(document).Pdf;
        var text = PdfDocument.Open(pdf).GetPage(1).Text;
        Assert.Contains("01/03/2026", text, StringComparison.Ordinal);
        Assert.Contains("31/03/2026", text, StringComparison.Ordinal);
        Assert.Contains("de mars", text, StringComparison.Ordinal);
        Assert.Contains("05/03/2026", text, StringComparison.Ordinal);
        Assert.Contains("Martin", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Saves_a_template_as_a_template_with_save_and_a_document_unchanged_with_save_as_document()
    {
        var template = Template();
        Assert.Contains("template.main+xml", DocxFactory.Part(template.ToArray(), "[Content_Types].xml"), StringComparison.Ordinal);

        var document = WordEditor.Open(ContentControlTemplate.Build(MainType));
        Assert.False(document.IsTemplate);
        var saved = document.ToDocumentArray();

        Assert.Empty(document.ChangedParts);
        Assert.Equal(DocxFactory.Entries(ContentControlTemplate.Build(MainType)).Keys, DocxFactory.Entries(saved).Keys);
    }

    [Fact]
    public void Round_trips_filled_values_through_save_and_reopen()
    {
        var editor = Template();
        editor.FillContentControls(new Dictionary<string, string> { ["debut"] = "2026-05-01", ["mois"] = "de mai", ["notes"] = "Une\nDeux", ["locataire"] = "Durand" });
        editor.SetChecked("recu", true);

        var reopened = WordEditor.Open(editor.ToDocumentArray());

        Assert.False(reopened.IsTemplate);
        Assert.Equal("01/05/2026", Find(reopened, "debut").Text);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), Find(reopened, "debut").Date!.FullDate);
        Assert.Equal("de mai", Find(reopened, "mois").Text);
        Assert.Equal("Une\nDeux", Find(reopened, "notes").Text);
        Assert.Equal("Durand", Find(reopened, "locataire").Text);
        Assert.True(Find(reopened, "recu").Checked);
        Assert.Equal(editor.ContentControls(), reopened.ContentControls(), new ControlComparer());
    }

    [Fact]
    public void Reads_and_fills_the_anonymised_rent_receipt_template()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.dotx");
        var editor = WordEditor.Open(File.ReadAllBytes(path));

        var controls = editor.ContentControls();

        Assert.True(editor.IsTemplate);
        Assert.Equal(6, controls.Count);
        Assert.Equal(2, controls.Count(c => c.Kind == WordContentControlKind.ComboBox));
        Assert.Equal(4, controls.Count(c => c.Kind == WordContentControlKind.Date));
        Assert.All(controls.Where(c => c.Kind == WordContentControlKind.Date), c => Assert.Equal("dd/MM/yyyy", c.Date!.Format));
        var tags = controls.Select(c => c.Tag!).Distinct().ToList();
        var values = tags.ToDictionary(t => t, t => controls.First(c => c.Tag == t).Kind == WordContentControlKind.Date ? "2026-03-01" : controls.First(c => c.Tag == t).ListItems[2].Value);

        var result = editor.FillContentControls(values);

        Assert.Equal(tags.Count, result.Filled.Count);
        Assert.Empty(result.Gaps);
        Assert.Empty(result.UnknownTags);
        Assert.All(editor.ContentControls(), c => Assert.False(c.ShowingPlaceholder));
        var reopened = WordEditor.Open(editor.ToDocumentArray());
        Assert.All(reopened.ContentControls().Where(c => c.Kind == WordContentControlKind.Date), c => Assert.Equal("01/03/2026", c.Text));
        Assert.NotEmpty(WordToPdf.Convert(WordDocument.Load(editor.ToDocumentArray())).Pdf);
    }

    private static WordEditor ListEditor(string kind, bool placeholder = false)
    {
        const string run = """<w:rPr><w:b/><w:sz w:val="22"/></w:rPr>""";
        var items = """<w:listItem w:displayText="Rouge" w:value="R"/><w:listItem w:displayText="Vert" w:value="V"/>""";
        var content = placeholder
            ? """<w:r><w:rPr><w:rStyle w:val="PlaceholderText"/><w:b/></w:rPr><w:t>Choisissez</w:t></w:r>"""
            : $"""<w:r>{run}<w:t>Rouge</w:t></w:r>""";
        var body = $"""<w:p><w:sdt><w:sdtPr><w:tag w:val="couleur"/><w:id w:val="7"/>{(placeholder ? "<w:showingPlcHdr/>" : string.Empty)}<w:{kind}>{items}</w:{kind}></w:sdtPr><w:sdtContent>{content}</w:sdtContent></w:sdt></w:p>""";
        return WordEditor.Open(DocxFactory.Build(body));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_combo_box_takes_free_text_like_word_does(bool placeholder)
    {
        var editor = ListEditor("comboBox", placeholder);

        var result = editor.SelectItem("couleur", "Bleu ciel");

        Assert.Equal(["couleur"], result.Filled);
        Assert.Empty(result.Gaps);
        var reopened = Find(WordEditor.Open(editor.ToDocumentArray()), "couleur");
        Assert.Equal("Bleu ciel", reopened.Text);
        Assert.False(reopened.ShowingPlaceholder);
        Assert.Equal(WordContentControlKind.ComboBox, reopened.Kind);
        Assert.Equal(2, reopened.ListItems.Count);
        var xml = MainXml(editor);
        Assert.DoesNotContain("showingPlcHdr", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("PlaceholderText", xml, StringComparison.Ordinal);
        Assert.Matches("<w:b ?/>", xml);
        Assert.Contains("Bleu ciel", PdfDocument.Open(WordToPdf.Convert(WordDocument.Load(editor.ToDocumentArray())).Pdf).GetPage(1).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" Bleu ")]
    [InlineData("a&b<c\"d")]
    [InlineData("ligne 1\nligne 2")]
    public void A_combo_box_keeps_free_text_with_spaces_markup_characters_and_line_breaks(string value)
    {
        var editor = ListEditor("comboBox");

        var result = editor.SelectItem("couleur", value);

        Assert.Empty(result.Gaps);
        Assert.Equal(value, Find(WordEditor.Open(editor.ToDocumentArray()), "couleur").Text);
    }

    [Fact]
    public void A_combo_box_without_entries_takes_free_text()
    {
        var body = """<w:p><w:sdt><w:sdtPr><w:tag w:val="ville"/><w:comboBox/></w:sdtPr><w:sdtContent><w:r><w:t>Paris</w:t></w:r></w:sdtContent></w:sdt></w:p>""";
        var editor = WordEditor.Open(DocxFactory.Build(body));

        Assert.Empty(editor.SelectItem("ville", "Lyon").Gaps);
        Assert.Equal("Lyon", Find(WordEditor.Open(editor.ToDocumentArray()), "ville").Text);
    }

    [Fact]
    public void A_combo_box_still_selects_the_entry_that_matches()
    {
        var editor = ListEditor("comboBox");

        Assert.Empty(editor.SelectItem("couleur", "V").Gaps);
        Assert.Equal("Vert", Find(editor, "couleur").Text);
        Assert.Empty(editor.SelectItem("couleur", "rouge").Gaps);
        Assert.Equal("Rouge", Find(editor, "couleur").Text);
        Assert.Empty(editor.FillContentControls(new Dictionary<string, string> { ["couleur"] = "Vert" }).Gaps);
        Assert.Equal("Vert", Find(editor, "couleur").Text);
    }

    [Fact]
    public void A_drop_down_list_refuses_a_value_outside_its_entries()
    {
        var editor = ListEditor("dropDownList");
        var before = Find(editor, "couleur");

        var selected = editor.SelectItem("couleur", "Bleu ciel");
        var filled = editor.FillContentControls(new Dictionary<string, string> { ["couleur"] = "Bleu ciel" });

        Assert.Empty(selected.Filled);
        Assert.Single(selected.Gaps);
        Assert.Empty(filled.Filled);
        Assert.Single(filled.Gaps);
        Assert.Equal(before.Text, Find(editor, "couleur").Text);
        Assert.Empty(editor.ChangedParts);
        Assert.Empty(editor.SelectItem("couleur", "V").Gaps);
        Assert.Equal("Vert", Find(editor, "couleur").Text);
    }

    [Fact]
    public void A_drop_down_list_without_entries_refuses_any_value_while_a_combo_box_takes_it()
    {
        static WordEditor Empty(string kind) => WordEditor.Open(DocxFactory.Build(
            $"""<w:p><w:sdt><w:sdtPr><w:tag w:val="ville"/><w:id w:val="7"/><w:{kind}/></w:sdtPr><w:sdtContent><w:r><w:t>Paris</w:t></w:r></w:sdtContent></w:sdt></w:p>"""));
        var editor = Empty("dropDownList");

        var selected = editor.SelectItem("ville", "Lyon");
        var filled = editor.FillContentControls(new Dictionary<string, string> { ["ville"] = "Lyon" });

        Assert.Empty(selected.Filled);
        Assert.Single(selected.Gaps);
        Assert.Empty(filled.Filled);
        Assert.Single(filled.Gaps);
        Assert.Equal("Paris", Find(editor, "ville").Text);
        Assert.Empty(Empty("comboBox").SelectItem("ville", "Lyon").Gaps);
    }

    private sealed class ControlComparer : IEqualityComparer<WordContentControl>
    {
        public bool Equals(WordContentControl? x, WordContentControl? y) =>
            x is not null && y is not null && x.Tag == y.Tag && x.Text == y.Text && x.Kind == y.Kind && x.Checked == y.Checked && x.Date == y.Date && x.ShowingPlaceholder == y.ShowingPlaceholder;

        public int GetHashCode(WordContentControl obj) => obj.Tag?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
