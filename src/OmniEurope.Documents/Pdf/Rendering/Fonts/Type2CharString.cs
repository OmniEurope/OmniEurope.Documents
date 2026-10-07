// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Rendering.Fonts;

/// <summary>
/// Interprets Type 2 charstrings (CFF glyph programs) into outline commands in font units: moves, lines,
/// curves (including the flex forms), subroutine calls with their biases, and hint operators skipped.
/// Arithmetic and storage operators are not supported (rare in fonts embedded in PDF).
/// </summary>
internal sealed class Type2CharString(byte[] data, List<(int Start, int End)> globalSubrs, List<(int Start, int End)> localSubrs)
{
    private const int MaxDepth = 10;
    private readonly List<GlyphCommand> _commands = [];
    private readonly List<double> _stack = [];
    private double _x;
    private double _y;
    private int _stems;
    private bool _widthDone;
    private bool _open;
    private bool _ended;

    public List<GlyphCommand> Run(int start, int end, double nominalWidth)
    {
        _ = nominalWidth;
        Execute(start, end, 0);
        Close();
        return _commands;
    }

    private void Execute(int start, int end, int depth)
    {
        var position = start;
        while (position < end && !_ended && depth <= MaxDepth)
        {
            var b0 = data[position++];
            if (b0 >= 32 || b0 == 28)
            {
                _stack.Add(Number(b0, ref position));
                continue;
            }

            if (b0 == 12)
            {
                Escaped(data[position++]);
                continue;
            }

            position = Operator(b0, position, depth);
        }
    }

    private double Number(byte b0, ref int position) => b0 switch
    {
        28 => (short)((data[position++] << 8) | data[position++]),
        <= 246 => b0 - 139,
        <= 250 => ((b0 - 247) * 256) + data[position++] + 108,
        <= 254 => -((b0 - 251) * 256) - data[position++] - 108,
        _ => ((data[position++] << 24) | (data[position++] << 16) | (data[position++] << 8) | data[position++]) / 65536.0,
    };

    private int Operator(byte op, int position, int depth)
    {
        switch (op)
        {
            case 1 or 3 or 18 or 23:
                Stems();
                break;
            case 19 or 20:
                Stems();
                position += (_stems + 7) / 8;
                break;
            case 10 or 29:
                Call(op == 10 ? localSubrs : globalSubrs, depth);
                break;
            case 11:
                return int.MaxValue;
            case 14:
                Width(0);
                _ended = true;
                break;
            default:
                Drawing(op);
                break;
        }

        return position;
    }

    private void Drawing(byte op)
    {
        switch (op)
        {
            case 21:
                Width(2);
                MoveTo(Arg(0), Arg(1));
                break;
            case 22:
                Width(1);
                MoveTo(Arg(0), 0);
                break;
            case 4:
                Width(1);
                MoveTo(0, Arg(0));
                break;
            case 5:
                for (var i = 0; i + 1 < _stack.Count; i += 2)
                {
                    LineTo(_stack[i], _stack[i + 1]);
                }

                break;
            case 6 or 7:
                AlternatingLines(op == 6);
                break;
            case 8:
                Curves(0, _stack.Count);
                break;
            case 24:
                Curves(0, _stack.Count - 2);
                LineTo(_stack[^2], _stack[^1]);
                break;
            case 25:
                for (var i = 0; i + 1 < _stack.Count - 6; i += 2)
                {
                    LineTo(_stack[i], _stack[i + 1]);
                }

                Curves(_stack.Count - 6, _stack.Count);
                break;
            case 26 or 27:
                ParallelCurves(vertical: op == 26);
                break;
            case 30 or 31:
                AlternatingCurves(startVertical: op == 30);
                break;
        }

        _stack.Clear();
    }

    private void Escaped(byte op)
    {
        var s = _stack;
        switch (op)
        {
            case 35 when s.Count >= 12:
                Curve(s[0], s[1], s[2], s[3], s[4], s[5]);
                Curve(s[6], s[7], s[8], s[9], s[10], s[11]);
                break;
            case 34 when s.Count >= 7:
                Curve(s[0], 0, s[1], s[2], s[3], 0);
                Curve(s[4], 0, s[5], -s[2], s[6], 0);
                break;
            case 36 when s.Count >= 9:
                Curve(s[0], s[1], s[2], s[3], s[4], 0);
                Curve(s[5], 0, s[6], s[7], s[8], -(s[1] + s[3] + s[7]));
                break;
            case 37 when s.Count >= 11:
                Flex1(s);
                break;
        }

        _stack.Clear();
    }

    // flex1: the last argument is the displacement along the dominant axis.
    private void Flex1(List<double> s)
    {
        var dx = s[0] + s[2] + s[4] + s[6] + s[8];
        var dy = s[1] + s[3] + s[5] + s[7] + s[9];
        Curve(s[0], s[1], s[2], s[3], s[4], s[5]);
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            Curve(s[6], s[7], s[8], s[9], s[10], -dy);
        }
        else
        {
            Curve(s[6], s[7], s[8], s[9], -dx, s[10]);
        }
    }

    private void Stems()
    {
        Width(_stack.Count % 2);
        _stems += _stack.Count / 2;
        _stack.Clear();
    }

    // The first stack value of the first stack-clearing operator is the advance width when the count is odd.
    private void Width(int expected)
    {
        if (!_widthDone && _stack.Count > expected)
        {
            _stack.RemoveAt(0);
        }

        _widthDone = true;
    }

    private void Call(List<(int Start, int End)> subrs, int depth)
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var bias = subrs.Count < 1240 ? 107 : subrs.Count < 33900 ? 1131 : 32768;
        var index = (int)_stack[^1] + bias;
        _stack.RemoveAt(_stack.Count - 1);
        if (index >= 0 && index < subrs.Count && depth < MaxDepth)
        {
            Execute(subrs[index].Start, subrs[index].End, depth + 1);
        }
    }

    private double Arg(int index) => index < _stack.Count ? _stack[index] : 0;

    private void AlternatingLines(bool horizontalFirst)
    {
        var horizontal = horizontalFirst;
        foreach (var value in _stack)
        {
            LineTo(horizontal ? value : 0, horizontal ? 0 : value);
            horizontal = !horizontal;
        }
    }

    private void Curves(int from, int to)
    {
        for (var i = from; i + 6 <= to; i += 6)
        {
            Curve(_stack[i], _stack[i + 1], _stack[i + 2], _stack[i + 3], _stack[i + 4], _stack[i + 5]);
        }
    }

    // hhcurveto / vvcurveto: curves along one axis, an optional first offset across it.
    private void ParallelCurves(bool vertical)
    {
        var i = 0;
        var cross = 0.0;
        if (_stack.Count % 4 == 1)
        {
            cross = _stack[0];
            i = 1;
        }

        for (; i + 3 < _stack.Count; i += 4)
        {
            if (vertical)
            {
                Curve(cross, _stack[i], _stack[i + 1], _stack[i + 2], 0, _stack[i + 3]);
            }
            else
            {
                Curve(_stack[i], cross, _stack[i + 1], _stack[i + 2], _stack[i + 3], 0);
            }

            cross = 0;
        }
    }

    // hvcurveto / vhcurveto: curves alternating between horizontal and vertical tangents.
    private void AlternatingCurves(bool startVertical)
    {
        var vertical = startVertical;
        for (var i = 0; i + 3 < _stack.Count; i += 4)
        {
            var last = i + 4 == _stack.Count - 1 ? _stack[^1] : 0;
            if (vertical)
            {
                Curve(0, _stack[i], _stack[i + 1], _stack[i + 2], _stack[i + 3], last);
            }
            else
            {
                Curve(_stack[i], 0, _stack[i + 1], _stack[i + 2], last, _stack[i + 3]);
            }

            vertical = !vertical;
        }
    }

    private void MoveTo(double dx, double dy)
    {
        Close();
        _x += dx;
        _y += dy;
        _commands.Add(new GlyphCommand('M', _x, _y));
        _open = true;
    }

    private void LineTo(double dx, double dy)
    {
        _x += dx;
        _y += dy;
        _commands.Add(new GlyphCommand('L', _x, _y));
    }

    private void Curve(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
    {
        var (x1, y1) = (_x + dx1, _y + dy1);
        var (x2, y2) = (x1 + dx2, y1 + dy2);
        (_x, _y) = (x2 + dx3, y2 + dy3);
        _commands.Add(new GlyphCommand('C', x1, y1, x2, y2, _x, _y));
    }

    private void Close()
    {
        if (_open)
        {
            _commands.Add(new GlyphCommand('Z'));
            _open = false;
        }
    }
}
