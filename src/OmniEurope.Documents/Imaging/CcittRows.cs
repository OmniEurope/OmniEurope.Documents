// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>
/// One fax row as a list of changing elements (positions where the colour changes, starting white), read
/// in one-dimensional (Modified Huffman) or two-dimensional (READ) coding against the previous row.
/// </summary>
internal static class CcittRows
{
    public static int[] Read1D(CcittBitReader reader, int columns)
    {
        var changes = new List<int>();
        var position = 0;
        var white = true;
        while (position < columns)
        {
            position = Math.Min(columns, position + ReadRun(reader, white));
            changes.Add(position);
            white = !white;
        }

        return Finish(changes, columns);
    }

    public static int[] Read2D(CcittBitReader reader, int columns, int[] reference)
    {
        var changes = new List<int>();
        var a0 = -1;
        var white = true;
        while (a0 < columns)
        {
            var (b1, b2) = FindB(reference, a0, white, columns);
            switch (ReadMode(reader))
            {
                case Mode.Pass:
                    a0 = b2;
                    break;
                case Mode.Horizontal:
                    var start = Math.Max(a0, 0);
                    var a1 = Math.Min(columns, start + ReadRun(reader, white));
                    var a2 = Math.Min(columns, a1 + ReadRun(reader, !white));
                    changes.Add(a1);
                    changes.Add(a2);
                    a0 = a2;
                    break;
                case var vertical:
                    var position = b1 + (int)vertical;
                    if (position < Math.Max(a0, 0) || position > columns)
                    {
                        throw new InvalidDataException("CCITT vertical mode points outside the row.");
                    }

                    changes.Add(position);
                    a0 = position;
                    white = !white;
                    break;
            }
        }

        return Finish(changes, columns);
    }

    private enum Mode
    {
        VerticalLeft3 = -3,
        VerticalLeft2 = -2,
        VerticalLeft1 = -1,
        Vertical0 = 0,
        VerticalRight1 = 1,
        VerticalRight2 = 2,
        VerticalRight3 = 3,
        Pass = 100,
        Horizontal = 101,
    }

    private static Mode ReadMode(CcittBitReader reader)
    {
        if (reader.ReadBit() == 1)
        {
            return Mode.Vertical0;
        }

        if (reader.ReadBit() == 1)
        {
            return reader.ReadBit() == 1 ? Mode.VerticalRight1 : Mode.VerticalLeft1;
        }

        if (reader.ReadBit() == 1)
        {
            return Mode.Horizontal;
        }

        if (reader.ReadBit() == 1)
        {
            return Mode.Pass;
        }

        if (reader.ReadBit() == 1)
        {
            return reader.ReadBit() == 1 ? Mode.VerticalRight2 : Mode.VerticalLeft2;
        }

        if (reader.ReadBit() == 1)
        {
            return reader.ReadBit() == 1 ? Mode.VerticalRight3 : Mode.VerticalLeft3;
        }

        throw new InvalidDataException("Unsupported CCITT mode code (extension or end of line inside a row).");
    }

    // b1: first changing element of the reference row right of a0 whose colour is the opposite of the
    // current colour; b2: the next changing element after b1. A reference row always ends with the row width
    // twice (see Finish), so the search stops at its last element at the latest.
    private static (int B1, int B2) FindB(int[] reference, int a0, bool white, int columns)
    {
        var wanted = white ? 0 : 1;
        var i = 0;
        while (i < reference.Length - 1 && (reference[i] <= a0 || (i & 1) != wanted))
        {
            i++;
        }

        return (reference[i], i + 1 < reference.Length ? reference[i + 1] : columns);
    }

    // Changing elements always come in pairs ending at the row width, so a row can serve as reference.
    private static int[] Finish(List<int> changes, int columns)
    {
        while (changes.Count > 0 && changes[^1] >= columns && changes.Count > 1 && changes[^2] >= columns)
        {
            changes.RemoveAt(changes.Count - 1);
        }

        if (changes.Count % 2 == 1)
        {
            changes.Add(columns);
        }

        changes.Add(columns);
        changes.Add(columns);
        return [.. changes];
    }

    private static int ReadRun(CcittBitReader reader, bool white)
    {
        var tables = white ? CcittCodes.White : CcittCodes.Black;
        var total = 0;
        while (true)
        {
            var run = ReadCode(reader, tables);
            total += run;
            if (run < 64)
            {
                return total;
            }
        }
    }

    private static int ReadCode(CcittBitReader reader, int[][] tables)
    {
        var code = 0;
        for (var length = 1; length < tables.Length; length++)
        {
            code = (code << 1) | reader.ReadBit();
            var run = tables[length][code];
            if (run >= 0)
            {
                return run;
            }
        }

        throw new InvalidDataException("Invalid CCITT run-length code.");
    }
}
