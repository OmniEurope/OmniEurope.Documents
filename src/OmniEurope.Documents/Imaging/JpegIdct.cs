// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>Dequantisation and the inverse DCT of every block of a component, giving a sample plane.</summary>
internal static class JpegIdct
{
    // cos((2x + 1) u pi / 16) scaled by C(u) / 2, with C(0) = 1/sqrt(2).
    private static readonly float[] Basis = BuildBasis();

    private static float[] BuildBasis()
    {
        var basis = new float[64];
        for (var u = 0; u < 8; u++)
        {
            var scale = u == 0 ? 1 / Math.Sqrt(2) : 1;
            for (var x = 0; x < 8; x++)
            {
                basis[(u * 8) + x] = (float)(scale / 2 * Math.Cos(((2 * x) + 1) * u * Math.PI / 16));
            }
        }

        return basis;
    }

    /// <summary>A plane of <c>BlocksPerLine * 8</c> by <c>BlocksPerColumn * 8</c> samples.</summary>
    public static JpegPlane Plane(JpegComponent component, ushort[] quant)
    {
        var width = component.BlocksPerLine * 8;
        var height = component.BlocksPerColumn * 8;
        var samples = new byte[(long)width * height];
        Span<float> block = stackalloc float[64];
        Span<float> temp = stackalloc float[64];
        for (var row = 0; row < component.BlocksPerColumn; row++)
        {
            for (var column = 0; column < component.BlocksPerLine; column++)
            {
                var coefficients = component.Coefficients.AsSpan(component.BlockOffset(row, column), 64);
                for (var i = 0; i < 64; i++)
                {
                    block[i] = coefficients[i] * quant[i];
                }

                Inverse(block, temp);
                var origin = ((long)row * 8 * width) + (column * 8);
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var value = (int)MathF.Round(block[(y * 8) + x] + 128);
                        samples[origin + (y * width) + x] = (byte)Math.Clamp(value, 0, 255);
                    }
                }
            }
        }

        component.Coefficients = [];
        return new JpegPlane(width, height, component.H, component.V, samples);
    }

    // Separable 2-D inverse DCT: rows then columns, in place in `block`.
    private static void Inverse(Span<float> block, Span<float> temp)
    {
        for (var v = 0; v < 8; v++)
        {
            for (var x = 0; x < 8; x++)
            {
                float sum = 0;
                for (var u = 0; u < 8; u++)
                {
                    sum += Basis[(u * 8) + x] * block[(v * 8) + u];
                }

                temp[(v * 8) + x] = sum;
            }
        }

        for (var x = 0; x < 8; x++)
        {
            for (var y = 0; y < 8; y++)
            {
                float sum = 0;
                for (var v = 0; v < 8; v++)
                {
                    sum += Basis[(v * 8) + y] * temp[(v * 8) + x];
                }

                block[(y * 8) + x] = sum;
            }
        }
    }
}

/// <summary>The decoded samples of one component and its sampling factors.</summary>
internal sealed record JpegPlane(int Width, int Height, int H, int V, byte[] Samples);

/// <summary>Upsampling and colour conversion to the output image.</summary>
internal static class JpegColor
{
    public static RasterImage Convert(JpegFrame frame, List<JpegPlane> planes, int adobeTransform)
    {
        var count = planes.Count;
        var type = count switch
        {
            1 => ImageColorType.Gray,
            3 => ImageColorType.Rgb,
            _ => ImageColorType.Cmyk,
        };
        var image = new RasterImage(frame.Width, frame.Height, type);
        var transform = count switch
        {
            3 => adobeTransform == 0 || IsRgbIds(frame) ? 0 : 1,
            4 => adobeTransform == 2 ? 2 : 0,
            _ => 0,
        };
        var invert = count == 4 && adobeTransform >= 0;
        Span<byte> sample = stackalloc byte[4];
        var output = image.Pixels;
        var o = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                for (var c = 0; c < count; c++)
                {
                    sample[c] = Upsample(planes[c], frame, x, y);
                }

                o = Write(output, o, sample[..count], transform, invert);
            }
        }

        return image;
    }

    // Full-resolution sample of a component. Halved directions are interpolated with the 3/4-1/4 triangle
    // filter between the two nearest samples (edges replicated), other ratios repeat samples.
    private static byte Upsample(JpegPlane plane, JpegFrame frame, int x, int y)
    {
        var factorX = frame.MaxH / plane.H;
        var factorY = frame.MaxV / plane.V;
        var usedWidth = ((frame.Width * plane.H) + frame.MaxH - 1) / frame.MaxH;
        var usedHeight = ((frame.Height * plane.V) + frame.MaxV - 1) / frame.MaxV;
        var (x0, x1, wx) = Neighbours(x, factorX, usedWidth);
        var (y0, y1, wy) = Neighbours(y, factorY, usedHeight);
        var s = plane.Samples;
        var w = plane.Width;
        var top = (s[((long)y0 * w) + x0] * (4 - wx)) + (s[((long)y0 * w) + x1] * wx);
        var bottom = (s[((long)y1 * w) + x0] * (4 - wx)) + (s[((long)y1 * w) + x1] * wx);
        return (byte)(((top * (4 - wy)) + (bottom * wy) + 8) >> 4);
    }

    // The two source samples around an output position and the weight (out of 4) of the second one.
    private static (int First, int Second, int Weight) Neighbours(int position, int factor, int used)
    {
        if (factor != 2)
        {
            var only = Math.Min(position / factor, used - 1);
            return (only, only, 0);
        }

        var nearest = Math.Min(position >> 1, used - 1);
        var other = (position & 1) == 0 ? Math.Max(nearest - 1, 0) : Math.Min(nearest + 1, used - 1);
        return (nearest, other, 1);
    }

    private static bool IsRgbIds(JpegFrame frame) =>
        frame.Components[0].Id == 'R' && frame.Components[1].Id == 'G' && frame.Components[2].Id == 'B';

    private static int Write(byte[] output, int o, ReadOnlySpan<byte> s, int transform, bool invert)
    {
        if (s.Length == 1)
        {
            output[o] = s[0];
            return o + 1;
        }

        if (s.Length == 3)
        {
            if (transform == 1)
            {
                YccToRgb(s[0], s[1], s[2], output.AsSpan(o, 3));
            }
            else
            {
                s.CopyTo(output.AsSpan(o, 3));
            }

            return o + 3;
        }

        WriteInks(output.AsSpan(o, 4), s, ycck: transform == 2, invert);
        return o + 4;
    }

    // Four components: CMYK, or YCCK (Adobe transform 2), whose YCbCr part decodes to the complement of the
    // C, M and Y samples. Adobe files store inverted inks (255 = none); our CMYK means 0 = none.
    private static void WriteInks(Span<byte> output, ReadOnlySpan<byte> s, bool ycck, bool invert)
    {
        if (ycck)
        {
            YccToRgb(s[0], s[1], s[2], output);
            for (var c = 0; c < 3; c++)
            {
                output[c] = (byte)(255 - output[c]);
            }
        }
        else
        {
            s[..3].CopyTo(output);
        }

        output[3] = s[3];
        for (var c = 0; invert && c < 4; c++)
        {
            output[c] = (byte)(255 - output[c]);
        }
    }

    private static void YccToRgb(byte luma, byte blue, byte red, Span<byte> rgb)
    {
        var cb = blue - 128f;
        var cr = red - 128f;
        rgb[0] = Clamp(luma + (1.402f * cr));
        rgb[1] = Clamp(luma - (0.344136f * cb) - (0.714136f * cr));
        rgb[2] = Clamp(luma + (1.772f * cb));
    }

    private static byte Clamp(float value) => (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
}
