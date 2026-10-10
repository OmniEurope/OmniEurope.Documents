// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>Writes small PDF files object by object, for markup our own writer never produces.</summary>
internal static class RawPdf
{
    /// <summary>A one-page document; objects are numbered from 1 in order, object 1 being the catalog.</summary>
    public static byte[] Page(string content, string resources = "", string pageExtra = "", params object[] extraObjects)
    {
        var objects = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << {resources} >> /Contents 4 0 R {pageExtra} >>"),
            Stream(string.Empty, Ascii(content)),
        };
        objects.AddRange(extraObjects.Select(o => o is byte[] bytes ? bytes : Ascii((string)o)));
        return Build(objects);
    }

    /// <summary>A document of the given objects, numbered from 1 in order, object 1 being the catalog.</summary>
    public static byte[] Objects(params object[] objects) => Build(objects.Select(o => o is byte[] bytes ? bytes : Ascii((string)o)).ToList());

    public static byte[] Stream(string dictionary, byte[] data) => [.. Ascii($"<< /Length {data.Length} {dictionary} >>\nstream\n"), .. data, .. Ascii("\nendstream")];

    public static byte[] Ascii(string text) => Encoding.Latin1.GetBytes(text);

    private static byte[] Build(List<byte[]> objects)
    {
        using var output = new MemoryStream();
        output.Write(Ascii("%PDF-1.7\n"));
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            output.Write(Ascii($"{i + 1} 0 obj\n"));
            output.Write(objects[i]);
            output.Write(Ascii("\nendobj\n"));
        }

        var xref = output.Position;
        output.Write(Ascii($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            output.Write(Ascii(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n"));
        }

        output.Write(Ascii($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return output.ToArray();
    }
}
