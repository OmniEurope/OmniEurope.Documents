// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Security.Cryptography;

namespace OmniEurope.Documents.Pdf.Objects;

/// <summary>
/// The indirect objects of a PDF being written, numbered from 1, and the writer of the complete file:
/// header, objects, cross-reference table, trailer. The file identifier is a hash of the body, so equal
/// documents produce equal bytes.
/// </summary>
internal sealed class PdfObjectTable(int firstNumber = 1)
{
    private readonly List<PdfObject?> _objects = [];

    public int FirstNumber => firstNumber;

    /// <summary>The objects added, with their numbers.</summary>
    public IEnumerable<(int Number, PdfObject Value)> Objects => _objects.Select((o, i) => (firstNumber + i, o ?? PdfNull.Instance));

    public int Count => _objects.Count;

    public PdfReference Add(PdfObject value)
    {
        _objects.Add(value);
        return new PdfReference(firstNumber + _objects.Count - 1, 0);
    }

    public PdfReference Reserve()
    {
        _objects.Add(null);
        return new PdfReference(firstNumber + _objects.Count - 1, 0);
    }

    public void Set(PdfReference reference, PdfObject value) => _objects[reference.Number - firstNumber] = value;

    public PdfObject? Get(PdfReference reference) =>
        reference.Number >= firstNumber && reference.Number < firstNumber + _objects.Count ? _objects[reference.Number - firstNumber] : null;

    public void Write(Stream output, PdfReference root, PdfReference? info, string version = "1.7")
    {
        using var body = new MemoryStream();
        PdfSerializer.Ascii(body, $"%PDF-{version}\n");
        body.Write([(byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);
        var offsets = new long[_objects.Count];
        for (var i = 0; i < _objects.Count; i++)
        {
            offsets[i] = body.Position;
            PdfSerializer.Ascii(body, (i + 1).ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
            PdfSerializer.Write(_objects[i] ?? PdfNull.Instance, body);
            PdfSerializer.Ascii(body, "\nendobj\n");
        }

        var id = SHA256.HashData(body.GetBuffer().AsSpan(0, (int)body.Length))[..16];
        var xref = body.Position;
        PdfSerializer.Ascii(body, $"xref\n0 {_objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            PdfSerializer.Ascii(body, offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        var trailer = new PdfDictionary()
            .SetNumber("Size", _objects.Count + 1)
            .Set("Root", root)
            .Set("Info", info)
            .Set("ID", new PdfArray(new PdfString(id, hex: true), new PdfString(id, hex: true)));
        PdfSerializer.Ascii(body, "trailer\n");
        PdfSerializer.Write(trailer, body);
        PdfSerializer.Ascii(body, $"\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        body.Position = 0;
        body.CopyTo(output);
    }
}
