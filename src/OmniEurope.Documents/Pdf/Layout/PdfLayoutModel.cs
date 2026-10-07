// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Layout;

/// <summary>A word: letters close together on one baseline, without the spaces around it.</summary>
public sealed record PdfWord(string Text, PdfRectangle BoundingBox, IReadOnlyList<PdfLetter> Letters)
{
    /// <summary>The most frequent font size of its letters.</summary>
    public double FontSize => Letters.GroupBy(l => Math.Round(l.FontSize, 1)).MaxBy(g => g.Count())!.Key;

    /// <summary>The font of its first letter.</summary>
    public string FontName => Letters[0].FontName;

    /// <summary>True when most letters come from a bold face (judged from the font name).</summary>
    public bool IsBold => Letters.Count(l => FontStyle.IsBold(l.FontName)) * 2 > Letters.Count;

    /// <summary>True when most letters come from an italic face (judged from the font name).</summary>
    public bool IsItalic => Letters.Count(l => FontStyle.IsItalic(l.FontName)) * 2 > Letters.Count;
}

/// <summary>A line segment: words on one baseline, not crossing a column gap.</summary>
public sealed record PdfTextLine(IReadOnlyList<PdfWord> Words, PdfRectangle BoundingBox)
{
    /// <summary>The words joined by single spaces.</summary>
    public string Text => string.Join(' ', Words.Select(w => w.Text));

    /// <summary>The baseline (y of the first letter).</summary>
    public double Baseline => Words[0].Letters[0].Y;

    /// <summary>The most frequent font size of the line.</summary>
    public double FontSize => Words.SelectMany(w => w.Letters).GroupBy(l => Math.Round(l.FontSize, 1)).MaxBy(g => g.Count())!.Key;
}

/// <summary>A block of text: consecutive lines of one paragraph, heading or table cell.</summary>
public sealed record PdfTextBlock(IReadOnlyList<PdfTextLine> Lines, PdfRectangle BoundingBox)
{
    /// <summary>The lines joined by line feeds.</summary>
    public string Text => string.Join('\n', Lines.Select(l => l.Text));

    /// <summary>Position in reading order on its page, from 0.</summary>
    public int ReadingOrder { get; init; }

    /// <summary>True when the block repeats at the top or bottom of the pages (running header, footer,
    /// page number).</summary>
    public bool IsDecoration { get; init; }

    /// <summary>The most frequent font size of the block.</summary>
    public double FontSize => Lines.SelectMany(l => l.Words).SelectMany(w => w.Letters).GroupBy(l => Math.Round(l.FontSize, 1)).MaxBy(g => g.Count())!.Key;
}

/// <summary>The analysed layout of one page.</summary>
public sealed record PdfPageLayout(PdfPage Page, IReadOnlyList<PdfTextBlock> Blocks, bool IsScanned)
{
    /// <summary>The blocks that are not decoration, in reading order, separated by blank lines.</summary>
    public string Text => string.Join("\n\n", Blocks.Where(b => !b.IsDecoration).Select(b => b.Text));
}

/// <summary>Style hints taken from font names (PostScript names carry the weight and slant).</summary>
internal static class FontStyle
{
    public static bool IsBold(string name) =>
        name.Contains("Bold", StringComparison.OrdinalIgnoreCase) || name.Contains("Black", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Heavy", StringComparison.OrdinalIgnoreCase) || name.Contains("Semibold", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(",Bold", StringComparison.OrdinalIgnoreCase);

    public static bool IsItalic(string name) =>
        name.Contains("Italic", StringComparison.OrdinalIgnoreCase) || name.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
}
