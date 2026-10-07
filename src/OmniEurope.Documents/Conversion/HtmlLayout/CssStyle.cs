// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.HtmlLayout;

/// <summary>
/// The few inline CSS declarations (<c>style</c> attribute) that carry into Word formatting: colours, font
/// weight, style, size and family, text decoration and text alignment. Everything else is ignored.
/// </summary>
internal static class CssStyle
{
    private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "000000", ["white"] = "FFFFFF", ["red"] = "FF0000", ["green"] = "008000", ["blue"] = "0000FF",
        ["yellow"] = "FFFF00", ["gray"] = "808080", ["grey"] = "808080", ["silver"] = "C0C0C0", ["maroon"] = "800000",
        ["navy"] = "000080", ["purple"] = "800080", ["teal"] = "008080", ["orange"] = "FFA500", ["olive"] = "808000",
        ["lime"] = "00FF00", ["aqua"] = "00FFFF", ["fuchsia"] = "FF00FF",
    };

    // Points per unit; "em", "rem" and "%" are relative to the current size.
    private static readonly Dictionary<string, Func<double, double, double>> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pt"] = (n, _) => n,
        ["px"] = (n, _) => n * 0.75,
        [string.Empty] = (n, _) => n * 0.75,
        ["em"] = (n, relative) => n * relative,
        ["rem"] = (n, relative) => n * relative,
        ["%"] = (n, relative) => n * relative / 100,
        ["cm"] = (n, _) => n * 72 / 2.54,
        ["mm"] = (n, _) => n * 72 / 25.4,
        ["in"] = (n, _) => n * 72,
    };

    // Each declaration that maps to character formatting, applied in this order.
    private static readonly (string Name, Func<string, WordRunProperties, double, WordRunProperties> Apply)[] Declarations =
    [
        ("color", (v, r, _) => Color(v) is { } color ? r with { Color = color } : r),
        ("background-color", (v, r, _) => Color(v) is { } fill ? r with { Shading = fill } : r),
        ("font-weight", (v, r, _) => r with { Bold = v is "bold" or "bolder" || (int.TryParse(v, out var weight) && weight >= 600) }),
        ("font-style", (v, r, _) => r with { Italic = v is "italic" or "oblique" }),
        ("text-decoration", Decoration),
        ("text-decoration-line", Decoration),
        ("font-size", (v, r, size) => Length(v, size) is { } points ? r with { FontSize = points, FontSizeComplex = points } : r),
        ("font-family", (v, r, _) => v.Split(',')[0].Trim().Trim('"', '\'') is { Length: > 0 } font ? r with { Font = font, FontTheme = null } : r),
    ];

    public static Dictionary<string, string> Parse(string? style)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in (style ?? string.Empty).Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon > 0)
            {
                result[declaration[..colon].Trim()] = declaration[(colon + 1)..].Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        return result;
    }

    /// <summary>Character formatting set by the declarations, on top of <paramref name="current"/>.</summary>
    public static WordRunProperties Run(Dictionary<string, string> css, WordRunProperties current, double baseSize)
    {
        var result = current;
        foreach (var (name, apply) in Declarations)
        {
            if (css.TryGetValue(name, out var value))
            {
                result = apply(value, result, baseSize);
            }
        }

        return result;
    }

    private static WordRunProperties Decoration(string value, WordRunProperties run, double size)
    {
        _ = size;
        var underline = value.Contains("underline", StringComparison.OrdinalIgnoreCase);
        var strike = value.Contains("line-through", StringComparison.OrdinalIgnoreCase);
        return run with { Underline = underline ? WordUnderline.Single : run.Underline, Strike = strike ? true : run.Strike };
    }

    public static WordAlignment? Alignment(Dictionary<string, string> css, string? alignAttribute)
    {
        var value = css.GetValueOrDefault("text-align") ?? alignAttribute;
        return value?.ToLowerInvariant() switch
        {
            "center" => WordAlignment.Center,
            "right" or "end" => WordAlignment.Right,
            "justify" => WordAlignment.Justify,
            "left" or "start" => WordAlignment.Left,
            _ => null,
        };
    }

    /// <summary>A CSS colour (<c>#rgb</c>, <c>#rrggbb</c>, <c>rgb(r, g, b)</c> or a basic name) as <c>RRGGBB</c>.</summary>
    public static string? Color(string value)
    {
        value = value.Trim();
        if (Named.TryGetValue(value, out var named))
        {
            return named;
        }

        if (value.StartsWith('#'))
        {
            return Hex(value[1..]);
        }

        return value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) ? Rgb(value) : null;
    }

    private static string? Hex(string hex)
    {
        hex = hex.Length == 3 ? string.Concat(hex.Select(c => new string(c, 2))) : hex;
        return hex.Length == 6 && hex.All(char.IsAsciiHexDigit) ? hex.ToUpperInvariant() : null;
    }

    private static string? Rgb(string value)
    {
        var open = value.IndexOf('(');
        var parts = open < 0 ? [] : value[(open + 1)..].TrimEnd(')').Split(',');
        var bytes = parts.Take(3).Select(p => int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Clamp(n, 0, 255) : -1).ToList();
        return bytes.Count == 3 && bytes.All(b => b >= 0) ? string.Concat(bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) : null;
    }

    /// <summary>A CSS length in points: <c>pt</c>, <c>px</c>, <c>em</c>, <c>rem</c>, <c>%</c> (relative to <paramref name="relative"/>), <c>cm</c>, <c>mm</c>, <c>in</c>.</summary>
    public static double? Length(string value, double relative)
    {
        value = value.Trim();
        var unitStart = value.Length;
        while (unitStart > 0 && !char.IsAsciiDigit(value[unitStart - 1]) && value[unitStart - 1] != '.')
        {
            unitStart--;
        }

        var parsed = double.TryParse(value[..unitStart], NumberStyles.Float, CultureInfo.InvariantCulture, out var number);
        return parsed && Units.TryGetValue(value[unitStart..], out var convert) ? convert(number, relative) : null;
    }
}
