// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// Draws image XObjects and inline images: the unit square mapped by the current matrix, each device pixel
/// sampled bilinearly from the image (its alpha and soft mask included); stencil masks paint the fill colour.
/// Images whose data cannot be decoded are reported.
/// </summary>
internal sealed class ImagePainter(PageRenderer page)
{
    public void Draw(PdfDictionary image, byte[]? inlineData, PdfDictionary? resources)
    {
        var store = page.Store;
        var raster = PdfImageDecoder.TryDecode(store, image, inlineData, resources);
        if (raster is null)
        {
            page.Context.Gap("images in an unsupported format are not drawn");
            return;
        }

        var stencil = store.Get(image, "ImageMask") is PdfBoolean { Value: true } || store.Get(image, "IM") is PdfBoolean { Value: true };
        var ctm = page.State.Ctm;
        var determinant = (ctm.A * ctm.D) - (ctm.B * ctm.C);
        if (Math.Abs(determinant) < 1e-12)
        {
            return;
        }

        var inverse = new Matrix(ctm.D / determinant, -ctm.B / determinant, -ctm.C / determinant, ctm.A / determinant,
            ((ctm.C * ctm.F) - (ctm.D * ctm.E)) / determinant, ((ctm.B * ctm.E) - (ctm.A * ctm.F)) / determinant);
        var (left, top, right, bottom) = Bounds(ctm, page.Surface);
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var (u, v) = inverse.Transform(x + 0.5, y + 0.5);
                if (u is >= 0 and < 1 && v is >= 0 and < 1)
                {
                    Pixel(raster, stencil, x, y, u * raster.Width, (1 - v) * raster.Height);
                }
            }
        }
    }

    private static (int Left, int Top, int Right, int Bottom) Bounds(Matrix ctm, Surface surface)
    {
        (double X, double Y)[] corners = [ctm.Transform(0, 0), ctm.Transform(1, 0), ctm.Transform(0, 1), ctm.Transform(1, 1)];
        return (
            Math.Max(0, (int)Math.Floor(corners.Min(c => c.X))),
            Math.Max(0, (int)Math.Floor(corners.Min(c => c.Y))),
            Math.Min(surface.Width, (int)Math.Ceiling(corners.Max(c => c.X))),
            Math.Min(surface.Height, (int)Math.Ceiling(corners.Max(c => c.Y))));
    }

    private void Pixel(RasterImage raster, bool stencil, int x, int y, double sx, double sy)
    {
        var state = page.State;
        var (r, g, b, a) = Sample(raster, sx, sy);
        if (stencil)
        {
            // A stencil sample of 0 (decoded black) is painted with the fill colour.
            var paint = (255 - r) / 255.0;
            page.Surface.Blend(x, y, state.FillColor.R, state.FillColor.G, state.FillColor.B, paint * state.FillAlpha, state.Clip);
            return;
        }

        page.Surface.Blend(x, y, r, g, b, a / 255.0 * state.FillAlpha, state.Clip);
    }

    // Bilinear sampling between the four nearest image pixels.
    private static (byte R, byte G, byte B, byte A) Sample(RasterImage raster, double sx, double sy)
    {
        var fx = Math.Clamp(sx - 0.5, 0, raster.Width - 1);
        var fy = Math.Clamp(sy - 0.5, 0, raster.Height - 1);
        var (x0, y0) = ((int)fx, (int)fy);
        var (x1, y1) = (Math.Min(x0 + 1, raster.Width - 1), Math.Min(y0 + 1, raster.Height - 1));
        var (tx, ty) = (fx - x0, fy - y0);
        var p00 = raster.GetRgba(x0, y0);
        var p10 = raster.GetRgba(x1, y0);
        var p01 = raster.GetRgba(x0, y1);
        var p11 = raster.GetRgba(x1, y1);
        byte Mix(byte a, byte b, byte c, byte d) => (byte)Math.Round(((a * (1 - tx)) + (b * tx)) * (1 - ty) + (((c * (1 - tx)) + (d * tx)) * ty));
        return (Mix(p00.R, p10.R, p01.R, p11.R), Mix(p00.G, p10.G, p01.G, p11.G), Mix(p00.B, p10.B, p01.B, p11.B), Mix(p00.A, p10.A, p01.A, p11.A));
    }
}
