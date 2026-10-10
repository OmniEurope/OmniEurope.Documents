// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Fonts;

/// <summary>
/// Advance widths of the Symbol font, read from Adobe's Core 14 font metrics file (<c>Symbol.afm</c>, shipped
/// unmodified with its licence <c>MustRead.html</c>). The font itself is not bundled: only its widths are used,
/// so text drawn with a look-alike face still takes the room Symbol gives it.
/// </summary>
internal static class SymbolMetrics
{
    private const string Resource = "OmniEurope.Documents.Fonts.Adobe.Symbol.afm";

    private static readonly Lazy<IReadOnlyDictionary<int, int>> Widths = new(() => Parse(ReadResource(Resource)));

    /// <summary>The advance of a Symbol character code (0x20 to 0xFE) in thousandths of an em, or null when the
    /// font has no character at that code.</summary>
    public static int? Advance(int code) => Widths.Value.TryGetValue(code, out var width) ? width : null;

    /// <summary>The licence notice of the metrics file.</summary>
    public static string Licence() => ReadResource("OmniEurope.Documents.Fonts.Adobe.MustRead.html");

    /// <summary>The <c>WX</c> width of each encoded character (<c>C</c> code 0 to 255) of an AFM file.</summary>
    internal static Dictionary<int, int> Parse(string afm)
    {
        var widths = new Dictionary<int, int>();
        foreach (var line in afm.Split('\n'))
        {
            if (line.StartsWith("C ", StringComparison.Ordinal) && Character(line) is var (code, width) && code is >= 0 and <= 255)
            {
                widths[code] = width;
            }
        }

        return widths;
    }

    // The code and width of a "C code ; WX width ; N name ; ..." line; null when either is missing.
    private static (int Code, int Width)? Character(string line)
    {
        int? code = null;
        int? width = null;
        foreach (var field in line.Split(';'))
        {
            var parts = field.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                code = parts[0] == "C" ? value : code;
                width = parts[0] == "WX" ? value : width;
            }
        }

        return code is { } c && width is { } w ? (c, w) : null;
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(SymbolMetrics).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Bundled metrics '{name}' are missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
