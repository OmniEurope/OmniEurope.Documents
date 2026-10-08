// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Fonts;

/// <summary>
/// The character-to-glyph map of a TrueType font: the best Unicode subtable (format 12 full repertoire,
/// else format 4 BMP, also 0 and 6), or the symbol subtable of symbol fonts (codes U+F020-U+F0FF, also
/// reachable with the plain 8-bit code).
/// </summary>
internal sealed class TrueTypeCmap
{
    private readonly Dictionary<int, int> _map = [];

    public TrueTypeCmap(FontReader font, FontTable table)
    {
        var count = font.U16(table.Offset + 2);
        var best = -1;
        var bestScore = 0;
        for (var i = 0; i < count; i++)
        {
            var record = table.Offset + 4 + (i * 8);
            var platform = font.U16(record);
            var encoding = font.U16(record + 2);
            var offset = table.Offset + (int)font.U32(record + 4);
            var format = font.Has(offset, 2) ? font.U16(offset) : -1;
            var score = Score(platform, encoding, format);
            if (score > bestScore)
            {
                bestScore = score;
                best = offset;
                IsSymbol = platform == 3 && encoding == 0;
            }
        }

        if (best >= 0)
        {
            Read(font, best);
        }
    }

    /// <summary>True when the map is a Windows symbol subtable.</summary>
    public bool IsSymbol { get; private set; }

    /// <summary>Every mapped code point.</summary>
    public IReadOnlyDictionary<int, int> Map => _map;

    public int Lookup(int codePoint)
    {
        if (_map.TryGetValue(codePoint, out var glyph))
        {
            return glyph;
        }

        return IsSymbol && codePoint is >= 0x20 and <= 0xFF && _map.TryGetValue(0xF000 + codePoint, out glyph) ? glyph : 0;
    }

    // Preferred subtables by (platform, encoding, format): Windows full Unicode, Unicode full, Windows BMP,
    // Unicode BMP (any Unicode encoding, keyed as 0), Windows symbol. Formats 0 and 6 come last.
    private static readonly Dictionary<(int Platform, int Encoding, int Format), int> Preferences = new()
    {
        [(3, 10, 12)] = 6,
        [(0, 0, 12)] = 5,
        [(3, 1, 4)] = 4,
        [(0, 0, 4)] = 3,
        [(3, 0, 4)] = 2,
    };

    private static int Score(int platform, int encoding, int format) =>
        Preferences.TryGetValue((platform, platform == 0 ? 0 : encoding, format), out var score) ? score : format is 0 or 6 ? 1 : 0;

    private void Read(FontReader font, int offset)
    {
        switch (font.U16(offset))
        {
            case 0:
                ReadFormat0(font, offset);
                break;
            case 4:
                ReadFormat4(font, offset);
                break;
            case 6:
                ReadFormat6(font, offset);
                break;
            case 12:
                ReadFormat12(font, offset);
                break;
        }
    }

    // Byte encoding table: one glyph byte for each of the 256 codes.
    private void ReadFormat0(FontReader font, int offset)
    {
        for (var code = 0; code < 256; code++)
        {
            Add(code, font.U8(offset + 6 + code));
        }
    }

    // Trimmed table: consecutive codes from firstCode.
    private void ReadFormat6(FontReader font, int offset)
    {
        var first = font.U16(offset + 6);
        var entries = font.U16(offset + 8);
        for (var i = 0; i < entries; i++)
        {
            Add(first + i, font.U16(offset + 10 + (i * 2)));
        }
    }

    private void ReadFormat4(FontReader font, int offset)
    {
        var segments = font.U16(offset + 6) / 2;
        var ends = offset + 14;
        var starts = ends + (segments * 2) + 2;
        var deltas = starts + (segments * 2);
        var ranges = deltas + (segments * 2);
        // Valid segments are disjoint, so together they hold at most the 65,536 16-bit codes; overlapping
        // segments of a forged table stop there instead of walking the code space once per segment.
        var budget = 0x10000;
        for (var s = 0; s < segments && budget > 0; s++)
        {
            var end = font.U16(ends + (s * 2));
            var start = font.U16(starts + (s * 2));
            var delta = font.S16(deltas + (s * 2));
            var rangeOffset = font.U16(ranges + (s * 2));
            for (var code = start; code <= end && code != 0xFFFF && budget-- > 0; code++)
            {
                int glyph;
                if (rangeOffset == 0)
                {
                    glyph = (code + delta) & 0xFFFF;
                }
                else
                {
                    var address = ranges + (s * 2) + rangeOffset + ((code - start) * 2);
                    glyph = font.Has(address, 2) ? font.U16(address) : 0;
                    glyph = glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
                }

                Add(code, glyph);
            }
        }
    }

    private void ReadFormat12(FontReader font, int offset)
    {
        var groups = font.U32(offset + 12);
        // Disjoint groups hold at most every Unicode code point; overlapping ones stop there.
        long budget = 0x110000;
        for (var g = 0; g < groups && g < 1 << 20 && budget > 0; g++)
        {
            var group = offset + 16 + (g * 12);
            var start = font.U32(group);
            var end = Math.Min(font.U32(group + 4), 0x10FFFF);
            var glyph = font.U32(group + 8);
            for (var code = start; code <= end && end - start < 1 << 20 && budget-- > 0; code++)
            {
                Add((int)code, (int)(glyph + (code - start)));
            }
        }
    }

    private void Add(int code, int glyph)
    {
        if (glyph != 0)
        {
            _map.TryAdd(code, glyph);
        }
    }
}
