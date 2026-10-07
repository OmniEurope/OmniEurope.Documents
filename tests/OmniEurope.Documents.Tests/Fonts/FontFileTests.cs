// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using System.Text;
using OmniEurope.Documents.Fonts;

namespace OmniEurope.Documents.Tests.Fonts;

/// <summary>
/// Font files built from a bundled face by editing its table directory (OpenType specification): a TrueType
/// collection header, a zero unitsPerEm, a missing post table, an OS/2 licence that forbids embedding.
/// </summary>
public sealed class FontFileTests
{
    [Fact]
    public void Data_that_is_not_a_font_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load([1, 2, 3]));
    }

    [Fact]
    public void A_collection_gives_its_faces_by_index()
    {
        var face = Regular();

        // "ttcf", version 1.0, one font, its offset table at 16; every table offset moves by those 16 bytes.
        var shifted = (byte[])face.Clone();
        foreach (var (at, _) in Tables(face))
        {
            BinaryPrimitives.WriteUInt32BigEndian(shifted.AsSpan(at + 8), BinaryPrimitives.ReadUInt32BigEndian(face.AsSpan(at + 8)) + 16);
        }

        byte[] collection = [.. "ttcf"u8, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 16, .. shifted];

        var font = TrueTypeFont.Load(collection);
        var original = TrueTypeFont.Load(face);

        Assert.Equal(original.GetGlyphIndex('A'), font.GetGlyphIndex('A'));
        Assert.True(font.HasTable("glyf"));
        Assert.Equal(original.GetGlyphIndex('A'), font.CharacterMap['A']);
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Load(collection, 1));
    }

    [Fact]
    public void A_zero_unit_square_is_refused()
    {
        var face = Regular();
        BinaryPrimitives.WriteUInt16BigEndian(face.AsSpan(Table(face, "head") + 18), 0);

        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(face));
    }

    [Fact]
    public void Without_a_post_table_the_underline_takes_default_metrics()
    {
        var face = Regular();
        var (at, _) = Tables(face).Single(t => t.Tag == "post");
        Encoding.ASCII.GetBytes("xost").CopyTo(face, at);

        var font = TrueTypeFont.Load(face);

        Assert.Equal((-font.UnitsPerEm / 10, font.UnitsPerEm / 20), ((int)font.UnderlinePosition, (int)font.UnderlineThickness));
    }

    [Fact]
    public void A_restricted_licence_forbids_subsetting()
    {
        var face = Regular();
        BinaryPrimitives.WriteUInt16BigEndian(face.AsSpan(Table(face, "OS/2") + 8), 0x0002);

        var font = TrueTypeFont.Load(face);

        Assert.False(font.EmbeddingAllowed);
        Assert.Throws<NotSupportedException>(() => TrueTypeSubsetter.Subset(font, [0, font.GetGlyphIndex('A')]));
    }

    private static byte[] Regular()
    {
        var assembly = typeof(TrueTypeFont).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("LiberationSans-Regular.ttf", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    // The table records of the directory: where each record is and its tag.
    private static IEnumerable<(int At, string Tag)> Tables(byte[] font)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        return Enumerable.Range(0, count).Select(i => (12 + (16 * i), Encoding.ASCII.GetString(font, 12 + (16 * i), 4)));
    }

    private static int Table(byte[] font, string tag) =>
        (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(Tables(font).Single(t => t.Tag == tag).At + 8));
}
