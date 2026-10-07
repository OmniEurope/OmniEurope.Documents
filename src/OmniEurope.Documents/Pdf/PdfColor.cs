// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Pdf;

/// <summary>An RGB colour, components from 0 to 255.</summary>
public readonly record struct PdfColor(byte R, byte G, byte B)
{
    /// <summary>Black.</summary>
    public static PdfColor Black { get; } = new(0, 0, 0);

    /// <summary>White.</summary>
    public static PdfColor White { get; } = new(255, 255, 255);

    /// <summary>Gray (#808080).</summary>
    public static PdfColor Gray { get; } = new(128, 128, 128);

    /// <summary>Dark gray (#A9A9A9, the CSS value).</summary>
    public static PdfColor DarkGray { get; } = new(169, 169, 169);

    /// <summary>Light gray (#D3D3D3).</summary>
    public static PdfColor LightGray { get; } = new(211, 211, 211);

    /// <summary>Red (#FF0000).</summary>
    public static PdfColor Red { get; } = new(255, 0, 0);

    /// <summary>Green (#008000).</summary>
    public static PdfColor Green { get; } = new(0, 128, 0);

    /// <summary>Blue (#0000FF).</summary>
    public static PdfColor Blue { get; } = new(0, 0, 255);

    /// <summary>Dark blue (#00008B).</summary>
    public static PdfColor DarkBlue { get; } = new(0, 0, 139);

    /// <summary>Orange (#FFA500).</summary>
    public static PdfColor Orange { get; } = new(255, 165, 0);

    /// <summary>Dark slate gray (#2F4F4F).</summary>
    public static PdfColor DarkSlateGray { get; } = new(47, 79, 79);

    /// <summary>Parses <c>#RRGGBB</c>, <c>RRGGBB</c> or <c>#RGB</c>.</summary>
    public static PdfColor FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var value = hex.Trim().TrimStart('#');
        if (value.Length == 3)
        {
            value = string.Concat(value.Select(c => new string(c, 2)));
        }

        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            throw new FormatException($"'{hex}' is not a #RRGGBB colour.");
        }

        return new PdfColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    /// <summary>The colour as <c>#RRGGBB</c>.</summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Black or white, whichever reads better on this colour (WCAG relative luminance).</summary>
    public PdfColor ReadableForeground()
    {
        static double Channel(byte c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        var luminance = (0.2126 * Channel(R)) + (0.7152 * Channel(G)) + (0.0722 * Channel(B));
        return luminance > 0.179 ? Black : White;
    }
}
