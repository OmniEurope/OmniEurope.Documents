// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Csv;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Markdown;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Layout;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Tests;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.StressTests;

/// <summary>
/// Thousands of damaged files, built from files the package wrote: reading them either works or fails with
/// a documented exception (damaged data, bad package, unsupported feature, wrong password), never with an
/// internal error, and always within a time limit.
/// </summary>
public sealed class RobustnessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    public static TheoryData<string> ImageFiles => new(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images"))
        .Where(f => Path.GetExtension(f) is ".png" or ".jpg" or ".gif" or ".bmp" or ".tif")
        .Select(Path.GetFileName)!);

    [Fact]
    public async Task Damaged_word_packages_fail_cleanly()
    {
        var sources = new[] { SampleDocuments.Report().ToArray(), Fixture("Word", "external.docx") };
        var inputs = sources.SelectMany((s, i) => Damage.Package(s, 6, i).Concat(Damage.Bytes(s, 150, 10 + i)));

        await Survive(inputs, (bytes, i) =>
        {
            var document = WordDocument.Load(bytes);
            _ = document.Text;
            WordEditor.Open(bytes).ReplaceText("e", "é");
            if (i % 5 == 0)
            {
                _ = WordToPdf.Convert(document);
            }
        }, sources);
    }

    [Fact]
    public async Task Damaged_workbooks_fail_cleanly()
    {
        var source = SampleDocuments.Workbook().ToArray();
        var inputs = Damage.Package(source, 8, 1).Concat(Damage.Bytes(source, 200, 2));

        await Survive(inputs, (bytes, i) =>
        {
            var workbook = XlsxWorkbook.Load(bytes);
            foreach (var cell in workbook.Worksheets.SelectMany(s => s.Cells))
            {
                _ = cell.FormattedText;
            }

            if (i % 5 == 0)
            {
                _ = ExcelToPdf.Convert(workbook);
            }
        }, source);
    }

    [Fact]
    public async Task Damaged_pdf_files_fail_cleanly()
    {
        var sources = new[] { SampleDocuments.MergedPdf(), WordToPdf.Convert(SampleDocuments.Report()).Pdf, Fixture("Pdf", "browser.pdf") };
        var inputs = sources.SelectMany((s, i) => Damage.Bytes(s, 300, 20 + i));

        await Survive(inputs, (bytes, i) =>
        {
            var pdf = PdfDocument.Open(bytes);
            foreach (var page in pdf.Pages)
            {
                _ = page.Text;
                foreach (var image in page.Images)
                {
                    _ = image.Decode();
                }
            }

            _ = PdfLayoutAnalyzer.Analyze(pdf);
            if (i % 5 == 0 && pdf.PageCount > 0)
            {
                _ = PdfRenderer.Render(pdf.GetPage(1), new PdfRenderOptions { Dpi = 18 });
                _ = PdfEditor.Merge(pdf, pdf);
                _ = PdfCompressor.Compress(pdf);
            }
        }, sources);
    }

    [Theory]
    [MemberData(nameof(ImageFiles))]
    public async Task Damaged_images_fail_cleanly(string file)
    {
        var source = Fixture("Images", file);

        await Survive(Damage.Bytes(source, 300, file.Length), (bytes, n) =>
        {
            _ = ImageInfo.TryIdentify(bytes, out _);
            _ = ImageDecoder.Decode(bytes);
        }, source);
    }

    [Fact]
    public async Task Damaged_fonts_fail_cleanly()
    {
        var assembly = typeof(TrueTypeFont).Assembly;
        using var stream = assembly.GetManifestResourceStream("OmniEurope.Documents.Fonts.LiberationSans-Regular.ttf")!;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, TestContext.Current.CancellationToken);

        await Survive(Damage.Bytes(copy.ToArray(), 300, 3), (bytes, n) =>
        {
            var font = TrueTypeFont.Load(bytes);
            _ = font.MeasureText("Société Ωmega", 12);
            _ = TrueTypeSubsetter.Subset(font, [0, font.GetGlyphIndex('A'), font.GetGlyphIndex('é')]);
        }, copy.ToArray());
    }

    [Fact]
    public async Task Random_markup_and_csv_never_break_the_parsers()
    {
        var inputs = Damage.Text("<>/=\"' abc\n\t#*_-[]()!`|:;&\\{}0123é\r", 2000, 300, 4)
            .Select(t => System.Text.Encoding.UTF8.GetBytes(t));

        await Survive(inputs, (bytes, n) =>
        {
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            _ = MarkdownRenderer.ToHtml(text, MarkdownOptions.GitHub);
            _ = HtmlParser.ParseDocument(text).Body.TextContent;
            _ = new HtmlSanitizer().Sanitize(text);
            _ = HtmlToWord.Convert(text).Text;
            try
            {
                _ = CsvReader.ReadAll(text);
            }
            catch (CsvFormatException)
            {
                // A documented refusal of malformed quoting.
            }
        });
    }

    private static byte[] Fixture(string folder, string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", folder, file));

    // Reads the sound files first (they must read cleanly), then every damaged input; fails with the list of
    // undocumented exceptions and time-outs, or when no input at all was read successfully.
    private static async Task Survive(IEnumerable<byte[]> inputs, Action<byte[], int> read, params byte[][] sound)
    {
        foreach (var file in sound)
        {
            read(file, 0);
        }

        var failures = new List<string>();
        var index = 0;
        var successes = 0;
        foreach (var input in inputs)
        {
            var i = index++;
            try
            {
                await Task.Run(() => read(input, i), TestContext.Current.CancellationToken).WaitAsync(Limit, TestContext.Current.CancellationToken);
                successes++;
            }
            catch (TimeoutException)
            {
                failures.Add($"Still running after {Limit.TotalSeconds} s (first: #{i})");
            }
            catch (Exception exception) when (exception is InvalidDataException or DocumentFormatException or NotSupportedException or PdfPasswordException)
            {
                // A documented refusal.
            }
            catch (Exception exception)
            {
                var frame = exception.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("OmniEurope.Documents.", StringComparison.Ordinal) && !l.Contains(".Tests.", StringComparison.Ordinal))?.Trim();
                failures.Add($"{exception.GetType().Name} {frame} (first: #{i})");
            }
        }

        // One line per failing place in the code, with how many inputs reached it.
        var places = failures.GroupBy(f => f[..f.LastIndexOf(" (first", StringComparison.Ordinal)]).Select(g => $"{g.Count()} x {g.First()}");
        Assert.True(failures.Count == 0, $"{failures.Count} of {index} inputs failed:\n" + string.Join("\n", places));
        Assert.True(successes > 0, $"None of the {index} inputs was read successfully: the reader refuses everything.");
    }
}
