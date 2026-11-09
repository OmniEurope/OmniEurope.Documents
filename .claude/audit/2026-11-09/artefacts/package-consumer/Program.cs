using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

Console.WriteLine($"PACKAGE_ASSEMBLY={typeof(WordDocument).Assembly.Location}");
Console.WriteLine($"PACKAGE_VERSION={typeof(WordDocument).Assembly.GetName().Version}");
Smoke.Run();
Console.WriteLine($"EXTRA_FONTS_LOADED_BEFORE_USE={AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "OmniEurope.Documents.Fonts")}");
FontSmoke.Run();
var assemblies = new[] { typeof(WordDocument).Assembly, typeof(DocumentFonts).Assembly };
var api = assemblies.SelectMany(a => a.GetExportedTypes()).OrderBy(t => t.FullName, StringComparer.Ordinal)
    .Select(t => new { Type = t.FullName, Members = t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.ToString()).Order(StringComparer.Ordinal).ToArray() }).ToArray();
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "public-api.json"), JsonSerializer.Serialize(api, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PUBLIC_TYPES={api.Length}");
Console.WriteLine("RESULT: PASS");

internal static class FontSmoke
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        var families = DocumentFonts.Families;
        foreach (var expected in new[] { "Selawik", "Gelasio", "Noto Sans", "Noto Serif", "Roboto", "Open Sans", "Lato" })
            Check(families.Contains(expected, StringComparer.OrdinalIgnoreCase), $"Famille complémentaire {expected}");
        Check(DocumentFonts.Licence("Selawik").Contains("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase), "Licence Selawik accessible");
        Check(DocumentFonts.Notice().Contains("Selawik", StringComparison.OrdinalIgnoreCase), "Notice des fontes accessible");
        var library = DocumentFonts.CreateLibrary();
        Check(library.Resolve("Segoe UI", false, false).HasGlyph('A'), "Substitution Segoe UI charge une vraie fonte et son glyphe");
        Check(library.Describe("Segoe UI").Used == "Selawik" && !library.Describe("Segoe UI").Exact, "Substitution explicitement décrite");
        var document = new WordDocument();
        document.AddParagraph().AddText("Texte complémentaire", new WordRunProperties { Font = "Segoe UI" });
        var result = WordToPdf.Convert(document, new WordPdfOptions { Fonts = library });
        Check(PdfDocument.Open(result.Pdf).GetPage(1).Text.Contains("Texte complémentaire", StringComparison.Ordinal) && result.Gaps.Count > 0, "PDF relu, texte préservé et substitution signalée");
    }

    private static void Check(bool result, string message)
    {
        Console.WriteLine($"FONT_ASSERT: {(result ? "PASS" : "FAIL")} {message}");
        if (!result) throw new InvalidOperationException(message);
    }
}
