// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;

namespace OmniEurope.Documents.Conversion.Emf;

/// <summary>A 2D affine transform (EMF <c>XFORM</c>): x' = x·M11 + y·M21 + Dx, y' = x·M12 + y·M22 + Dy.</summary>
internal readonly record struct EmfTransform(double M11, double M12, double M21, double M22, double Dx, double Dy)
{
    public static EmfTransform Identity => new(1, 0, 0, 1, 0, 0);

    public (double X, double Y) Apply(double x, double y) => ((x * M11) + (y * M21) + Dx, (x * M12) + (y * M22) + Dy);

    /// <summary>This transform followed by <paramref name="next"/>.</summary>
    public EmfTransform Then(EmfTransform next) => new(
        (M11 * next.M11) + (M12 * next.M21),
        (M11 * next.M12) + (M12 * next.M22),
        (M21 * next.M11) + (M22 * next.M21),
        (M21 * next.M12) + (M22 * next.M22),
        (Dx * next.M11) + (Dy * next.M21) + next.Dx,
        (Dx * next.M12) + (Dy * next.M22) + next.Dy);
}

/// <summary>A GDI pen: colour and width in logical units, or no line.</summary>
internal sealed record EmfPen(PdfColor Color, double Width, bool Visible);

/// <summary>A GDI brush: a solid colour or no fill (hatches are drawn solid).</summary>
internal sealed record EmfBrush(PdfColor Color, bool Visible);

/// <summary>A GDI font: height in logical units (negative for the em height), weight, italic, face.</summary>
internal sealed record EmfFont(double Height, int Weight, bool Italic, string Face, int Escapement);

/// <summary>The device-context state that <c>SaveDC</c> saves and <c>RestoreDC</c> restores.</summary>
internal sealed record EmfState
{
    public EmfPen Pen { get; init; } = new(PdfColor.Black, 0, true);

    public EmfBrush Brush { get; init; } = new(PdfColor.White, true);

    public EmfFont Font { get; init; } = new(-12, 400, false, "Arial", 0);

    public PdfColor TextColor { get; init; } = PdfColor.Black;

    public int TextAlign { get; init; }

    public bool EvenOdd { get; init; } = true;

    public int MapMode { get; init; } = 1;

    public (double X, double Y) WindowOrigin { get; init; }

    public (double X, double Y) WindowExtent { get; init; } = (1, 1);

    public (double X, double Y) ViewportOrigin { get; init; }

    public (double X, double Y) ViewportExtent { get; init; } = (1, 1);

    public EmfTransform World { get; init; } = EmfTransform.Identity;

    public (double X, double Y) Position { get; init; }

    /// <summary>Logical to device coordinates, given the device pixels per millimetre for fixed map modes.</summary>
    public EmfTransform PageTransform((double X, double Y) pixelsPerMm)
    {
        var (sx, sy) = MapMode switch
        {
            7 or 8 => (Ratio(ViewportExtent.X, WindowExtent.X), Ratio(ViewportExtent.Y, WindowExtent.Y)),
            2 => (pixelsPerMm.X / 10, -pixelsPerMm.Y / 10),
            3 => (pixelsPerMm.X / 100, -pixelsPerMm.Y / 100),
            4 => (pixelsPerMm.X * 0.254, -pixelsPerMm.Y * 0.254),
            5 => (pixelsPerMm.X * 0.0254, -pixelsPerMm.Y * 0.0254),
            6 => (pixelsPerMm.X * 25.4 / 1440, -pixelsPerMm.Y * 25.4 / 1440),
            _ => (1.0, 1.0),
        };
        if (MapMode == 8)
        {
            // Isotropic: the same scale on both axes, the smaller one.
            var scale = Math.Min(Math.Abs(sx), Math.Abs(sy));
            (sx, sy) = (Math.Sign(sx) * scale, Math.Sign(sy) * scale);
        }

        var page = new EmfTransform(sx, 0, 0, sy, ViewportOrigin.X - (WindowOrigin.X * sx), ViewportOrigin.Y - (WindowOrigin.Y * sy));
        return World.Then(page);
    }

    private static double Ratio(double viewport, double window) => window == 0 ? 1 : viewport / window;
}
