// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>Where an object lives: at a byte offset, or at an index inside an object stream.</summary>
internal readonly record struct XrefEntry(long Offset, int Generation, int StreamNumber, int StreamIndex)
{
    public bool InStream => StreamNumber > 0;
}

/// <summary>
/// Loads the cross-reference information: classic tables, cross-reference streams, hybrid files and the
/// chain of incremental updates (newest entry wins). When the structure is broken, a full scan of the file
/// for <c>n g obj</c> headers and trailers rebuilds it.
/// </summary>
internal static class PdfXref
{
    public static (Dictionary<int, XrefEntry> Entries, PdfDictionary Trailer, bool Repaired, long StartXref, bool StreamXref) Load(byte[] data)
    {
        try
        {
            var entries = new Dictionary<int, XrefEntry>();
            var trailer = new PdfDictionary();
            var start = FindStartXref(data);
            var last = start ?? 0;
            var lastIsStream = last > 0 && last + 4 <= data.Length && !data.AsSpan((int)last, 4).SequenceEqual("xref"u8);
            var visited = new HashSet<long>();
            while (start is { } offset && offset > 0 && offset < data.Length && visited.Add(offset))
            {
                var section = ReadSection(data, offset, entries);
                MergeTrailer(trailer, section);
                start = section["Prev"] is PdfNumber prev ? (long)prev.Value : null;
            }

            if (trailer["Root"] is PdfReference && entries.Count > 0)
            {
                return (entries, trailer, false, last, lastIsStream);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidCastException or IndexOutOfRangeException or ArgumentException or NotSupportedException)
        {
            // Fall through to the repair scan.
        }

        var (scanned, scannedTrailer) = Scan(data);
        return (scanned, scannedTrailer, true, 0, false);
    }

    private static long? FindStartXref(byte[] data)
    {
        var from = Math.Max(0, data.Length - 4096);
        var index = data.AsSpan(from).LastIndexOf("startxref"u8);
        if (index < 0)
        {
            return null;
        }

        var lexer = new PdfLexer(data, from + index + 9);
        return lexer.Next().Value is PdfNumber n ? (long)n.Value : null;
    }

    private static void MergeTrailer(PdfDictionary trailer, PdfDictionary section)
    {
        foreach (var (key, value) in section.Entries)
        {
            if (key is not ("Prev" or "XRefStm" or "Type" or "W" or "Index" or "Filter" or "DecodeParms" or "Length") && !trailer.ContainsKey(key))
            {
                trailer.Set(key, value);
            }
        }
    }

    private static PdfDictionary ReadSection(byte[] data, long offset, Dictionary<int, XrefEntry> entries)
    {
        var lexer = new PdfLexer(data, offset);
        var first = lexer.Next();
        if (first.Text == "xref")
        {
            var trailer = ReadTable(lexer, entries);
            if (trailer["XRefStm"] is PdfNumber stream)
            {
                // Hybrid file: the stream's entries count, but those of this table come first.
                ReadStreamSection(data, (long)stream.Value, entries);
            }

            return trailer;
        }

        return ReadStreamSection(data, offset, entries);
    }

    private static PdfDictionary ReadTable(PdfLexer lexer, Dictionary<int, XrefEntry> entries)
    {
        while (true)
        {
            var token = lexer.Next();
            if (token.Text == "trailer")
            {
                return new PdfParser(lexer).ReadObject() as PdfDictionary ?? throw new InvalidDataException("Bad trailer.");
            }

            if (token.Kind != PdfTokenKind.Number)
            {
                throw new InvalidDataException("Bad cross-reference table.");
            }

            var firstNumber = ((PdfNumber)token.Value!).IntValue;
            var count = lexer.Next().Value is PdfNumber c ? c.IntValue : throw new InvalidDataException("Bad cross-reference subsection.");
            for (var i = 0; i < count; i++)
            {
                var offset = lexer.Next();
                var generation = lexer.Next();
                var kind = lexer.Next();
                if (offset.Kind != PdfTokenKind.Number || generation.Kind != PdfTokenKind.Number)
                {
                    throw new InvalidDataException("Bad cross-reference entry.");
                }

                var number = firstNumber + i;
                if (kind.Text == "n" && !entries.ContainsKey(number))
                {
                    entries[number] = new XrefEntry((long)((PdfNumber)offset.Value!).Value, ((PdfNumber)generation.Value!).IntValue, 0, 0);
                }
                else if (kind.Text == "f")
                {
                    entries.TryAdd(number, new XrefEntry(-1, 0, 0, 0));
                }
            }
        }
    }

    private static PdfDictionary ReadStreamSection(byte[] data, long offset, Dictionary<int, XrefEntry> entries)
    {
        var parser = new PdfParser(new PdfLexer(data, offset));
        var stream = parser.ReadIndirect() is { Value: PdfStream { } found } && found["Type"] is PdfName { Value: "XRef" }
            ? found
            : throw new InvalidDataException("No cross-reference stream at the expected offset.");
        var widths = (stream["W"] as PdfArray)?.Items.Select(i => ((PdfNumber)i).IntValue).ToArray() ?? throw new InvalidDataException("XRef stream without W.");
        // Three field widths of at most eight bytes, at least one non-zero: an empty row would never advance.
        if (widths.Length != 3 || widths.Any(w => w is < 0 or > 8) || widths.Sum() == 0)
        {
            throw new InvalidDataException("Bad cross-reference stream widths.");
        }

        var size = stream["Size"] is PdfNumber s ? s.IntValue : 0;
        var index = (stream["Index"] as PdfArray)?.Items.Select(i => ((PdfNumber)i).IntValue).ToArray() ?? [0, size];
        var (decoded, _, _) = PdfFilters.Decode(stream.Data, PdfStreams.FilterNames(stream), PdfStreams.FilterParameters(stream));
        var rowSize = widths.Sum();
        var position = 0;
        for (var pair = 0; pair + 1 < index.Length; pair += 2)
        {
            for (var i = 0; i < index[pair + 1] && position + rowSize <= decoded.Length; i++, position += rowSize)
            {
                // A newer section was read first: its entry wins.
                entries.TryAdd(index[pair] + i, Entry(decoded, position, widths));
            }
        }

        return stream;
    }

    // One row: the type (1 when its field is absent), then an offset and generation (type 1) or an object
    // stream and index (type 2); any other type is a free entry.
    private static XrefEntry Entry(byte[] decoded, int position, int[] widths)
    {
        var type = widths[0] == 0 ? 1 : Field(decoded, position, widths[0]);
        var second = Field(decoded, position + widths[0], widths[1]);
        var third = Field(decoded, position + widths[0] + widths[1], widths[2]);
        return type switch
        {
            1 => new XrefEntry(second, (int)third, 0, 0),
            2 => new XrefEntry(0, 0, (int)second, (int)third),
            _ => new XrefEntry(-1, 0, 0, 0),
        };
    }

    private static long Field(byte[] data, int position, int width)
    {
        long value = 0;
        for (var i = 0; i < width; i++)
        {
            value = (value << 8) | data[position + i];
        }

        return value;
    }

    // Rebuilds the table from the objects actually present in the file.
    private static (Dictionary<int, XrefEntry>, PdfDictionary) Scan(byte[] data)
    {
        var entries = new Dictionary<int, XrefEntry>();
        var trailer = new PdfDictionary();
        var text = Encoding.Latin1.GetString(data);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, @"(?<![0-9])(\d{1,10})\s+(\d{1,5})\s+obj\b"))
        {
            if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                entries[number] = new XrefEntry(match.Index, int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), 0, 0);
            }
        }

        var position = 0;
        while ((position = text.IndexOf("trailer", position, StringComparison.Ordinal)) >= 0)
        {
            position += 7;
            if (new PdfParser(new PdfLexer(data, position)).ReadObject() is PdfDictionary dictionary)
            {
                foreach (var (key, value) in dictionary.Entries)
                {
                    trailer.Set(key, value);
                }
            }
        }

        return (entries, trailer);
    }
}

/// <summary>Filter names and parameters of a stream dictionary.</summary>
internal static class PdfStreams
{
    public static IReadOnlyList<string> FilterNames(PdfDictionary stream) => stream["Filter"] switch
    {
        PdfName name => [name.Value],
        PdfArray array => array.Items.OfType<PdfName>().Select(n => n.Value).ToList(),
        _ => [],
    };

    public static IReadOnlyList<PdfDictionary?> FilterParameters(PdfDictionary stream) => stream["DecodeParms"] switch
    {
        PdfDictionary single => [single],
        PdfArray array => array.Items.Select(i => i as PdfDictionary).ToList(),
        _ => [],
    };
}
