// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Security.Cryptography;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>
/// Appends an incremental update to an existing file (ISO 32000-1 §7.5.6): the original bytes are kept,
/// new and replaced objects follow, then a cross-reference section in the same style as the last one
/// (table or stream) whose trailer points back to the previous section.
/// </summary>
internal static class PdfIncrementalWriter
{
    public static byte[] Append(byte[] original, PdfObjectStore store, IReadOnlyList<(int Number, int Generation, PdfObject Value)> objects)
    {
        using var output = new MemoryStream();
        output.Write(original);
        if (original.Length > 0 && original[^1] is not ((byte)'\n' or (byte)'\r'))
        {
            output.WriteByte((byte)'\n');
        }

        var offsets = new SortedDictionary<int, (long Offset, int Generation)>();
        foreach (var (number, generation, value) in objects)
        {
            offsets[number] = (output.Position, generation);
            PdfSerializer.Ascii(output, $"{number.ToString(CultureInfo.InvariantCulture)} {generation.ToString(CultureInfo.InvariantCulture)} obj\n");
            PdfSerializer.Write(value, output);
            PdfSerializer.Ascii(output, "\nendobj\n");
        }

        var size = Math.Max(store.NextObjectNumber, offsets.Keys.DefaultIfEmpty(0).Max() + 1);
        var id = (store.Trailer["ID"] as PdfArray)?.Items.FirstOrDefault() as PdfString ?? new PdfString(new byte[16], hex: true);
        var newId = new PdfString(SHA256.HashData(output.GetBuffer().AsSpan(0, (int)output.Length))[..16], hex: true);
        var trailer = new PdfDictionary()
            .SetNumber("Prev", store.StartXref)
            .Set("Root", store.Trailer["Root"])
            .Set("Info", store.Trailer["Info"])
            .Set("ID", new PdfArray(id, newId));
        if (store.StreamXref)
        {
            WriteStream(output, offsets, size, trailer);
        }
        else
        {
            WriteTable(output, offsets, size, trailer);
        }

        return output.ToArray();
    }

    private static void WriteTable(MemoryStream output, SortedDictionary<int, (long Offset, int Generation)> offsets, int size, PdfDictionary trailer)
    {
        var xref = output.Position;
        PdfSerializer.Ascii(output, "xref\n");
        foreach (var run in Runs(offsets.Keys))
        {
            PdfSerializer.Ascii(output, $"{run.First.ToString(CultureInfo.InvariantCulture)} {run.Count.ToString(CultureInfo.InvariantCulture)}\n");
            for (var number = run.First; number < run.First + run.Count; number++)
            {
                var (offset, generation) = offsets[number];
                PdfSerializer.Ascii(output, $"{offset.ToString("D10", CultureInfo.InvariantCulture)} {generation.ToString("D5", CultureInfo.InvariantCulture)} n \n");
            }
        }

        PdfSerializer.Ascii(output, "trailer\n");
        PdfSerializer.Write(trailer.SetNumber("Size", size), output);
        PdfSerializer.Ascii(output, $"\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
    }

    private static void WriteStream(MemoryStream output, SortedDictionary<int, (long Offset, int Generation)> offsets, int size, PdfDictionary trailer)
    {
        var number = size;
        var xref = output.Position;
        offsets[number] = (xref, 0);
        var rows = new List<byte>();
        foreach (var (_, (offset, generation)) in offsets)
        {
            rows.Add(1);
            rows.AddRange([(byte)(offset >> 24), (byte)(offset >> 16), (byte)(offset >> 8), (byte)offset]);
            rows.AddRange([(byte)(generation >> 8), (byte)generation]);
        }

        var index = new PdfArray(Runs(offsets.Keys).SelectMany(r => new PdfObject[] { PdfNumber.Of(r.First), PdfNumber.Of(r.Count) }));
        var stream = new PdfStream([.. rows]);
        foreach (var (key, value) in trailer.Entries)
        {
            stream.Set(key, value);
        }

        stream.SetName("Type", "XRef").SetNumber("Size", number + 1).Set("Index", index).Set("W", PdfArray.OfNumbers(1, 4, 2));
        PdfSerializer.Ascii(output, $"{number.ToString(CultureInfo.InvariantCulture)} 0 obj\n");
        PdfSerializer.Write(stream, output);
        PdfSerializer.Ascii(output, $"\nendobj\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
    }

    private static List<(int First, int Count)> Runs(IEnumerable<int> numbers)
    {
        var runs = new List<(int First, int Count)>();
        foreach (var number in numbers.Order())
        {
            if (runs.Count > 0 && runs[^1].First + runs[^1].Count == number)
            {
                runs[^1] = (runs[^1].First, runs[^1].Count + 1);
            }
            else
            {
                runs.Add((number, 1));
            }
        }

        return runs;
    }
}
