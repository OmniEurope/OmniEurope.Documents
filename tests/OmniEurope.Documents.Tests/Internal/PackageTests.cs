// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Tests.Internal;

/// <summary>
/// Open Packaging Conventions (ECMA-376 part 2) and the zip limits: duplicate or missing parts, content types
/// by extension, relationship parts created on demand, relative targets, Office text escapes, and archives
/// with too many entries, oversized or suspiciously compressed parts.
/// </summary>
public sealed class PackageTests
{
    private const string Types = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>";

    [Fact]
    public void Packages_need_one_content_types_part_and_unique_names()
    {
        Assert.Throws<DocumentFormatException>(() => OpcPackage.Open(Zip(("a.xml", "<a/>")), null));
        Assert.Throws<DocumentFormatException>(() => OpcPackage.Open(Zip(("[Content_Types].xml", Types), ("a.xml", "<a/>"), ("a.xml", "<b/>")), null));
    }

    [Fact]
    public void Content_types_relationships_and_names_are_added_as_needed()
    {
        var package = OpcPackage.Open(Zip(("[Content_Types].xml", Types)), null);

        package.SetBytes("media/clip.bin", [1, 2], "application/x-clip");
        var first = package.AddRelationship("doc.xml", "urn:a", "media/clip.bin");
        var rels = package.GetXml("_rels/doc.xml.rels")!;
        rels.Root!.Add(new XElement(rels.Root.Name.Namespace + "Relationship", new XAttribute("Id", "rId3"), new XAttribute("Type", "urn:b"), new XAttribute("Target", "x")));
        var next = package.AddRelationship("doc.xml", "urn:c", "y");

        Assert.Contains("Extension=\"bin\"", package.GetXml("[Content_Types].xml")!.ToString(), StringComparison.Ordinal);
        Assert.Equal("application/x-clip", package.ContentTypeOf("media/other.bin"));
        Assert.Equal(("rId1", "rId4"), (first, next));
        Assert.Equal("media/clip.bin", package.Relationships("doc.xml")[0].Target);
    }

    [Theory]
    [InlineData("word", "../../../media/a.png", "media/a.png")]
    [InlineData("word/sub", "./../b.xml", "word/b.xml")]
    [InlineData("word", "/root.xml", "root.xml")]
    public void Relative_targets_resolve_and_stop_at_the_root(string directory, string target, string resolved)
    {
        Assert.Equal(resolved, OpcRelationships.Resolve(directory, target));
    }

    [Fact]
    public void Office_text_escapes_keep_pairs_and_replace_what_xml_cannot_hold()
    {
        // An emoji pair stays; U+0001 becomes _x0001_; an existing escape sequence is protected; a lone surrogate is U+FFFD.
        var text = OpenXmlPackageWriter.EscapeOfficeText("a\U0001F600\u0001_x0041_\uD800");

        Assert.Equal("a\U0001F600_x0001__x005F_x0041_�", text);
    }

    [Fact]
    public void Archives_beyond_the_limits_are_refused()
    {
        // The ratio is only checked above 1 MiB; the total size counts what was really inflated.
        var zeros = Encoding.ASCII.GetString(new byte[2_100_000]);

        Assert.Throws<DocumentFormatException>(() => OpcPackage.Open(Zip(("[Content_Types].xml", Types), ("a.xml", "<a/>")), new PackageLimits { MaxEntries = 1 }));
        Assert.Throws<DocumentFormatException>(() => OpcPackage.Open(Zip(("[Content_Types].xml", Types), ("big.xml", "<a>" + new string('a', 5000) + "</a>")), new PackageLimits { MaxPartSize = 1000 }));
        Assert.Throws<DocumentFormatException>(() => OpcPackage.Open(Zip(("[Content_Types].xml", Types), ("zeros.bin", zeros)), new PackageLimits { MaxCompressionRatio = 10 }));
        Assert.Throws<DocumentFormatException>(() => OpcPackage.Open(Zip(("[Content_Types].xml", Types), ("a.xml", new string('a', 600)), ("b.xml", new string('b', 600))), new PackageLimits { MaxTotalSize = 1000 }));
    }

    [Fact]
    public async Task A_prefixed_stream_reads_its_prefix_then_the_inner_stream()
    {
        using var stream = new PrefixedReadStream([9, 1, 2, 9], 1, 2, new MemoryStream([3, 4]), leaveOpen: false);

        var buffer = new byte[4];
        var read = await stream.ReadAsync(buffer, 0, 4, TestContext.Current.CancellationToken);
        read += stream.Read(buffer, read, 4 - read);

        Assert.Equal((4, new byte[] { 1, 2, 3, 4 }), (read, buffer), Comparer.Instance);
        Assert.False(stream.CanSeek || stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
        stream.Flush();
    }

    private static MemoryStream Zip(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        stream.Position = 0;
        return stream;
    }

    private sealed class Comparer : IEqualityComparer<(int, byte[])>
    {
        public static readonly Comparer Instance = new();

        public bool Equals((int, byte[]) x, (int, byte[]) y) => x.Item1 == y.Item1 && x.Item2.SequenceEqual(y.Item2);

        public int GetHashCode((int, byte[]) obj) => obj.Item1;
    }
}
