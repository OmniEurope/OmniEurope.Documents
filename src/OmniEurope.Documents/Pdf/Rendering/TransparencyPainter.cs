// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>A soft mask in device pixels: a value from 0 to 1 per pixel of its bounds, one value outside them.</summary>
internal sealed class SoftMask(PixelBounds bounds, float[] values, double outside)
{
    public double Value(int x, int y) => bounds.Contains(x, y) ? values[((y - bounds.Top) * bounds.Width) + x - bounds.Left] : outside;
}

/// <summary>
/// Transparency groups and soft masks (ISO 32000-1 §11.4, §11.5, §11.6.5). A group form is drawn on its own
/// <see cref="GroupCanvas"/> with the blend mode, alpha constants and soft mask reset, then composited as one object
/// with those of the graphics state at <c>Do</c>. A soft mask dictionary renders its group in the coordinates of the
/// <c>gs</c> operator: Alpha masks keep the group's alpha, Luminosity masks the luminosity of the group composited
/// on its BC backdrop; the TR function then applies, also to the value outside the group's box.
/// </summary>
internal sealed class TransparencyPainter(PageRenderer page)
{
    /// <summary>The group dictionary of a form when it is a transparency group.</summary>
    public static PdfDictionary? Group(PdfObjectStore store, PdfStream form) =>
        store.Get<PdfDictionary>(form, "Group") is { } group && store.Get(group, "S") is PdfName { Value: "Transparency" } ? group : null;

    /// <summary>Runs the content of a group form, whose matrix and box clip are already in the graphics state.</summary>
    public void PaintGroup(PdfStream form, PdfDictionary group, PdfDictionary? resources, (double X0, double Y0, double X1, double Y1)? box)
    {
        var (store, state, target) = (page.Store, page.State, page.Surface);
        var bounds = Bounds(state.Ctm, box, target);
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var isolated = store.Get(group, "I") is PdfBoolean { Value: true };
        var knockout = store.Get(group, "K") is PdfBoolean { Value: true };
        var canvas = new GroupCanvas(target.Width, target.Height, bounds, isolated ? null : target, knockout);
        var (opacity, compositing) = (state.FillAlpha, state.Compositing with { Clip = null });
        var inner = state with { FillAlpha = 1, StrokeAlpha = 1, Mode = BlendMode.Normal, Mask = null, AlphaIsShape = false };
        page.RunOn(canvas, inner, null, () => page.Run(store.DecodeBytes(form), resources));
        canvas.CompositeOnto(target, opacity, compositing);
    }

    /// <summary>The soft mask an ExtGState SMask dictionary gives under the current matrix, null when it cannot be read.</summary>
    public SoftMask? Mask(PdfDictionary dictionary, PdfDictionary? resources)
    {
        var store = page.Store;
        var kind = (store.Get(dictionary, "S") as PdfName)?.Value;
        if (store.Get(dictionary, "G") is not PdfStream form || kind is not ("Luminosity" or "Alpha"))
        {
            page.Context.Gap("soft masks that cannot be read are not applied");
            return null;
        }

        var luminosity = kind == "Luminosity";
        var transfer = Transfer(store.Get(dictionary, "TR"));
        var backdrop = luminosity ? Backdrop(store, dictionary, form, resources) : default;
        var (state, target) = (page.State, page.Surface);
        var formMatrix = Matrix.FromArray(store.Get<PdfArray>(form, "Matrix")).Multiply(state.Ctm);
        var bounds = Bounds(formMatrix, page.FormBox(form), target);
        var canvas = new GroupCanvas(target.Width, target.Height, bounds, null, false);
        if (luminosity)
        {
            canvas.Clear(backdrop.R, backdrop.G, backdrop.B, 1);
        }

        page.RunOn(canvas, new GraphicsState { Ctm = state.Ctm }, null, () => page.RunForm(form, resources));
        var values = new float[bounds.Width * bounds.Height];
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                var (r, g, b, a) = canvas.Read(x, y);
                values[((y - bounds.Top) * bounds.Width) + x - bounds.Left] = (float)transfer(luminosity ? Blending.Lum((r / 255, g / 255, b / 255)) : a);
            }
        }

        var outside = transfer(luminosity ? Blending.Lum((backdrop.R / 255.0, backdrop.G / 255.0, backdrop.B / 255.0)) : 0);
        return new SoftMask(bounds, values, outside);
    }

    private static PixelBounds Bounds(Matrix toDevice, (double X0, double Y0, double X1, double Y1)? box, Canvas target) =>
        (box is { } b ? PixelBounds.Of(toDevice, b, target.Width, target.Height) : target.Bounds).Intersect(target.Bounds);

    // BC in the group's colour space (black by default), as RGB.
    private static PdfColor Backdrop(PdfObjectStore store, PdfDictionary dictionary, PdfStream form, PdfDictionary? resources)
    {
        var values = ShadingFunction.Numbers(store, dictionary, "BC");
        if (values is not { Length: > 0 })
        {
            return PdfColor.Black;
        }

        var space = store.Get(store.Get<PdfDictionary>(form, "Group"), "CS") is { } cs
            ? PdfColorSpace.Resolve(store, cs, store.Get<PdfDictionary>(form, "Resources") ?? resources)
            : values.Length switch { 1 => PdfColorSpace.Gray, 4 => PdfColorSpace.Cmyk, _ => PdfColorSpace.Rgb };
        return GraphicsState.ToRgb(space, values);
    }

    // The TR entry: Identity (or absent), or a one-input function from 0..1 to 0..1.
    private Func<double, double> Transfer(PdfObject? value)
    {
        if (value is null or PdfName { Value: "Identity" })
        {
            return v => v;
        }

        if (ShadingFunction.Read(page.Store, value) is { } function)
        {
            return v => Math.Clamp(function.Evaluate(v) is { Length: > 0 } output ? output[0] : v, 0, 1);
        }

        page.Context.Gap("soft mask transfer functions that cannot be read are ignored");
        return v => v;
    }
}
