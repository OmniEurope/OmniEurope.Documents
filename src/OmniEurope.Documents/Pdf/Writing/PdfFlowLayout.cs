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

    /// <summary>Drawn on each page when it is created (letterhead, header); receives the page and its number.</summary>
    public Action<PdfCanvas, int>? Header { get; init; }

    /// <summary>Drawn on every page once the flow is finished; receives the page, its number and the page
    /// count, so "page 3 / 7" footers are possible.</summary>
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
    private bool _finished;

    /// <summary>Starts a flow on a new page of <paramref name="document"/>.</summary>
    public PdfFlowLayout(PdfDocumentBuilder document, PdfFlowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _options = options ?? new PdfFlowOptions();
        Page = null!;
        NewPage();
    }

    /// <summary>The document.</summary>
    public PdfDocumentBuilder Document => _document;

    /// <summary>The page being filled.</summary>
    public PdfCanvas Page { get; private set; }

    /// <summary>The vertical position of the next element.</summary>
    public double Y { get; set; }

    /// <summary>Left edge of the content area.</summary>
    public double Left => _options.Margins.Left;

    /// <summary>Width of the content area.</summary>
    public double ContentWidth => _options.PageWidth - _options.Margins.Left - _options.Margins.Right;

    /// <summary>Lowest position content may reach.</summary>
    public double Bottom => _options.PageHeight - _options.Margins.Bottom;

    /// <summary>Space left on the current page.</summary>
    public double Remaining => Bottom - Y;

    /// <summary>Starts a new page.</summary>
    public PdfCanvas NewPage()
    {
        ThrowIfFinished();
        Page = _document.AddPage(_options.PageWidth, _options.PageHeight);
        _pages.Add(Page);
        Y = _options.Margins.Top;
        _options.Header?.Invoke(Page, _pages.Count);
        return Page;
    }

    /// <summary>Starts a new page unless <paramref name="height"/> points still fit on this one.</summary>
    public void EnsureSpace(double height)
    {
        ThrowIfFinished();
        if (Y + height > Bottom && Y > _options.Margins.Top)
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
        var x = alignment switch
        {
            PdfTextAlignment.Center => Left + ((ContentWidth - width) / 2),
            PdfTextAlignment.Right => Left + ContentWidth - width,
            _ => Left,
        };
        Page.DrawImage(image, x, Y, width, height);
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
