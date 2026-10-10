// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// A transparency group being drawn (ISO 32000-1 §11.4): colour and alpha per pixel over the group's bounds. An
/// isolated group starts transparent; a non-isolated one starts from its backdrop and also keeps the alpha of its
/// own objects, so that the backdrop can be taken out again when the group is composited (§11.4.8). In a knockout
/// group each object is composited with the initial backdrop instead of the objects before it, weighted by its shape
/// (§11.4.6). Colours are stored from 0 to 1, not premultiplied.
/// </summary>
internal sealed class GroupCanvas : Canvas
{
    private readonly PixelBounds _bounds;
    private readonly float[] _color;
    private readonly float[] _alpha;
    private readonly float[]? _groupAlpha;
    private readonly float[]? _initialColor;
    private readonly float[]? _initialAlpha;
    private readonly bool _knockout;

    /// <summary>A group over <paramref name="bounds"/>; <paramref name="backdrop"/> is the parent of a non-isolated group, null for an isolated one.</summary>
    public GroupCanvas(int width, int height, PixelBounds bounds, Canvas? backdrop, bool knockout)
        : base(width, height)
    {
        _bounds = bounds;
        _knockout = knockout;
        var count = bounds.Width * bounds.Height;
        _color = new float[count * 3];
        _alpha = new float[count];
        if (backdrop is not null)
        {
            _groupAlpha = new float[count];
            CopyBackdrop(backdrop);
        }

        if (backdrop is not null || knockout)
        {
            _initialColor = (float[])_color.Clone();
            _initialAlpha = (float[])_alpha.Clone();
        }
    }

    public override PixelBounds Bounds => _bounds;

    /// <summary>Fills every pixel with one colour (0 to 255) and alpha: the backdrop of a luminosity soft mask.</summary>
    public void Clear(double r, double g, double b, double a)
    {
        for (var i = 0; i < _alpha.Length; i++)
        {
            (_color[i * 3], _color[(i * 3) + 1], _color[(i * 3) + 2], _alpha[i]) = ((float)(r / 255), (float)(g / 255), (float)(b / 255), (float)a);
        }
    }

    public override (double R, double G, double B, double A) Read(int x, int y)
    {
        if (!_bounds.Contains(x, y))
        {
            return (0, 0, 0, 0);
        }

        var i = Index(x, y);
        return (_color[i * 3] * 255.0, _color[(i * 3) + 1] * 255.0, _color[(i * 3) + 2] * 255.0, _alpha[i]);
    }

    public override void Composite(int x, int y, double r, double g, double b, double shape, double opacity, Compositing compositing)
    {
        if (!_bounds.Contains(x, y))
        {
            return;
        }

        if (compositing.Clip is not null)
        {
            shape *= compositing.Clip.Alpha[(y * Width) + x] / 255.0;
        }

        if (compositing.Mask is not null)
        {
            opacity *= compositing.Mask.Value(x, y);
        }

        if (compositing.AlphaIsShape)
        {
            (shape, opacity) = (shape * opacity, 1);
        }

        var source = (r / 255, g / 255, b / 255);
        if (_knockout)
        {
            KnockOut(Index(x, y), source, Math.Clamp(shape, 0, 1), Math.Clamp(opacity, 0, 1), compositing.Mode);
        }
        else
        {
            Over(Index(x, y), source, Math.Clamp(shape * opacity, 0, 1), compositing.Mode);
        }
    }

    /// <summary>Composites the group, as one object, on its parent (§11.4.8): its colour with the backdrop taken out, its alpha as shape.</summary>
    public void CompositeOnto(Canvas parent, double opacity, Compositing compositing)
    {
        for (var y = _bounds.Top; y < _bounds.Bottom; y++)
        {
            for (var x = _bounds.Left; x < _bounds.Right; x++)
            {
                var (c, alpha) = Result(Index(x, y));
                if (alpha > 0)
                {
                    parent.Composite(x, y, c.R * 255, c.G * 255, c.B * 255, alpha, opacity, compositing);
                }
            }
        }
    }

    private int Index(int x, int y) => ((y - _bounds.Top) * _bounds.Width) + x - _bounds.Left;

    private static (double R, double G, double B) ColorAt(float[] colors, int i) => (colors[i * 3], colors[(i * 3) + 1], colors[(i * 3) + 2]);

    private void Store(int i, (double R, double G, double B) c, double alpha)
    {
        (_color[i * 3], _color[(i * 3) + 1], _color[(i * 3) + 2]) = ((float)c.R, (float)c.G, (float)c.B);
        _alpha[i] = (float)alpha;
    }

    // Cr = [(1 - as) ab Cb + as ((1 - ab) Cs + ab B(Cb, Cs))] / ar, with ar = ab + as - ab as (§11.3.6).
    private void Over(int i, (double R, double G, double B) source, double alphaSource, BlendMode mode)
    {
        if (alphaSource <= 0)
        {
            return;
        }

        var (color, alpha) = Combine(ColorAt(_color, i), _alpha[i], source, alphaSource, mode);
        Store(i, color, alpha);
        if (_groupAlpha is not null)
        {
            _groupAlpha[i] += (float)(alphaSource * (1 - _groupAlpha[i]));
        }
    }

    // The object composited with the initial backdrop, then mixed with the previous result by its shape (§11.4.6).
    private void KnockOut(int i, (double R, double G, double B) source, double shape, double opacity, BlendMode mode)
    {
        if (shape <= 0)
        {
            return;
        }

        var (initial, initialAlpha) = (ColorAt(_initialColor!, i), (double)_initialAlpha![i]);
        var (knocked, knockedAlpha) = Combine(initial, initialAlpha, source, opacity, mode);
        var (previous, previousAlpha) = (ColorAt(_color, i), (double)_alpha[i]);
        var alpha = ((1 - shape) * previousAlpha) + (shape * knockedAlpha);
        var color = alpha <= 0 ? previous : (
            (((1 - shape) * previousAlpha * previous.R) + (shape * knockedAlpha * knocked.R)) / alpha,
            (((1 - shape) * previousAlpha * previous.G) + (shape * knockedAlpha * knocked.G)) / alpha,
            (((1 - shape) * previousAlpha * previous.B) + (shape * knockedAlpha * knocked.B)) / alpha);
        Store(i, color, alpha);
        if (_groupAlpha is not null)
        {
            _groupAlpha[i] = (float)(((1 - shape) * _groupAlpha[i]) + (shape * opacity));
        }
    }

    private static ((double R, double G, double B) Color, double Alpha) Combine((double R, double G, double B) backdrop, double backdropAlpha, (double R, double G, double B) source, double alphaSource, BlendMode mode)
    {
        var alpha = backdropAlpha + alphaSource - (backdropAlpha * alphaSource);
        if (alpha <= 0)
        {
            return (backdrop, 0);
        }

        var blended = mode == BlendMode.Normal || backdropAlpha <= 0 ? source : Blending.Apply(mode, backdrop, source);
        double Channel(double cb, double cs, double b) => (((1 - alphaSource) * backdropAlpha * cb) + (alphaSource * (((1 - backdropAlpha) * cs) + (backdropAlpha * b)))) / alpha;
        return ((Channel(backdrop.R, source.R, blended.R), Channel(backdrop.G, source.G, blended.G), Channel(backdrop.B, source.B, blended.B)), alpha);
    }

    // C = Cn + (Cn - C0) (a0 / agn - a0) and a = agn for a non-isolated group; the stored values for an isolated one.
    private ((double R, double G, double B) Color, double Alpha) Result(int i)
    {
        var color = ColorAt(_color, i);
        if (_groupAlpha is null)
        {
            return (color, _alpha[i]);
        }

        var groupAlpha = (double)_groupAlpha[i];
        if (groupAlpha <= 0)
        {
            return (color, 0);
        }

        var initial = ColorAt(_initialColor!, i);
        var initialAlpha = (double)_initialAlpha![i];
        var factor = (initialAlpha / groupAlpha) - initialAlpha;
        double Channel(double cn, double c0) => Math.Clamp(cn + ((cn - c0) * factor), 0, 1);
        return ((Channel(color.R, initial.R), Channel(color.G, initial.G), Channel(color.B, initial.B)), groupAlpha);
    }

    private void CopyBackdrop(Canvas backdrop)
    {
        for (var y = _bounds.Top; y < _bounds.Bottom; y++)
        {
            for (var x = _bounds.Left; x < _bounds.Right; x++)
            {
                var (r, g, b, a) = backdrop.Read(x, y);
                Store(Index(x, y), (r / 255, g / 255, b / 255), a);
            }
        }
    }
}
