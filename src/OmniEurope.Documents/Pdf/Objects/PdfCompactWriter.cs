// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Pdf.Objects;

/// <summary>
/// Writes a complete file in the compact form of PDF 1.5: every non-stream object goes into compressed
/// object streams (up to 100 objects each) and the cross-reference information is itself a compressed
/// stream. Far smaller than one object per line plus a text table when a file has many small objects.
/// </summary>
internal static class PdfCompactWriter
{
    private const int ObjectsPerStream = 100;

    public static void Write(PdfObjectTable table, Stream output, PdfReference root, PdfReference? info)
    {
        var objects = table.Objects.ToList();
        var nextNumber = table.FirstNumber + table.Count;
        var plain = objects.Where(o => o.Value is not PdfStream).ToList();
        var streams = objects.Where(o => o.Value is PdfStream).ToList();
        var locations = new Dictionary<int, (int Type, long Field2, int Field3)>();
        using var body = new MemoryStream();
        PdfSerializer.Ascii(body, "%PDF-1.7\n");
        body.Write([(byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);
        foreach (var (number, value) in streams)
        {
            locations[number] = (1, body.Position, 0);
            WriteObject(body, number, value);
        }

        foreach (var chunk in plain.Chunk(ObjectsPerStream))
        {
            var streamNumber = nextNumber++;
            var (data, first) = Pack(chunk);
            var objectStream = new PdfStream(PngCodec.Deflate(data, CompressionLevel.SmallestSize))
                .SetName("Type", "ObjStm").SetNumber("N", chunk.Length).SetNumber("First", first).SetName("Filter", "FlateDecode");
            locations[streamNumber] = (1, body.Position, 0);
            WriteObject(body, streamNumber, objectStream);
            for (var i = 0; i < chunk.Length; i++)
            {
                locations[chunk[i].Number] = (2, streamNumber, i);
            }
        }

        var xrefNumber = nextNumber;
        var xrefOffset = body.Position;
        locations[xrefNumber] = (1, xrefOffset, 0);
        var size = xrefNumber + 1;
        var rows = new byte[size * 7];
        rows[0] = 0;
        rows[5] = 0xFF;
        rows[6] = 0xFF;
        foreach (var (number, (type, field2, field3)) in locations)
        {
            var row = number * 7;
            rows[row] = (byte)type;
            for (var k = 0; k < 4; k++)
            {
                rows[row + 1 + k] = (byte)(field2 >> (24 - (8 * k)));
            }

            rows[row + 5] = (byte)(field3 >> 8);
            rows[row + 6] = (byte)field3;
        }

        var id = SHA256.HashData(body.GetBuffer().AsSpan(0, (int)body.Length))[..16];
        var xref = new PdfStream(PngCodec.Deflate(rows, CompressionLevel.SmallestSize))
            .SetName("Type", "XRef").SetNumber("Size", size).Set("W", PdfArray.OfNumbers(1, 4, 2)).SetName("Filter", "FlateDecode")
            .Set("Root", root).Set("Info", info)
            .Set("ID", new PdfArray(new PdfString(id, hex: true), new PdfString(id, hex: true)));
        WriteObject(body, xrefNumber, xref);
        PdfSerializer.Ascii(body, $"startxref\n{xrefOffset.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        body.Position = 0;
        body.CopyTo(output);
    }

    private static (byte[] Data, int First) Pack((int Number, PdfObject Value)[] chunk)
    {
        using var objects = new MemoryStream();
        var header = new System.Text.StringBuilder();
        foreach (var (number, value) in chunk)
        {
            header.Append(number.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(objects.Position.ToString(CultureInfo.InvariantCulture)).Append(' ');
            PdfSerializer.Write(value, objects);
            objects.WriteByte((byte)'\n');
        }

        var headerBytes = System.Text.Encoding.ASCII.GetBytes(header.Append('\n').ToString());
        return ([.. headerBytes, .. objects.ToArray()], headerBytes.Length);
    }

    private static void WriteObject(Stream body, int number, PdfObject value)
    {
        PdfSerializer.Ascii(body, number.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
        PdfSerializer.Write(value, body);
        PdfSerializer.Ascii(body, "\nendobj\n");
    }
}
