// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>The graphics state saved by <c>q</c> and restored by <c>Q</c>.</summary>
internal sealed record GraphicsState
{
    public Matrix Ctm { get; set; } = Matrix.Identity;

    public ClipMask? Clip { get; set; }

    public PdfColorSpace FillSpace { get; set; } = PdfColorSpace.Gray;

    public PdfColorSpace StrokeSpace { get; set; } = PdfColorSpace.Gray;

    public PdfColor FillColor { get; set; } = PdfColor.Black;

    /// <summary>A shading pattern used as the fill colour, with the matrix from shading space to device pixels.</summary>
    public (Shading Shading, Matrix ToDevice)? FillPattern { get; set; }

    public PdfColor StrokeColor { get; set; } = PdfColor.Black;

    public double FillAlpha { get; set; } = 1;

    public double StrokeAlpha { get; set; } = 1;

    public double LineWidth { get; set; } = 1;

    public int LineCap { get; set; }

    public int LineJoin { get; set; }

    public double MiterLimit { get; set; } = 10;

    public double[]? Dash { get; set; }

    public double DashPhase { get; set; }

    public RenderFont? Font { get; set; }

    public double FontSize { get; set; } = 1;

    public double CharSpacing { get; set; }

    public double WordSpacing { get; set; }

    public double HorizontalScale { get; set; } = 1;

    public double Leading { get; set; }

    public double Rise { get; set; }

    public int RenderMode { get; set; }

    /// <summary>A colour from operands in a colour space, as RGB.</summary>
    public static PdfColor ToRgb(PdfColorSpace space, double[] operands)
    {
        var samples = new double[Math.Max(space.Components, operands.Length)];
        operands.CopyTo(samples, 0);
        var output = new byte[4];
        space.Write(samples, output, 0);
        return space.Output switch
        {
            ImageColorType.Gray => new PdfColor(output[0], output[0], output[0]),
            ImageColorType.Cmyk => FromCmyk(output),
            _ => new PdfColor(output[0], output[1], output[2]),
        };
    }

    public static PdfColor FromCmyk(ReadOnlySpan<byte> cmyk)
    {
        var k = cmyk[3] / 255.0;
        byte Channel(byte value) => (byte)Math.Round(255 * (1 - (value / 255.0)) * (1 - k));
        return new PdfColor(Channel(cmyk[0]), Channel(cmyk[1]), Channel(cmyk[2]));
    }
}
