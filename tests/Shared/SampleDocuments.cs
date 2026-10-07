// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests;

/// <summary>Rich documents written by the package, shared by the fast suite and the stress suite.</summary>
internal static class SampleDocuments
{
    /// <summary>The picture of <see cref="Report"/>.</summary>
    public static readonly byte[] Png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"));

    /// <summary>One colour per drawn page of <see cref="MergedPdf"/>, found again in the rendering wherever the page goes.</summary>
    public static readonly (byte R, byte G, byte B)[] PdfColours = [(200, 30, 30), (30, 160, 30), (30, 30, 200)];

    /// <summary>A report with headers, footers, a footnote, lists, a merged table, a landscape section, a picture and a text box.</summary>
    public static WordDocument Report()
    {
        var document = new WordDocument { Information = new WordInformation { Title = "Rapport", Author = "Équipe" } };
        var section = document.Sections[0];
        section.Page = section.Page with { TitlePage = true };
        section.Headers[WordHeaderFooterKind.Default] = new WordHeaderFooter(new WordParagraph("Rapport confidentiel", "Header"));
        section.Headers[WordHeaderFooterKind.First] = new WordHeaderFooter(new WordParagraph("Première page", "Header"));
        var footer = new WordParagraph("Page ", "Footer").Add(WordField.Page()).AddText(" / ").Add(WordField.NumPages());
        section.Footers[WordHeaderFooterKind.Default] = new WordHeaderFooter(footer);

        document.AddHeading("Introduction");
        var body = new WordParagraph("Texte en ")
        {
            Properties = new WordParagraphProperties { Alignment = WordAlignment.Justify, FirstLineIndent = -18, LineSpacing = 1.5, LineSpacingRule = WordLineSpacingRule.Multiple },
        };
        body.AddText("gras", new WordRunProperties { Bold = true, Color = "FF0000", Language = "en-GB" })
            .AddText("\tet italique, ", new WordRunProperties { Italic = true })
            .Add(new WordHyperlink("https://example.org/", "lien"))
            .AddText(".")
            .Add(document.AddFootnote("Source de la note."));
        document.Body.Add(body);

        var list = document.Numbering.AddNumberedList();
        var bullets = document.Numbering.AddBulletList();
        document.AddParagraph("Un").AsListItem(list);
        document.AddTable(Table());
        document.AddParagraph("Deux").AsListItem(list);
        document.AddParagraph("Deux a").AsListItem(list, 1);
        document.AddParagraph("Deux b").AsListItem(list, 1);
        document.AddParagraph("Trois").AsListItem(list);
        document.AddParagraph("Puce").AsListItem(bullets);

        document.AddSection(WordPageSetup.A4.ToLandscape());
        document.AddParagraph().Add(new WordPicture(WordImage.FromBytes(Png), 96, 72) { Description = "Dégradé" });
        var box = new WordTextBox(150, 40) { Floating = new WordFloatingPosition(10, "margin", 20, "paragraph") };
        box.Blocks.Add(new WordParagraph("Encadré"));
        document.AddParagraph("Avec encadré").Add(box);
        return document;
    }

    private static WordTable Table()
    {
        var table = new WordTable(100, 150, 200) { Properties = new WordTableProperties { StyleId = "TableGrid", Width = WordWidth.Percent(100) } };
        var header = table.AddRow("A", "B", "C");
        header.Properties = new WordTableRowProperties { IsHeader = true, CantSplit = true };
        foreach (var cell in header.Cells)
        {
            cell.Properties = new WordTableCellProperties { Shading = "D9E2F3" };
        }

        var merged = table.AddRow("Fusion", "Haut");
        merged.Cells[0].Properties = new WordTableCellProperties { GridSpan = 2 };
        merged.Cells[1].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Restart };
        var last = table.AddRow("1", "2", string.Empty);
        last.Cells[2].Properties = new WordTableCellProperties { VerticalMerge = WordVerticalMerge.Continue };
        return table;
    }

    /// <summary>A two-sheet workbook: styles, number and date formats, merges, frozen panes, a filter, formulas, errors, durations.</summary>
    public static XlsxWorkbook Workbook()
    {
        var workbook = new XlsxWorkbook { Title = "Ventes 2026", Author = "Service commercial" };
        var sheet = workbook.AddWorksheet("Ventes");
        var bold = XlsxStyle.Default with { Bold = true, FillColor = "D9E2F3", Border = true };

        sheet.Cell("A1").Value = "Bilan des ventes";
        sheet.Cell("A1").Style = XlsxStyle.Default with { Bold = true, FontSize = 14, HorizontalAlignment = XlsxHorizontalAlignment.Center };
        sheet.Merge(XlsxRange.Parse("A1:F1"));

        string[] headers = ["Région", "Montant", "Part", "Date", "Payé", "Total"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(2, i + 1).Value = headers[i];
            sheet.Cell(2, i + 1).Style = bold;
        }

        var money = XlsxStyle.Default with { NumberFormat = "#,##0.00" };
        var percent = XlsxStyle.Default with { NumberFormat = "0%" };
        var date = XlsxStyle.Default with { NumberFormat = "dd/mm/yyyy" };
        Row(sheet, 3, "Nord", 1234.5, 0.25, new DateTime(2026, 7, 16), true, money, percent, date);
        Row(sheet, 4, "Sud", 987.25, 0.75, new DateTime(2026, 8, 3), false, money, percent, date);

        // Text that looks like a formula stays text.
        sheet.Cell("A5").Value = "=cmd|' /C calc'!A0";
        sheet.Cell("B5").Formula = "B3/0";
        sheet.Cell("B5").Value = new XlsxError("#DIV/0!");
        sheet.Cell("F3").Formula = "B3*C3";
        sheet.Cell("F3").Value = 308.625;
        sheet.Cell("F3").Style = money with { Italic = true, FontColor = "C00000" };

        sheet.SetColumnWidth(1, 25);
        sheet.SetColumnWidth(4, 12);
        sheet.FreezePanes(2, 1);
        sheet.AutoFilter = XlsxRange.Parse("A2:F4");

        var notes = workbook.AddWorksheet("Notes");
        notes.Cell("A1").Value = "Remarque longue : les montants sont hors taxes et arrondis au centime le plus proche.";
        notes.Cell("A1").Style = XlsxStyle.Default with { WrapText = true, VerticalAlignment = XlsxVerticalAlignment.Top };
        notes.Cell("A2").Value = TimeSpan.FromHours(36);
        notes.Cell("A2").Style = XlsxStyle.Default with { NumberFormat = "[h]:mm" };

        // Too wide for a default column: Excel shows '#' for a formatted number, fewer digits under General.
        notes.Cell("B1").Value = 1234567.891;
        notes.Cell("B1").Style = money;
        notes.Cell("C1").Value = 123456789.123;
        return workbook;
    }

    private static void Row(XlsxWorksheet sheet, int row, string region, double amount, double share, DateTime day, bool paid, XlsxStyle money, XlsxStyle percent, XlsxStyle date)
    {
        sheet.Cell(row, 1).Value = region;
        (sheet.Cell(row, 2).Value, sheet.Cell(row, 2).Style) = (amount, money);
        (sheet.Cell(row, 3).Value, sheet.Cell(row, 3).Style) = (share, percent);
        (sheet.Cell(row, 4).Value, sheet.Cell(row, 4).Style) = (day, date);
        sheet.Cell(row, 5).Value = paid;
    }

    /// <summary>Six pages from three writers: three drawn pages (the second with an image), a Word page, a landscape and a portrait photo.</summary>
    public static byte[] MergedPdf()
    {
        var drawn = Drawn();
        var word = new WordDocument();
        word.AddParagraph("Annexe Word");
        var photos = ImagesToPdf.Convert([Jpeg(200, 100, 30, 120, 30), Samples.Png(100, 200, 120, 30, 30)]);
        return PdfEditor.Merge(PdfDocument.Open(drawn), PdfDocument.Open(WordToPdf.Convert(word).Pdf), PdfDocument.Open(photos));
    }

    private static byte[] Drawn()
    {
        var builder = new PdfDocumentBuilder { Title = "Rapport", Author = "Atelier" };
        var logo = builder.AddImage(Samples.Png(60, 40, 40, 40, 160));
        for (var i = 0; i < 3; i++)
        {
            var canvas = builder.AddPage();
            canvas.DrawText($"Rapport page {i + 1}", 72, 72, PdfFont.Sans, 14);
            canvas.FillRectangle(72, 200, 200, 100, new PdfColor(PdfColours[i].R, PdfColours[i].G, PdfColours[i].B));
            if (i == 1)
            {
                canvas.DrawImage(logo, 72, 400, 120, 80);
            }

            builder.AddBookmark($"Page {i + 1}", canvas);
        }

        return builder.ToArray();
    }

    private static byte[] Jpeg(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * 3];
        for (var i = 0; i < pixels.Length; i += 3)
        {
            (pixels[i], pixels[i + 1], pixels[i + 2]) = (r, g, b);
        }

        return JpegEncoder.Encode(new RasterImage(width, height, ImageColorType.Rgb, pixels));
    }
}
