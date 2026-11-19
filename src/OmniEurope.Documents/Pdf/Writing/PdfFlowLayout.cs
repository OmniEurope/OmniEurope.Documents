// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>Page geometry and decorations of a <see cref="PdfFlowLayout"/>.</summary>
public sealed class PdfFlowOptions
{
    /// <summary>Page width in points. Default A4.</summary>
    public double PageWidth { get; init; } = PdfPageSize.A4.Width;

    /// <summary>Page height in points. Default A4.</summary>
    public double PageHeight { get; init; } = PdfPageSize.A4.Height;

    /// <summary>Margins in points (left, top, right, bottom). Default 2 cm on every side.</summary>
    public (double Left, double Top, double Right, double Bottom) Margins { get; init; } = (56.7, 56.7, 56.7, 56.7);

    /// <summary>Height in points of the header zone, at the top of the content area (below the top margin). The flow
    /// starts below it on every page; <see cref="Header"/> draws inside it. Default 0: no zone is reserved and the
    /// flow starts at the top margin.</summary>
    public double HeaderHeight { get; init; }

    /// <summary>Height in points of the footer zone, at the bottom of the content area (above the bottom margin). The
    /// flow stops above it on every page; <see cref="Footer"/> draws inside it. Default 0: no zone is reserved.</summary>
    public double FooterHeight { get; init; }

    /// <summary>Drawn on each page when it is created (letterhead, header); receives the page and its number. With a
    /// <see cref="HeaderHeight"/> it should stay within the zone that starts at the top margin.</summary>
    public Action<PdfCanvas, int>? Header { get; init; }

    /// <summary>Drawn on every page once the flow is finished; receives the page, its number and the page
    /// count, so "page 3 / 7" footers are possible. With a <see cref="FooterHeight"/> it should stay within the zone
    /// that ends at the bottom margin.</summary>
    public Action<PdfCanvas, int, int>? Footer { get; init; }
}

/// <summary>
/// Lays content out top to bottom between the margins and starts a new page when the next element does not
/// fit: wrapped paragraphs (split between lines), spaces, rules, images and tables (split between rows,
/// header rows repeated). Call <see cref="Finish"/> to draw the footers.
/// </summary>
public sealed class PdfFlowLayout
{
    private readonly PdfDocumentBuilder _document;
    private readonly PdfFlowOptions _options;
    private readonly List<PdfCanvas> _pages = [];
    private double _left;
    private double _width;
    private bool _finished;

    /// <summary>Starts a flow on a new page of <paramref name="document"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A header or footer height is negative, or the margins, header
    /// and footer leave no room on the page.</exception>
    public PdfFlowLayout(PdfDocumentBuilder document, PdfFlowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _options = options ?? new PdfFlowOptions();
        if (!(_options.HeaderHeight >= 0) || !(_options.FooterHeight >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The header and footer heights cannot be negative.");
        }

        var margins = _options.Margins;
        if (!(_options.PageWidth - margins.Left - margins.Right > 0)
            || !(_options.PageHeight - margins.Top - margins.Bottom - _options.HeaderHeight - _options.FooterHeight > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The margins, header and footer leave no room on the page.");
        }

        _left = _options.Margins.Left;
        _width = _options.PageWidth - _options.Margins.Left - _options.Margins.Right;
        Page = null!;
        NewPage();
    }

    /// <summary>The document.</summary>
    public PdfDocumentBuilder Document => _document;

    /// <summary>The page being filled.</summary>
    public PdfCanvas Page { get; private set; }

    /// <summary>The vertical position of the next element.</summary>
    public double Y { get; set; }

    /// <summary>Left edge of the content area (of the current block inside <see cref="AddBlock"/>).</summary>
    public double Left => _left;

    /// <summary>Width of the content area (of the current block inside <see cref="AddBlock"/>).</summary>
    public double ContentWidth => _width;

    /// <summary>Highest position content may reach: below the top margin and the header zone.</summary>
    public double Top => _options.Margins.Top + _options.HeaderHeight;

    /// <summary>Lowest position content may reach: above the bottom margin and the footer zone.</summary>
    public double Bottom => _options.PageHeight - _options.Margins.Bottom - _options.FooterHeight;

    /// <summary>Space left on the current page.</summary>
    public double Remaining => Bottom - Y;

    /// <summary>Starts a new page.</summary>
    public PdfCanvas NewPage()
    {
        ThrowIfFinished();
        Page = _document.AddPage(_options.PageWidth, _options.PageHeight);
        _pages.Add(Page);
        Y = Top;
        _options.Header?.Invoke(Page, _pages.Count);
        return Page;
    }

    /// <summary>Starts a new page unless <paramref name="height"/> points still fit on this one.</summary>
    public void EnsureSpace(double height)
    {
        ThrowIfFinished();
        if (Y + height > Bottom && Y > Top)
        {
            NewPage();
        }
    }

    /// <summary>Moves down by <paramref name="height"/> points (never across a page).</summary>
    public void AddSpace(double height)
    {
        ThrowIfFinished();
        Y = Math.Min(Y + height, Bottom);
    }

    /// <summary>Adds a wrapped paragraph; lines that do not fit continue on the next page.</summary>
    public void AddParagraph(string text, PdfFont font, double size, PdfColor? color = null, PdfTextAlignment alignment = PdfTextAlignment.Left,
        double spaceBefore = 0, double spaceAfter = 0, double lineSpacing = 1.0, double indent = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfFinished();
        var metrics = _document.Metrics(font, size);
        var lineHeight = metrics.LineHeight * lineSpacing;
        var width = ContentWidth - indent;
        Y += spaceBefore;
        foreach (var line in TextWrapper.Wrap(_document, text, font, size, width))
        {
            EnsureSpace(lineHeight);
            PdfParagraphPainter.DrawLine(Page, line, Left + indent, Y + metrics.Ascent, width, font, size, color, alignment);
            Y += lineHeight;
        }

        AddSpace(spaceAfter);
    }

    /// <summary>Adds a horizontal rule across the content width.</summary>
    public void AddRule(PdfColor color, double thickness = 0.5, double spaceAround = 6)
    {
        ThrowIfFinished();
        EnsureSpace((spaceAround * 2) + thickness);
        Y += spaceAround;
        Page.FillRectangle(Left, Y, ContentWidth, thickness, color);
        Y += thickness + spaceAround;
    }

    /// <summary>Adds an image of the given size (scaled down to the content width if wider).</summary>
    public void AddImage(PdfImage image, double width, double height, PdfTextAlignment alignment = PdfTextAlignment.Left)
    {
        ArgumentNullException.ThrowIfNull(image);
        ThrowIfFinished();
        if (width > ContentWidth)
        {
            height *= ContentWidth / width;
            width = ContentWidth;
        }

        EnsureSpace(height);
        Page.DrawImage(image, Left + Offset(alignment, ContentWidth - width), Y, width, height);
        Y += height;
    }

    /// <summary>
    /// Runs <paramref name="content"/> in a column of <paramref name="width"/> points placed left, centred or right
    /// (<see cref="PdfTextAlignment.Justify"/> places it left) in the current content width. Inside it
    /// <see cref="Left"/> and <see cref="ContentWidth"/> describe the column, so paragraphs, rules, images, tables, boxes
    /// and nested blocks flow in it, and the flow goes down and onto new pages as usual; afterwards the full width is
    /// back and <see cref="Y"/> is below the block. A width over the content width is reduced to it.
    /// </summary>
    /// <param name="width">Width of the block in points, above zero.</param>
    /// <param name="alignment">Where the block sits in the content width.</param>
    /// <param name="content">Adds the content of the block to the layout it receives (this layout).</param>
    public void AddBlock(double width, PdfTextAlignment alignment, Action<PdfFlowLayout> content)
    {
        if (!(width > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(width), "A block needs a width above zero.");
        }

        ArgumentNullException.ThrowIfNull(content);
        ThrowIfFinished();
        var (outerLeft, outerWidth) = (_left, _width);
        width = Math.Min(width, outerWidth);
        _left = outerLeft + Offset(alignment, outerWidth - width);
        _width = width;
        try
        {
            content(this);
        }
        finally
        {
            (_left, _width) = (outerLeft, outerWidth);
        }
    }

    /// <summary>
    /// Adds a box around wrapped text: padding, an optional fill and an optional border on all four sides (see
    /// <see cref="PdfBoxStyle"/>). The box advances <see cref="Y"/> by the height of its text plus twice the padding
    /// and moves to a new page when it does not fit; it is never split, so a box taller than a page runs past the
    /// bottom of its page.
    /// </summary>
    /// <param name="text">The text; line feeds are hard breaks.</param>
    /// <param name="font">Font of the text.</param>
    /// <param name="size">Size of the text in points.</param>
    /// <param name="style">Padding, colours, width and alignment; the defaults of <see cref="PdfBoxStyle"/> when null.</param>
    public void AddBox(string text, PdfFont font, double size, PdfBoxStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        style ??= new PdfBoxStyle();
        if (style.Width is <= 0 or double.NaN)
        {
            throw new ArgumentException("A box needs a width above zero.", nameof(style));
        }

        ThrowIfFinished();
        var metrics = _document.Metrics(font, size);
        var padding = Math.Max(style.Padding, 0);
        var limit = Math.Min(style.Width ?? ContentWidth, ContentWidth);
        var lines = TextWrapper.Wrap(_document, text, font, size, limit - (2 * padding));
        var width = style.Width is null ? Math.Min(lines.Select(l => l.Width).DefaultIfEmpty(0).Max() + (2 * padding), limit) : limit;
        var height = (lines.Count * metrics.LineHeight) + (2 * padding);
        EnsureSpace(height);
        var x = Left + Offset(style.BoxAlignment, ContentWidth - width);
        if (style.Fill is { } fill)
        {
            Page.FillRectangle(x, Y, width, height, fill);
        }

        var baseline = Y + padding + metrics.Ascent;
        foreach (var line in lines)
        {
            PdfParagraphPainter.DrawLine(Page, line, x + padding, baseline, width - (2 * padding), font, size, style.TextColor, style.TextAlignment);
            baseline += metrics.LineHeight;
        }

        if (style.Border is { } border)
        {
            Page.StrokeRectangle(x, Y, width, height, border, style.BorderWidth);
        }

        Y += height;
    }

    /// <summary>Adds a table; rows never split, header rows repeat on each page.</summary>
    public void AddTable(PdfTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        ThrowIfFinished();
        new PdfTablePainter(this, table).Paint();
    }

    /// <summary>Draws the footers. Adding content or a page afterwards throws <see cref="InvalidOperationException"/>,
    /// since a later page would have no footer.</summary>
    public void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        for (var i = 0; i < _pages.Count; i++)
        {
            _options.Footer?.Invoke(_pages[i], i + 1, _pages.Count);
        }
    }

    private static double Offset(PdfTextAlignment alignment, double free) => alignment switch
    {
        PdfTextAlignment.Center => free / 2,
        PdfTextAlignment.Right => free,
        _ => 0,
    };

    private void ThrowIfFinished()
    {
        if (_finished)
        {
            throw new InvalidOperationException("The layout is finished: its footers are drawn and nothing can be added.");
        }
    }
}

/// <summary>Draws one wrapped line with an alignment (justification spreads the spaces).</summary>
internal static class PdfParagraphPainter
{
    public static void DrawLine(PdfCanvas page, WrappedLine line, double x, double baseline, double width, PdfFont font, double size, PdfColor? color, PdfTextAlignment alignment)
    {
        switch (alignment)
        {
            case PdfTextAlignment.Center:
                page.DrawText(line.Text, x + ((width - line.Width) / 2), baseline, font, size, color);
                break;
            case PdfTextAlignment.Right:
                page.DrawText(line.Text, x + width - line.Width, baseline, font, size, color);
                break;
            case PdfTextAlignment.Justify when !line.EndsParagraph && line.Text.Contains(' '):
                // Each word is drawn with its following space glyph, so text extraction still sees the spaces;
                // the extra room is spread over the gaps.
                var words = line.Text.Split(' ');
                var space = page.MeasureText(" ", font, size);
                var wordsWidth = words.Sum(w => page.MeasureText(w, font, size));
                var extra = (width - wordsWidth - (space * (words.Length - 1))) / (words.Length - 1);
                for (var i = 0; i < words.Length; i++)
                {
                    var last = i == words.Length - 1;
                    x += page.DrawText(last ? words[i] : words[i] + " ", x, baseline, font, size, color) + (last ? 0 : extra);
                }

                break;
            default:
                page.DrawText(line.Text, x, baseline, font, size, color);
                break;
        }
    }
}
