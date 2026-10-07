// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>Horizontal alignment of a cell.</summary>
public enum XlsxHorizontalAlignment
{
    /// <summary>Excel's default (text left, numbers right).</summary>
    General,

    /// <summary>Left.</summary>
    Left,

    /// <summary>Centred.</summary>
    Center,

    /// <summary>Right.</summary>
    Right,

    /// <summary>Justified.</summary>
    Justify,
}

/// <summary>Vertical alignment of a cell.</summary>
public enum XlsxVerticalAlignment
{
    /// <summary>Bottom (Excel's default).</summary>
    Bottom,

    /// <summary>Top.</summary>
    Top,

    /// <summary>Centred.</summary>
    Center,
}

/// <summary>
/// The look of a cell. Immutable with value equality: derive variants with <c>with</c>
/// (<c>cell.Style = cell.Style with { Bold = true }</c>); equal styles share one entry in the file.
/// Colours are <c>RRGGBB</c> hexadecimal strings.
/// </summary>
public sealed record XlsxStyle
{
    /// <summary>The default style (Calibri 11, general format).</summary>
    public static XlsxStyle Default { get; } = new();

    /// <summary>Font family. Default Calibri.</summary>
    public string FontName { get; init; } = "Calibri";

    /// <summary>Font size in points. Default 11.</summary>
    public double FontSize { get; init; } = 11;

    /// <summary>Bold.</summary>
    public bool Bold { get; init; }

    /// <summary>Italic.</summary>
    public bool Italic { get; init; }

    /// <summary>Single underline.</summary>
    public bool Underline { get; init; }

    /// <summary>Strike-through.</summary>
    public bool Strike { get; init; }

    /// <summary>Font colour (<c>RRGGBB</c>), null for automatic.</summary>
    public string? FontColor { get; init; }

    /// <summary>Solid fill colour (<c>RRGGBB</c>), null for none.</summary>
    public string? FillColor { get; init; }

    /// <summary>Number format code (<c>0.00</c>, <c>yyyy-mm-dd</c>, <c>#,##0 €</c>...); <c>General</c> by default.</summary>
    public string NumberFormat { get; init; } = "General";

    /// <summary>Horizontal alignment.</summary>
    public XlsxHorizontalAlignment HorizontalAlignment { get; init; }

    /// <summary>Vertical alignment.</summary>
    public XlsxVerticalAlignment VerticalAlignment { get; init; }

    /// <summary>Wrap text inside the cell.</summary>
    public bool WrapText { get; init; }

    /// <summary>Thin border on every side.</summary>
    public bool Border { get; init; }

    /// <summary>Border colour (<c>RRGGBB</c>), null for automatic.</summary>
    public string? BorderColor { get; init; }

    /// <summary>Marks text that starts like a formula as literal text (Excel's leading apostrophe), so
    /// editing the cell never turns it into a formula.</summary>
    public bool QuotePrefix { get; init; }

    /// <summary>Normalises a colour to upper-case <c>RRGGBB</c>; accepts <c>#RRGGBB</c>, <c>RRGGBB</c> or <c>AARRGGBB</c>.</summary>
    public static string NormalizeColor(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        var hex = color.Trim().TrimStart('#');
        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        if (hex.Length != 6 || !hex.All(char.IsAsciiHexDigit))
        {
            throw new FormatException($"'{color}' is not an RRGGBB colour.");
        }

        return hex.ToUpperInvariant();
    }
}
