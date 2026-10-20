// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;

namespace OmniEurope.Documents.Charts;

/// <summary>
/// The colours of a theme's colour scheme (<c>a:clrScheme</c>) and the DrawingML colour elements resolved against it
/// (ECMA-376 part 1, §20.1.2.3): <c>srgbClr</c>, <c>schemeClr</c>, <c>sysClr</c> and the common <c>prstClr</c>, with
/// their <c>lumMod</c>, <c>lumOff</c>, <c>tint</c> and <c>shade</c> transforms. Without a theme the Office scheme applies.
/// </summary>
internal sealed class ChartPalette
{
    private static readonly Dictionary<string, string> Office = new(StringComparer.Ordinal)
    {
        ["dk1"] = "000000",
        ["lt1"] = "FFFFFF",
        ["dk2"] = "44546A",
        ["lt2"] = "E7E6E6",
        ["accent1"] = "4472C4",
        ["accent2"] = "ED7D31",
        ["accent3"] = "A5A5A5",
        ["accent4"] = "FFC000",
        ["accent5"] = "5B9BD5",
        ["accent6"] = "70AD47",
        ["hlink"] = "0563C1",
        ["folHlink"] = "954F72",
    };

    // The scheme slots a colour may name by its role in the text (tx, bg) rather than by its slot.
    private static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        ["tx1"] = "dk1",
        ["bg1"] = "lt1",
        ["tx2"] = "dk2",
        ["bg2"] = "lt2",
    };

    private static readonly Dictionary<string, string> Presets = new(StringComparer.Ordinal)
    {
        ["black"] = "000000",
        ["white"] = "FFFFFF",
        ["red"] = "FF0000",
        ["green"] = "008000",
        ["blue"] = "0000FF",
        ["yellow"] = "FFFF00",
        ["gray"] = "808080",
        ["orange"] = "FFA500",
    };

    private readonly Dictionary<string, string> _scheme;

    private ChartPalette(Dictionary<string, string> scheme) => _scheme = scheme;

    /// <summary>The Office theme's colours.</summary>
    public static ChartPalette Default { get; } = new(Office);

    /// <summary>The colours of a theme part, the Office ones for the slots it lacks.</summary>
    public static ChartPalette FromTheme(XDocument? theme)
    {
        var scheme = new Dictionary<string, string>(Office, StringComparer.Ordinal);
        var colors = theme?.Root?.Descendants().FirstOrDefault(e => e.Name.LocalName == "clrScheme");
        foreach (var slot in colors?.Elements() ?? [])
        {
            if (slot.Elements().FirstOrDefault() is { } color && Base(color, null) is { } value)
            {
                scheme[slot.Name.LocalName] = value;
            }
        }

        return new ChartPalette(scheme);
    }

    /// <summary>The automatic colour of series (or slice) <paramref name="index"/>: the six accents in turn, darker on
    /// the second round, lighter on the third, and so on.</summary>
    public string Accent(int index)
    {
        // Six accents in three rounds; a negative index (a damaged file) wraps round to a non-negative slot.
        var slot = ((index % 18) + 18) % 18;
        var accent = _scheme["accent" + ((slot % 6) + 1).ToString(CultureInfo.InvariantCulture)];
        return (slot / 6) switch
        {
            1 => Luminance(accent, 0.6, 0),
            2 => Luminance(accent, 0.8, 0.2),
            _ => accent,
        };
    }

    /// <summary>The solid fill colour of a shape properties element (<c>c:spPr</c>), or of its outline when
    /// <paramref name="line"/> is set; null when it names none.</summary>
    public string? Fill(XElement? properties, bool line = false)
    {
        var owner = line ? Child(properties, "ln") : properties;
        return Child(owner, "solidFill")?.Elements().FirstOrDefault() is { } color ? Resolve(color) : null;
    }

    /// <summary>True when the shape properties say no fill (or, with <paramref name="line"/>, no outline).</summary>
    public static bool None(XElement? properties, bool line = false) =>
        Child(line ? Child(properties, "ln") : properties, "noFill") is not null;

    /// <summary>A colour element with its transforms applied, as <c>RRGGBB</c>.</summary>
    public string? Resolve(XElement color)
    {
        if (Base(color, _scheme) is not { } value)
        {
            return null;
        }

        foreach (var transform in color.Elements())
        {
            var amount = Percent(transform);
            value = transform.Name.LocalName switch
            {
                "lumMod" => Luminance(value, amount, 0),
                "lumOff" => Luminance(value, 1, amount),
                "tint" => Mix(value, amount, 255),
                "shade" => Mix(value, amount, 0),
                _ => value,
            };
        }

        return value;
    }

    private static string? Base(XElement color, Dictionary<string, string>? scheme)
    {
        var value = (string?)color.Attribute("val");
        return color.Name.LocalName switch
        {
            "srgbClr" => Hex(value),
            "sysClr" => Hex((string?)color.Attribute("lastClr")) ?? (value == "window" ? "FFFFFF" : "000000"),
            "schemeClr" when scheme is not null && value is not null => scheme.GetValueOrDefault(Roles.GetValueOrDefault(value, value)),
            "prstClr" when value is not null => Presets.GetValueOrDefault(value),
            _ => null,
        };
    }

    private static string? Hex(string? value) =>
        value is { Length: 6 } && value.All(char.IsAsciiHexDigit) ? value.ToUpperInvariant() : null;

    private static double Percent(XElement transform) =>
        int.TryParse((string?)transform.Attribute("val"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value / 100000d : 1;

    // tint moves the colour towards white, shade towards black: the amount kept of the colour itself.
    private static string Mix(string color, double kept, int toward)
    {
        var (r, g, b) = Rgb(color);
        int Channel(int c) => Round((c * kept) + (toward * (1 - kept)));
        return Format(Channel(r), Channel(g), Channel(b));
    }

    // Luminance in the HSL model multiplied by modulation, then offset.
    internal static string Luminance(string color, double modulation, double offset)
    {
        var (r, g, b) = Rgb(color);
        var (h, s, l) = ToHsl(r / 255d, g / 255d, b / 255d);
        l = Math.Clamp((l * modulation) + offset, 0, 1);
        var (rr, gg, bb) = FromHsl(h, s, l);
        return Format(Round(rr * 255), Round(gg * 255), Round(bb * 255));
    }

    // A channel from its exact value: values of the form n + 0.5 (a halved luminance often gives them) are ties, and
    // floating-point noise around them would decide the side; snapping to six decimals first makes the tie exact, then
    // every channel rounds it the same way.
    private static int Round(double channel) => (int)Math.Round(Math.Round(channel, 6), MidpointRounding.ToEven);

    private static (double H, double S, double L) ToHsl(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;
        if (d == 0)
        {
            return (0, 0, l);
        }

        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        var h = max == r ? ((g - b) / d) + (g < b ? 6 : 0) : max == g ? ((b - r) / d) + 2 : ((r - g) / d) + 4;
        return (h / 6, s, l);
    }

    private static (double R, double G, double B) FromHsl(double h, double s, double l)
    {
        if (s == 0)
        {
            return (l, l, l);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
        var p = (2 * l) - q;
        return (Hue(p, q, h + (1d / 3)), Hue(p, q, h), Hue(p, q, h - (1d / 3)));
    }

    private static double Hue(double p, double q, double t)
    {
        t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
        return t switch
        {
            < 1d / 6 => p + ((q - p) * 6 * t),
            < 1d / 2 => q,
            < 2d / 3 => p + ((q - p) * ((2d / 3) - t) * 6),
            _ => p,
        };
    }

    private static (int R, int G, int B) Rgb(string color) =>
        (int.Parse(color.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
         int.Parse(color.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
         int.Parse(color.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static string Format(int r, int g, int b) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}");

    internal static XElement? Child(XElement? parent, string name) => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
}
