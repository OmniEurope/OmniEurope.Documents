// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>The font, size (0 for automatic) and colour of a field's default appearance string (ISO 32000-1 §12.7.3.3).</summary>
internal readonly record struct DefaultAppearance(PdfFont Font, double Size, PdfColor Color);

/// <summary>
/// Writes the normal appearance streams of filled fields (ISO 32000-1 §12.7.3.3, §12.5.5): form XObjects the size
/// of the widget rectangle with its background and border, then the value between <c>/Tx BMC</c> and <c>EMC</c>,
/// clipped to the padded box. Text is drawn with the document builder's embedded font subsets, so any Unicode
/// value shows in viewers that do not regenerate appearances.
/// </summary>
internal sealed class FieldAppearance(PdfDocumentBuilder builder, PdfObjectStore store, PdfDictionary? acroForm)
{
    private const double AutoMaximum = 12;
    private static readonly PdfColor Highlight = new(153, 193, 218);

    // Acrobat's usual resource names and the PostScript names of the standard families, as the font library knows them.
    private static readonly Dictionary<string, string> FamilyAliases = new(StringComparer.Ordinal)
    {
        ["Helv"] = "Helvetica", ["HeBo"] = "Helvetica", ["TiRo"] = "Times New Roman", ["TiBo"] = "Times New Roman", ["TimesNewRoman"] = "Times New Roman",
        ["TimesNewRomanPS"] = "Times New Roman", ["Cour"] = "Courier New", ["CoBo"] = "Courier New", ["CourierNew"] = "Courier New", ["CourierNewPS"] = "Courier New",
    };

    private static readonly HashSet<string> BoldResourceNames = new(StringComparer.Ordinal) { "HeBo", "TiBo", "CoBo" };

    /// <summary>A single or multiple line text, or the value a combo box shows.</summary>
    public PdfReference Text(FormFieldNode field, FormWidgetNode widget, string text)
    {
        var (width, height) = Size(widget);
        var look = Look(field);
        var frame = Frame(widget, width, height, out var padding);
        var canvas = builder.CreateDetachedCanvas(width, height);
        canvas.SaveState();
        canvas.ClipRectangle(padding / 2, padding / 2, width - padding, height - padding);
        var shown = (field.Flags & (1 << 13)) != 0 ? new string('*', text.EnumerateRunes().Count()) : text;
        var comb = (field.Flags & (1 << 24)) != 0 && field.Kind == PdfFieldKind.Text ? MaxLength(field) : 0;
        if (comb > 0)
        {
            Comb(canvas, look, shown, width, height, comb);
        }
        else if (field.Kind == PdfFieldKind.Text && (field.Flags & (1 << 12)) != 0)
        {
            Multiline(canvas, look, shown, field.Quadding, width, height, padding);
        }
        else
        {
            SingleLine(canvas, look, shown, field.Quadding, width, height, padding);
        }

        canvas.RestoreState();
        return Form(width, height, frame, canvas);
    }

    /// <summary>A list box: its options from the top index, the selected ones highlighted.</summary>
    public PdfReference List(FormFieldNode field, FormWidgetNode widget, IReadOnlyList<PdfChoiceOption> options, IReadOnlySet<string> selected)
    {
        var (width, height) = Size(widget);
        var look = Look(field);
        var size = look.Size > 0 ? look.Size : AutoMaximum;
        var frame = Frame(widget, width, height, out var padding);
        var canvas = builder.CreateDetachedCanvas(width, height);
        var metrics = canvas.Metrics(look.Font, size);
        canvas.SaveState();
        canvas.ClipRectangle(padding / 2, padding / 2, width - padding, height - padding);
        var top = Math.Clamp((int)store.Number(field.Dictionary, "TI"), 0, Math.Max(0, options.Count - 1));
        for (var i = top; i < options.Count && (i - top) * metrics.LineHeight < height; i++)
        {
            var y = padding + ((i - top) * metrics.LineHeight);
            if (selected.Contains(options[i].ExportValue))
            {
                canvas.FillRectangle(padding / 2, y, width - padding, metrics.LineHeight, Highlight);
            }

            canvas.DrawText(options[i].DisplayText, padding, y + metrics.Ascent, look.Font, size, look.Color);
        }

        canvas.RestoreState();
        return Form(width, height, frame, canvas);
    }

    /// <summary>The on and Off appearances of a check box (a check mark) or a radio button (a dot).</summary>
    public PdfDictionary Toggle(FormFieldNode field, FormWidgetNode widget, string onState)
    {
        var (width, height) = Size(widget);
        var look = Look(field);
        var states = new PdfDictionary();
        foreach (var on in new[] { true, false })
        {
            var frame = Frame(widget, width, height, out _);
            var canvas = builder.CreateDetachedCanvas(width, height);
            if (on && field.Kind == PdfFieldKind.RadioButton)
            {
                var radius = Math.Min(width, height) / 4;
                canvas.FillPolygon(Enumerable.Range(0, 32).Select(i => ((width / 2) + (radius * Math.Cos(i * Math.PI / 16)), (height / 2) + (radius * Math.Sin(i * Math.PI / 16)))).ToList(), look.Color);
            }
            else if (on)
            {
                canvas.StrokePolyline([(0.2 * width, 0.55 * height), (0.42 * width, 0.78 * height), (0.8 * width, 0.22 * height)], look.Color, Math.Max(1, Math.Min(width, height) / 10));
            }

            states.Set(on ? onState : "Off", Form(width, height, frame, canvas, variableText: false));
        }

        return states;
    }

    /// <summary>The font, size and colour of a field's DA string, the form's when the field has none.</summary>
    public DefaultAppearance Look(FormFieldNode field)
    {
        string? fontName = null;
        double size = 0;
        var color = PdfColor.Black;
        foreach (var operation in ContentStreamReader.Read(Encoding.Latin1.GetBytes(field.DefaultAppearance ?? string.Empty)))
        {
            var operands = operation.Operands.Select(o => o is PdfNumber n ? n.Value : 0).ToArray();
            switch (operation.Operator)
            {
                case "Tf" when operation.Operands.Count >= 2:
                    fontName = (operation.Operands[^2] as PdfName)?.Value;
                    size = Math.Max(0, operands[^1]);
                    break;
                case "g" when operands.Length >= 1:
                    color = Rgb(operands[^1], operands[^1], operands[^1]);
                    break;
                case "rg" when operands.Length >= 3:
                    color = Rgb(operands[^3], operands[^2], operands[^1]);
                    break;
                case "k" when operands.Length >= 4:
                    color = Rgb((1 - operands[^4]) * (1 - operands[^1]), (1 - operands[^3]) * (1 - operands[^1]), (1 - operands[^2]) * (1 - operands[^1]));
                    break;
            }
        }

        return new DefaultAppearance(FontFor(fontName), size, color);
    }

    // The family and style of a resource font: its base font name (subset tag removed), or Acrobat's usual resource names.
    private PdfFont FontFor(string? resourceName)
    {
        var font = resourceName is null ? null : store.Get(store.Get<PdfDictionary>(store.Get<PdfDictionary>(acroForm, "DR"), "Font"), resourceName) as PdfDictionary;
        var baseName = (store.Get(font, "BaseFont") as PdfName)?.Value ?? resourceName ?? "Helvetica";
        baseName = baseName[(baseName.IndexOf('+', StringComparison.Ordinal) + 1)..];
        var family = baseName.Split('-', ',')[0];
        var bold = baseName.Contains("Bold", StringComparison.OrdinalIgnoreCase) || BoldResourceNames.Contains(family);
        var italic = baseName.Contains("Italic", StringComparison.OrdinalIgnoreCase) || baseName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
        return new PdfFont(FamilyAliases.GetValueOrDefault(family, family), bold, italic);
    }

    private static void SingleLine(PdfCanvas canvas, DefaultAppearance look, string text, int quadding, double width, double height, double padding)
    {
        if (text.Length == 0)
        {
            return;
        }

        var size = look.Size;
        if (size <= 0)
        {
            var fitHeight = (height - (2 * padding)) / canvas.Metrics(look.Font, 1).LineHeight;
            var fitWidth = (width - (2 * padding)) / Math.Max(canvas.MeasureText(text, look.Font, 1), 1e-6);
            size = Math.Clamp(Math.Min(Math.Min(fitHeight, fitWidth), AutoMaximum), 1, AutoMaximum);
        }

        canvas.DrawTextInBox(text, padding, 0, width - (2 * padding), height, look.Font, size, look.Color, Alignment(quadding));
    }

    private void Multiline(PdfCanvas canvas, DefaultAppearance look, string text, int quadding, double width, double height, double padding)
    {
        var size = look.Size > 0 ? look.Size : AutoMaximum;
        var lines = TextWrapper.Wrap(builder, text, look.Font, size, width - (2 * padding));
        while (look.Size <= 0 && size > 4 && lines.Count * canvas.Metrics(look.Font, size).LineHeight > height - (2 * padding))
        {
            size -= 0.5;
            lines = TextWrapper.Wrap(builder, text, look.Font, size, width - (2 * padding));
        }

        var metrics = canvas.Metrics(look.Font, size);
        for (var i = 0; i < lines.Count; i++)
        {
            var x = quadding switch
            {
                1 => (width - lines[i].Width) / 2,
                2 => width - padding - lines[i].Width,
                _ => padding,
            };
            canvas.DrawText(lines[i].Text, x, padding + metrics.Ascent + (i * metrics.LineHeight), look.Font, size, look.Color);
        }
    }

    // A comb field: the box is cut into MaxLen equal cells, one character centred in each.
    private static void Comb(PdfCanvas canvas, DefaultAppearance look, string text, double width, double height, int cells)
    {
        var cell = width / cells;
        var size = look.Size > 0 ? look.Size : Math.Min(AutoMaximum, height * 0.6);
        var index = 0;
        foreach (var rune in text.EnumerateRunes().Take(cells))
        {
            canvas.DrawTextInBox(rune.ToString(), index * cell, 0, cell, height, look.Font, size, look.Color, PdfTextAlignment.Center);
            index++;
        }
    }

    // The background (MK BG) and border (MK BC, width from BS or 1) as a content stream; the padding inside the border.
    private byte[] Frame(FormWidgetNode widget, double width, double height, out double padding)
    {
        var characteristics = store.Get<PdfDictionary>(widget.Dictionary, "MK");
        var border = Color(store.Get<PdfArray>(characteristics, "BC"));
        var borderWidth = border is null ? 0 : store.Get(store.Get<PdfDictionary>(widget.Dictionary, "BS"), "W") is PdfNumber w ? w.Value : 1;
        padding = Math.Max(1, borderWidth * 2);
        var frame = builder.CreateDetachedCanvas(width, height);
        if (Color(store.Get<PdfArray>(characteristics, "BG")) is { } background)
        {
            frame.FillRectangle(0, 0, width, height, background);
        }

        if (border is { } borderColor && borderWidth > 0)
        {
            frame.StrokeRectangle(borderWidth / 2, borderWidth / 2, width - borderWidth, height - borderWidth, borderColor, borderWidth);
        }

        return frame.FinishContent();
    }

    private PdfReference Form(double width, double height, byte[] frame, PdfCanvas canvas, bool variableText = true)
    {
        byte[] content = variableText ? [.. frame, .. "/Tx BMC\n"u8, .. canvas.FinishContent(), .. "EMC\n"u8] : [.. frame, .. canvas.FinishContent()];
        var stream = EmbeddedFont.Stream(content);
        stream.SetName("Type", "XObject").SetName("Subtype", "Form").Set("BBox", PdfArray.OfNumbers(0, 0, width, height)).Set("Resources", canvas.BuildResources());
        return builder.Table.Add(stream);
    }

    private (double Width, double Height) Size(FormWidgetNode widget)
    {
        var rectangle = AnnotationAppearance.Rectangle(store, widget.Dictionary);
        return (Math.Max(rectangle.Width, 1), Math.Max(rectangle.Height, 1));
    }

    private int MaxLength(FormFieldNode field) => (int)store.Number(field.Dictionary, "MaxLen");

    private PdfColor? Color(PdfArray? components)
    {
        var values = components?.Items.Select(i => store.Resolve(i) is PdfNumber n ? n.Value : 0).ToArray() ?? [];
        return values.Length switch
        {
            1 => Rgb(values[0], values[0], values[0]),
            3 => Rgb(values[0], values[1], values[2]),
            4 => Rgb((1 - values[0]) * (1 - values[3]), (1 - values[1]) * (1 - values[3]), (1 - values[2]) * (1 - values[3])),
            _ => null,
        };
    }

    private static PdfColor Rgb(double r, double g, double b) =>
        new((byte)Math.Round(Math.Clamp(r, 0, 1) * 255), (byte)Math.Round(Math.Clamp(g, 0, 1) * 255), (byte)Math.Round(Math.Clamp(b, 0, 1) * 255));

    private static PdfTextAlignment Alignment(int quadding) => quadding switch
    {
        1 => PdfTextAlignment.Center,
        2 => PdfTextAlignment.Right,
        _ => PdfTextAlignment.Left,
    };
}
