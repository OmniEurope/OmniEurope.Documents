using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Imaging.Jbig2;
using OmniEurope.Documents.Imaging.Jpeg2000;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Tests;
using OmniEurope.Documents.Tests.Imaging;
using OmniEurope.Documents.Word;

var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
var results = new List<object>();
var failed = 0;
void Inspect(string relative, Func<string> work)
{
    var path = Path.Combine(fixtures, relative);
    var watch = Stopwatch.StartNew();
    try
    {
        var detail = work();
        results.Add(new { path = "tests/OmniEurope.Documents.Tests/Fixtures/" + relative.Replace('\\', '/'), bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), success = true, detail, milliseconds = watch.ElapsedMilliseconds });
    }
    catch (Exception e)
    {
        failed++;
        results.Add(new { path = "tests/OmniEurope.Documents.Tests/Fixtures/" + relative.Replace('\\', '/'), success = false, detail = e.GetType().Name + ": " + e.Message, milliseconds = watch.ElapsedMilliseconds });
    }
}

if (args.Length > 1)
{
    Inspect(args[1], () =>
    {
        var image = ImageDecoder.Decode(File.ReadAllBytes(Path.Combine(fixtures, args[1])));
        Assert.True(image.Width > 0 && image.Height > 0 && image.Pixels.Length > 0);
        return $"Image decodee integralement : {image.Width}x{image.Height}, {image.ColorType}, {image.Pixels.Length} octets pixels";
    });
    File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Inspections={results.Count}, failures={failed}");
    return failed == 0 ? 0 : 1;
}

var jbigSamples = Jbig2Conformance.Samples("Samples").Concat(Jbig2Conformance.Samples("Large")).ToList();
foreach (var group in jbigSamples.GroupBy(s => s.File))
{
    Inspect("Jbig2/" + group.Key, () =>
    {
        var notes = new List<string>();
        foreach (var sample in group)
        {
            var (globals, bytes) = Jbig2Conformance.Embedded(sample);
            var expected = Jbig2Conformance.Expected(sample);
            var decoded = Jbig2Decoder.Decode(bytes, globals);
            Assert.Equal((expected.Width, expected.Height), (decoded.Width, decoded.Height));
            Assert.Equal(expected.Pixels, decoded.Pixels);
            var raster = Assert.Single(PdfDocument.Open(Jbig2Conformance.Pdf(bytes, globals, expected.Width, expected.Height)).GetPage(1).Images).Decode()!;
            Assert.Equal(expected.Pixels.Select(p => p == 1 ? (byte)0 : (byte)255), raster.Pixels);
            notes.Add($"page={sample.Page}, {decoded.Width}x{decoded.Height}, reference={sample.Expected}, direct+PDF pixels exacts");
        }
        return string.Join("; ", notes);
    });
}
foreach (var reference in jbigSamples.GroupBy(s => s.Expected))
{
    Inspect("Jbig2/Expected/" + reference.Key, () =>
    {
        var bitmap = Jbig2Conformance.Expected(reference.First());
        Assert.Equal(bitmap.Width * bitmap.Height, bitmap.Pixels.Length);
        return $"PBM P4 lu en entier, {bitmap.Width}x{bitmap.Height}, {reference.Count()} comparaison(s) de pages associees";
    });
}

foreach (var sample in Jpeg2000Conformance.Samples())
{
    Inspect("Jpeg2000/" + sample.File, () =>
    {
        var image = Jpeg2000Decoder.Decode(Jpeg2000Conformance.Data(sample));
        var decoded = image.Colors.Concat(image.Alpha is null ? [] : [image.Alpha]).Select(p => new Jpeg2000TestPlane(p.Width, p.Height, (1 << p.Precision) - 1, p.Signed ? [.. p.Samples.Select(v => v + (1 << (p.Precision - 1)))] : p.Samples)).ToList();
        var expected = Jpeg2000Conformance.Expected(sample);
        var mismatch = Jpeg2000Conformance.Mismatch(decoded, expected, sample.Tolerance);
        Assert.Null(mismatch);
        return $"JPX decode, {decoded.Count} plans, tolerance={sample.Tolerance}, reference={sample.Expected}, {decoded.Sum(p => p.Samples.Length)} echantillons compares";
    });
}
foreach (var file in Directory.GetFiles(Path.Combine(fixtures, "Jpeg2000", "Expected"), "*.pgm"))
{
    Inspect(Path.GetRelativePath(fixtures, file), () =>
    {
        var planes = Jpeg2000Conformance.Planes(File.ReadAllBytes(file));
        foreach (var plane in planes)
        {
            Assert.Equal(plane.Width * plane.Height, plane.Samples.Length);
            Assert.All(plane.Samples, value => Assert.InRange(value, 0, plane.Max));
        }
        return $"PGM concatene lu en entier : {string.Join(", ", planes.Select(p => $"{p.Width}x{p.Height}/max{p.Max}"))}";
    });
}
Inspect("Jpeg2000/Large/large-4096x3072.jp2", () =>
{
    var bytes = File.ReadAllBytes(Path.Combine(fixtures, "Jpeg2000", "Large", "large-4096x3072.jp2"));
    var image = Assert.Single(PdfDocument.Open(Jpeg2000Conformance.Pdf(bytes, 4096, 3072)).GetPage(1).Images).Decode()!;
    Assert.Equal((4096, 3072), (image.Width, image.Height));
    var expected = Jpeg2000Conformance.Pattern(4096, 3072);
    Assert.Equal(expected, image.Pixels);
    return "PDF JPX 4096x3072, 37748736 composantes comparees au motif independant";
});

foreach (var file in Directory.GetFiles(Path.Combine(fixtures, "Images")))
{
    var extension = Path.GetExtension(file);
    if (extension is not (".jpg" or ".png" or ".tif" or ".bmp" or ".gif")) continue;
    Inspect(Path.GetRelativePath(fixtures, file), () =>
    {
        var image = ImageDecoder.Decode(File.ReadAllBytes(file));
        Assert.True(image.Width > 0 && image.Height > 0);
        var name = Path.GetFileName(file);
        if (File.Exists(file + ".reference.bgr"))
        {
            var reference = File.ReadAllBytes(file + ".reference.bgr");
            Assert.Equal(image.Width * image.Height * 3, reference.Length);
            var difference = ImageFixtures.Compare(image, reference);
            Assert.True(difference.Mean < 2.5 && difference.Max < 48);
        }
        if (File.Exists(file + ".reference.cmyk"))
        {
            var reference = File.ReadAllBytes(file + ".reference.cmyk");
            Assert.Equal(reference.Length, image.Pixels.Length);
            var differences = reference.Zip(image.Pixels, (a, b) => Math.Abs(a - b)).ToList();
            Assert.True(differences.Average() < 2.5 && differences.Max() < 48);
        }
        return $"Decode natif {image.Width}x{image.Height}, {image.ColorType}, {image.Pixels.Length} octets pixels";
    });
}
foreach (var file in Directory.GetFiles(Path.Combine(fixtures, "Images"), "*.reference.*"))
{
    Inspect(Path.GetRelativePath(fixtures, file), () =>
    {
        var original = file[..file.LastIndexOf(".reference.", StringComparison.Ordinal)];
        var image = ImageDecoder.Decode(File.ReadAllBytes(original));
        var reference = File.ReadAllBytes(file);
        if (file.EndsWith(".bgr", StringComparison.Ordinal))
        {
            Assert.Equal(image.Width * image.Height * 3, reference.Length);
            var difference = ImageFixtures.Compare(image, reference);
            Assert.True(difference.Mean < 2.5 && difference.Max < 48);
            return $"BGR24 integral, {image.Width}x{image.Height}, MAE={difference.Mean:F6}, max={difference.Max}, compare au JPEG associe";
        }
        Assert.Equal(image.Pixels.Length, reference.Length);
        var differences = reference.Zip(image.Pixels, (a, b) => Math.Abs(a - b)).ToList();
        Assert.True(differences.Average() < 2.5 && differences.Max() < 48);
        return $"CMYK integral, {reference.Length} octets, MAE={differences.Average():F6}, max={differences.Max()}, compare au JPEG associe";
    });
}

foreach (var file in Directory.GetFiles(fixtures, "*.pdf", SearchOption.AllDirectories))
{
    Inspect(Path.GetRelativePath(fixtures, file), () =>
    {
        var pdf = PdfDocument.Open(File.ReadAllBytes(file));
        Assert.True(pdf.PageCount > 0);
        var text = 0;
        foreach (var page in pdf.Pages)
        {
            text += page.Text.Length;
            foreach (var image in page.Images) _ = image.Decode();
            var raster = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 18 }).Image;
            Assert.True(raster.Pixels.Length > 0);
        }
        return $"PDF ouvert, {pdf.PageCount} pages, {text} caracteres extraits, images decodees et chaque page rendue a18dpi";
    });
}
foreach (var file in Directory.GetFiles(fixtures, "*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f) is ".docx" or ".dotx"))
{
    Inspect(Path.GetRelativePath(fixtures, file), () =>
    {
        var bytes = File.ReadAllBytes(file);
        var document = WordDocument.Load(bytes);
        Assert.True(document.Sections.Count > 0);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var xml = 0;
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            if (entry.FullName.EndsWith(".xml", StringComparison.Ordinal) || entry.FullName.EndsWith(".rels", StringComparison.Ordinal)) { _ = XDocument.Load(stream); xml++; }
            else { using var memory = new MemoryStream(); stream.CopyTo(memory); }
        }
        return $"Word charge : {document.Sections.Count} section(s), {document.Text.Length} caracteres ; ZIP integral {archive.Entries.Count} parties, {xml} XML parses";
    });
}
File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Inspections={results.Count}, failures={failed}");
return failed == 0 ? 0 : 1;
