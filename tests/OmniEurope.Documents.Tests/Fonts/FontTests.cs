// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using OmniEurope.Documents.Fonts;

namespace OmniEurope.Documents.Tests.Fonts;

public sealed class FontTests
{
    // Letters of the 24 official EU languages beyond ASCII, plus the Greek and Cyrillic alphabets.
    private const string EuropeanLetters =
        "ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿĀāĂăĄąĆćČčĎďĐđĒēĖėĘęĚěĞğĢģĪīĮįİıĶķĹĺĻļĽľŁłŃńŅņŇňŌōŐőŒœŔŕŘřŚśŞşŠšŢţŤťŪūŮůŰűŲųŸŹźŻżŽžȘșȚțĊċĠġĦħ"
        + "ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩαβγδεζηθικλμνξοπρσςτυφχψωάέήίόύώΆΈΉΊΌΎΏϊϋΐΰ"
        + "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЬЮЯабвгдежзийклмнопрстуфхцчшщъьюя"
        + "€‚„…–—‘’“”•«»§©®°±·×÷";

    public static TheoryData<string> Families => new() { "Liberation Sans", "Liberation Serif", "Liberation Mono", "Carlito", "Caladea" };

    [Theory]
    [MemberData(nameof(Families))]
    public void Bundled_faces_load_with_their_names_and_styles(string family)
    {
        foreach (var (bold, italic) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            var font = FontLibrary.Default.Resolve(family, bold, italic);

            Assert.Equal(family, font.Names.Family);
            Assert.Equal(bold, font.IsBold);
            Assert.Equal(italic, font.IsItalic);
            Assert.True(font.EmbeddingAllowed);
            Assert.NotNull(font.Glyphs);
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void Every_eu_language_greek_and_cyrillic_resolve_to_a_glyph(string family)
    {
        var missing = EuropeanLetters
            .Where(c => !FontLibrary.Default.ResolveForCharacter(family, false, false, c).HasGlyph(c))
            .ToArray();

        Assert.True(missing.Length == 0, $"{family} lacks: {new string(missing)}");
    }

    [Fact]
    public void Caladea_falls_back_to_liberation_serif_for_greek_and_cyrillic()
    {
        Assert.False(FontLibrary.Default.Resolve("Caladea").HasGlyph('Ω'));
        Assert.Equal("Liberation Serif", FontLibrary.Default.ResolveForCharacter("Cambria", false, false, 'Ω').Names.Family);
        Assert.Equal("Caladea", FontLibrary.Default.ResolveForCharacter("Cambria", false, false, 'é').Names.Family);
    }

    [Fact]
    public void Liberation_sans_has_the_widths_of_arial()
    {
        var font = FontLibrary.Default.Resolve("Arial");

        // Arial advances in 2048ths of an em: H 1479, e 1139, l 455, o 1139.
        Assert.Equal(2048, font.UnitsPerEm);
        Assert.Equal((1479 + 1139 + 455 + 455 + 1139) * 12 / 2048.0, font.MeasureText("Hello", 12), 6);
        Assert.Equal("LiberationSans", FontLibrary.BundledStem("Helvetica"));
        Assert.Equal("LiberationMono", FontLibrary.BundledStem("Consolas"));
        Assert.Equal("LiberationSans", FontLibrary.BundledStem("Verdana"));
        Assert.Equal("LiberationSerif", FontLibrary.BundledStem("Garamond"));
        Assert.Equal("Carlito", FontLibrary.BundledStem("Calibri"));
    }

    [Fact]
    public void Metrics_are_plausible()
    {
        var font = FontLibrary.Default.Resolve("Liberation Serif");

        Assert.InRange(font.WinAscent, 1500, 2100);
        Assert.InRange(font.WinDescent, 300, 700);
        Assert.True(font.UnderlinePosition < 0);
        Assert.True(font.UnderlineThickness > 0);
        Assert.InRange(font.CapHeight, 1100, 1600);
        Assert.Equal(0, font.GetGlyphIndex(0x10FFFF));
        Assert.True(FontLibrary.Default.Resolve("Courier New").IsFixedPitch);
        Assert.True(FontLibrary.Default.Resolve("Arial", italic: true).ItalicAngle < 0);
    }

    [Fact]
    public void Outlines_have_closed_contours_with_on_curve_points()
    {
        var font = FontLibrary.Default.Resolve("Arial");

        var outline = font.Glyphs!.GetOutline(font.GetGlyphIndex('O'));

        Assert.Equal(2, outline.Contours.Count);
        Assert.All(outline.Contours, c => Assert.Contains(c, p => p.OnCurve));
        Assert.Empty(font.Glyphs.GetOutline(font.GetGlyphIndex(' ')).Contours);
    }

    [Fact]
    public void Subset_keeps_requested_glyphs_and_their_outlines()
    {
        var font = FontLibrary.Default.Resolve("Liberation Sans", bold: true);
        var glyphs = "Façade Ωμέγα Жук".EnumerateRunes().Select(r => font.GetGlyphIndex(r.Value)).Distinct().ToList();

        var subset = TrueTypeSubsetter.Subset(font, glyphs);
        var reloaded = TrueTypeFont.Load(subset.Data);

        // Accented letters are composites: their components come along.
        Assert.Equal(subset.GlyphMap.Count, reloaded.GlyphCount);
        Assert.True(reloaded.GlyphCount >= glyphs.Union([0]).Count());
        Assert.True(subset.Data.Length < font.Data.Length / 10);
        foreach (var glyph in glyphs)
        {
            var mapped = subset.GlyphMap[glyph];
            Assert.Equal(font.GetAdvanceWidth(glyph), reloaded.GetAdvanceWidth(mapped));
            Assert.Equal(Flatten(font.Glyphs!.GetOutline(glyph)), Flatten(reloaded.Glyphs!.GetOutline(mapped)));
        }

        Assert.Equal(0xB1B0AFBAu, FileChecksum(subset.Data));
    }

    [Fact]
    public void Subset_of_a_composite_glyph_carries_its_components()
    {
        var font = FontLibrary.Default.Resolve("Liberation Serif");
        var composite = Enumerable.Range(0, font.GlyphCount).First(g => font.Glyphs!.Components(g).Count > 0);

        var subset = TrueTypeSubsetter.Subset(font, [composite]);
        var reloaded = TrueTypeFont.Load(subset.Data);

        Assert.True(reloaded.GlyphCount > 2);
        Assert.Equal(Flatten(font.Glyphs!.GetOutline(composite)), Flatten(reloaded.Glyphs!.GetOutline(subset.GlyphMap[composite])));
    }

    [Fact]
    public void Registered_fonts_take_precedence_and_corrupt_fonts_are_rejected()
    {
        var library = new FontLibrary();
        var data = FontLibrary.Bundled("Caladea", false, false).Data;

        library.Register(data, alias: "Corporate");

        Assert.Equal("Caladea", library.Resolve("corporate").Names.Family);
        Assert.Equal("Liberation Serif", library.Resolve("Unknown Family").Names.Family);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new byte[64]));
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(data[..200]));
        Assert.Contains("SIL OPEN FONT LICENSE", FontLibrary.BundledLicence("LiberationSans"));
    }

    private static List<string> Flatten(GlyphOutline outline) =>
        outline.Contours.SelectMany(c => c.Select(p => $"{p.X},{p.Y},{p.OnCurve}").Append("|")).ToList();

    private static uint FileChecksum(byte[] data)
    {
        uint sum = 0;
        for (var i = 0; i + 3 < data.Length; i += 4)
        {
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i)));
        }

        return sum;
    }

    [Fact]
    public void Byte_and_trimmed_cmap_subtables_map_codes()
    {
        // A cmap table (OpenType "cmap") with one Macintosh subtable, format 0 then format 6.
        var byteTable = new byte[12 + 6 + 256];
        WriteCmapHeader(byteTable, platform: 1, encoding: 0);
        BinaryPrimitives.WriteUInt16BigEndian(byteTable.AsSpan(12), 0);
        byteTable[18 + 'A'] = 7;
        var trimmed = new byte[12 + 10 + 4];
        WriteCmapHeader(trimmed, platform: 1, encoding: 0);
        BinaryPrimitives.WriteUInt16BigEndian(trimmed.AsSpan(12), 6);
        BinaryPrimitives.WriteUInt16BigEndian(trimmed.AsSpan(18), 0x41);
        BinaryPrimitives.WriteUInt16BigEndian(trimmed.AsSpan(20), 2);
        BinaryPrimitives.WriteUInt16BigEndian(trimmed.AsSpan(22), 9);
        BinaryPrimitives.WriteUInt16BigEndian(trimmed.AsSpan(24), 10);

        var format0 = new TrueTypeCmap(new FontReader(byteTable), new FontTable(0, byteTable.Length));
        var format6 = new TrueTypeCmap(new FontReader(trimmed), new FontTable(0, trimmed.Length));

        Assert.Equal((7, 0), (format0.Lookup('A'), format0.Lookup('B')));
        Assert.Equal((9, 10, 0), (format6.Lookup('A'), format6.Lookup('B'), format6.Lookup('C')));
    }

    [Fact]
    public void Segmented_coverage_cmap_maps_codes_beyond_the_basic_plane()
    {
        // A Windows full-Unicode subtable (3, 10), format 12, with two groups: U+1F600-U+1F602 from glyph 50, U+0041 to 3.
        var table = new byte[12 + 16 + (2 * 12)];
        WriteCmapHeader(table, platform: 3, encoding: 10);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(12), 12);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(24), 2);
        uint[] groups = [0x1F600, 0x1F602, 50, 0x41, 0x41, 3];
        for (var i = 0; i < groups.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(28 + (i * 4)), groups[i]);
        }

        var cmap = new TrueTypeCmap(new FontReader(table), new FontTable(0, table.Length));

        Assert.Equal((50, 52, 0, 3), (cmap.Lookup(0x1F600), cmap.Lookup(0x1F602), cmap.Lookup(0x1F603), cmap.Lookup('A')));
        Assert.False(cmap.IsSymbol);
    }

    [Fact]
    public void Overlapping_segments_stop_once_the_code_space_is_covered()
    {
        // Format 4 with 32,767 identical segments 0-65534 (delta 1): the first one maps every code, the others
        // would walk the same codes again, two billion steps in all.
        const int segments = 32767;
        var table = new byte[12 + 16 + (segments * 8)];
        WriteCmapHeader(table, platform: 3, encoding: 1);
        var span = table.AsSpan(12);
        BinaryPrimitives.WriteUInt16BigEndian(span, 4);
        BinaryPrimitives.WriteUInt16BigEndian(span[6..], segments * 2);
        for (var s = 0; s < segments; s++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(span[(14 + (s * 2))..], 0xFFFE);
            BinaryPrimitives.WriteUInt16BigEndian(span[(16 + (segments * 2) + (s * 2))..], 0);
            BinaryPrimitives.WriteInt16BigEndian(span[(16 + (segments * 4) + (s * 2))..], 1);
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var cmap = new TrueTypeCmap(new FontReader(table), new FontTable(0, table.Length));

        Assert.Equal((0x42, 0xFFFE), (cmap.Lookup('A'), cmap.Lookup(0xFFFD)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.Elapsed.ToString());
    }

    [Fact]
    public void Overlapping_groups_stop_once_every_code_point_is_covered()
    {
        // Format 12 with 1,000 groups 0-0xFFFFF from glyph 1: a billion steps without the bound.
        const int groups = 1000;
        var table = new byte[12 + 16 + (groups * 12)];
        WriteCmapHeader(table, platform: 3, encoding: 10);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(12), 12);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(24), groups);
        for (var g = 0; g < groups; g++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(28 + (g * 12) + 4), 0xFFFFE);
            BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(28 + (g * 12) + 8), 1);
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var cmap = new TrueTypeCmap(new FontReader(table), new FontTable(0, table.Length));

        Assert.Equal((0x42, 0x1F601), (cmap.Lookup('A'), cmap.Lookup(0x1F600)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.Elapsed.ToString());
    }

    private static void WriteCmapHeader(byte[] table, int platform, int encoding)
    {
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(4), (ushort)platform);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(6), (ushort)encoding);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(8), 12);
    }
}
