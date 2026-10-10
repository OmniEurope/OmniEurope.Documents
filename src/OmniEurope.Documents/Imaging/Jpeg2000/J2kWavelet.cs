// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// The inverse discrete wavelet transform of a tile-component (ISO/IEC 15444-1 annex F): resolution after
/// resolution, the four sub-bands interleaved (2D_INTERLEAVE), then every row and every column reconstructed
/// (HOR_SR then VER_SR) with the reversible 5-3 filter in integers or the irreversible 9-7 filter in reals, each
/// signal extended symmetrically past its ends (1D_EXTR) and its parity taken from its absolute coordinates.
/// </summary>
internal static class J2kWavelet
{
    // Samples of symmetric extension each side of a signal: enough for the four lifting steps of the 9-7 filter.
    private const int Pad = 6;
    private const float Alpha = -1.586134342059924f;
    private const float Beta = -0.052980118572961f;
    private const float Gamma = 0.882911075530934f;
    private const float Delta = 0.443506852043971f;
    private const float K = 1.230174104914001f;

    public static void Inverse(J2kTileComponent component)
    {
        var resolutions = component.Resolutions;
        if (component.Style.Reversible)
        {
            var samples = resolutions[0].Bands[0].Integers!;
            for (var r = 1; r < resolutions.Length; r++)
            {
                samples = Reconstruct(resolutions[r], resolutions[r - 1], samples, b => b.Integers!, Reversible);
            }

            component.Integers = samples;
        }
        else
        {
            var samples = resolutions[0].Bands[0].Reals!;
            for (var r = 1; r < resolutions.Length; r++)
            {
                samples = Reconstruct(resolutions[r], resolutions[r - 1], samples, b => b.Reals!, Irreversible);
            }

            component.Reals = samples;
        }
    }

    private static T[] Reconstruct<T>(J2kResolution resolution, J2kResolution lower, T[] low, Func<J2kBand, T[]> coefficients, Action<T[], int, int> reconstruct)
        where T : struct
    {
        var width = resolution.X1 - resolution.X0;
        var height = resolution.Y1 - resolution.Y0;
        var output = new T[width * height];
        if (width == 0 || height == 0)
        {
            return output;
        }

        Interleave(output, resolution, low, lower.X0, lower.Y0, lower.X1 - lower.X0, 0, 0);
        foreach (var band in resolution.Bands)
        {
            Interleave(output, resolution, coefficients(band), band.X0, band.Y0, band.Width, band.Orientation & 1, band.Orientation >> 1);
        }

        Filter(output, resolution, reconstruct);
        return output;
    }

    // Places the samples of a band (origin x0, y0 on its own grid) at their positions 2u + xo, 2v + yo.
    private static void Interleave<T>(T[] output, J2kResolution resolution, T[] band, int x0, int y0, int bandWidth, int xo, int yo)
    {
        if (bandWidth == 0)
        {
            return;
        }

        var width = resolution.X1 - resolution.X0;
        var rows = band.Length / bandWidth;
        for (var v = 0; v < rows; v++)
        {
            var y = (2 * (y0 + v)) + yo - resolution.Y0;
            var target = (y * width) + (2 * x0) + xo - resolution.X0;
            var source = v * bandWidth;
            for (var u = 0; u < bandWidth; u++)
            {
                output[target + (2 * u)] = band[source + u];
            }
        }
    }

    // Every row (HOR_SR) then every column (VER_SR) through the one-dimensional reconstruction.
    private static void Filter<T>(T[] samples, J2kResolution resolution, Action<T[], int, int> reconstruct)
        where T : struct
    {
        var width = resolution.X1 - resolution.X0;
        var height = resolution.Y1 - resolution.Y0;
        var line = new T[Math.Max(width, height) + (2 * Pad)];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(samples, y * width, line, Pad, width);
            Signal(line, resolution.X0, width, reconstruct);
            Array.Copy(line, Pad, samples, y * width, width);
        }

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                line[Pad + y] = samples[(y * width) + x];
            }

            Signal(line, resolution.Y0, height, reconstruct);
            for (var y = 0; y < height; y++)
            {
                samples[(y * width) + x] = line[Pad + y];
            }
        }
    }

    // 1D_SR (F.3.6): a single sample at an odd coordinate is halved; longer signals are extended then reconstructed.
    private static void Signal<T>(T[] line, int i0, int length, Action<T[], int, int> reconstruct)
        where T : struct
    {
        if (length == 1)
        {
            if ((i0 & 1) == 1)
            {
                line[Pad] = line[Pad] is int value ? (T)(object)(value / 2) : (T)(object)((float)(object)line[Pad] / 2);
            }

            return;
        }

        for (var k = 1; k <= Pad; k++)
        {
            line[Pad - k] = line[Pad + Reflect(-k, length)];
            line[Pad + length - 1 + k] = line[Pad + Reflect(length - 1 + k, length)];
        }

        // The first even coordinate's offset in the line, counted from its start (Pad - i0 parity).
        reconstruct(line, (i0 & 1) == 0 ? 0 : 1, length + (2 * Pad));
    }

    // Periodic symmetric extension (F.3.7) of an index outside 0..length-1.
    private static int Reflect(int index, int length)
    {
        var period = 2 * (length - 1);
        var i = ((index % period) + period) % period;
        return i < length ? i : period - i;
    }

    // F.3.8.1: X(2n) = Y(2n) - floor((Y(2n-1) + Y(2n+1) + 2) / 4), then X(2n+1) = Y(2n+1) + floor((X(2n) + X(2n+2)) / 2).
    // The line holds Pad samples of extension; even (low-pass) samples start at the given parity offset of Pad.
    private static void Reversible(int[] line, int odd, int count)
    {
        var first = (Pad + odd) % 2;
        for (var i = first + 2; i < count - 1; i += 2)
        {
            line[i] -= (line[i - 1] + line[i + 1] + 2) >> 2;
        }

        for (var i = first + 3; i < count - 2; i += 2)
        {
            line[i] += (line[i - 1] + line[i + 1]) >> 1;
        }
    }

    // F.3.8.2: the six lifting steps of the 9-7 filter in reverse.
    private static void Irreversible(float[] line, int odd, int count)
    {
        var even = (Pad + odd) % 2;
        var oddStart = 1 - even;
        Scale(line, even, count, K);
        Scale(line, oddStart, count, 1 / K);
        Lift(line, even + 2, count - 1, Delta);
        Lift(line, oddStart + 2, count - 1, Gamma);
        Lift(line, even + 2, count - 1, Beta);
        Lift(line, oddStart + 2, count - 1, Alpha);
    }

    private static void Scale(float[] line, int start, int count, float factor)
    {
        for (var i = start; i < count; i += 2)
        {
            line[i] *= factor;
        }
    }

    private static void Lift(float[] line, int start, int end, float factor)
    {
        for (var i = start; i < end; i += 2)
        {
            line[i] -= factor * (line[i - 1] + line[i + 1]);
        }
    }
}
