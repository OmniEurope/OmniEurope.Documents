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

    /// <summary>A pattern used as the fill colour (null for a plain colour).</summary>
    public PatternPaint? FillPaint { get; set; }

    /// <summary>A pattern used as the stroke colour (null for a plain colour).</summary>
    public PatternPaint? StrokePaint { get; set; }

    /// <summary>The underlying space of a <c>[/Pattern base]</c> fill space, for uncoloured tiling patterns.</summary>
    public PdfColorSpace? FillUnderlying { get; set; }

    public PdfColorSpace? StrokeUnderlying { get; set; }

    public PdfColor StrokeColor { get; set; } = PdfColor.Black;

    public double FillAlpha { get; set; } = 1;

    public double StrokeAlpha { get; set; } = 1;

    public BlendMode Mode { get; set; }

    /// <summary>The soft mask of the ExtGState SMask entry, null for none.</summary>
    public SoftMask? Mask { get; set; }

    /// <summary>The AIS flag: the soft mask and alpha constant are shape rather than opacity.</summary>
    public bool AlphaIsShape { get; set; }

    /// <summary>The clip, blend mode, soft mask and AIS flag objects are painted with.</summary>
    public Compositing Compositing => new(Clip, Mode, Mask, AlphaIsShape);

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
