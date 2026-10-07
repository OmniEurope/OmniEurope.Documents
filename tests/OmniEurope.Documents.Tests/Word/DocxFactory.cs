// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>Hand-written minimal packages, to test markup our own writer never produces.</summary>
internal static class DocxFactory
{
    public const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public const string MainType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    public static byte[] Build(string body, string mainType = MainType, IReadOnlyDictionary<string, string>? extraParts = null, string documentRelationships = "", string prolog = "", IReadOnlyDictionary<string, byte[]>? binaryParts = null, IReadOnlyDictionary<string, string>? contentTypes = null)
    {
        var overrides = string.Concat((contentTypes ?? new Dictionary<string, string>()).Select(c => "<Override PartName=\"/" + c.Key + "\" ContentType=\"" + c.Value + "\"/>"));
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", $"""<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/word/document.xml" ContentType="{mainType}"/>{overrides}</Types>""");
            Add(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
            Add(zip, "word/document.xml", $"""<?xml version="1.0" encoding="UTF-8"?>{prolog}<w:document xmlns:w="{W}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"><w:body>{body}</w:body></w:document>""");
            Add(zip, "word/_rels/document.xml.rels", $"""<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{documentRelationships}</Relationships>""");
            foreach (var (name, content) in extraParts ?? new Dictionary<string, string>())
            {
                Add(zip, name, content);
            }

            foreach (var (name, content) in binaryParts ?? new Dictionary<string, byte[]>())
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(Encoding.UTF8.GetBytes(content));
    }

    /// <summary>The entries of a package with their uncompressed bytes.</summary>
    public static Dictionary<string, byte[]> Entries(byte[] package)
    {
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var input = e.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        });
    }

    public static string Part(byte[] package, string name) => Encoding.UTF8.GetString(Entries(package)[name]);
}
