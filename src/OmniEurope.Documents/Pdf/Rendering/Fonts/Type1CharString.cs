// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering.Fonts;

/// <summary>
/// Interprets Type 1 charstrings into outline commands in font units: side bearing, moves, lines, curves,
/// subroutines, flex (other subroutines 0 to 2), hint replacement (3), and accented characters (seac) built
/// from two glyphs of the standard encoding.
/// </summary>
internal sealed class Type1CharString(Type1Font font)
{
    private const int MaxDepth = 10;
    private readonly List<GlyphCommand> _commands = [];
    private readonly List<double> _stack = [];
    private readonly Stack<double> _results = new();
    private List<(double X, double Y)>? _flex;
    private double _x;
    private double _y;
    private double _offsetX;
    private double _offsetY;
    private bool _open;
    private bool _ended;

    public List<GlyphCommand> Run(byte[] program)
    {
        Execute(program, 0);
        Close();
        return _commands;
    }

    private void Execute(byte[] program, int depth)
    {
        var position = 0;
        while (position < program.Length && !_ended && depth <= MaxDepth)
        {
            var b0 = program[position++];
            if (b0 >= 32)
            {
                _stack.Add(Number(program, b0, ref position));
            }
            else if (b0 == 12 && position < program.Length)
            {
                Escaped(program[position++], depth);
            }
            else if (b0 == 11)
            {
                return;
            }
            else
            {
                Operator(b0, depth);
            }
        }
    }

    private static double Number(byte[] program, byte b0, ref int position) => b0 switch
    {
        <= 246 => b0 - 139,
        <= 250 => ((b0 - 247) * 256) + program[position++] + 108,
        <= 254 => -((b0 - 251) * 256) - program[position++] - 108,
        _ => (program[position++] << 24) | (program[position++] << 16) | (program[position++] << 8) | program[position++],
    };

    private void Operator(byte op, int depth)
    {
        switch (op)
        {
            case 13:
                (_x, _y) = (Arg(0), 0);
                break;
            case 21:
                Move(Arg(0), Arg(1));
                break;
            case 22:
                Move(Arg(0), 0);
                break;
            case 4:
                Move(0, Arg(0));
                break;
            case 5:
                Line(Arg(0), Arg(1));
                break;
            case 6:
                Line(Arg(0), 0);
                break;
            case 7:
                Line(0, Arg(0));
                break;
            case 8:
                Curve(Arg(0), Arg(1), Arg(2), Arg(3), Arg(4), Arg(5));
                break;
            case 30:
                Curve(0, Arg(0), Arg(1), Arg(2), Arg(3), 0);
                break;
            case 31:
                Curve(Arg(0), 0, Arg(1), Arg(2), 0, Arg(3));
                break;
            case 9:
                Close();
                break;
            case 10:
                CallSubr(depth);
                return;
            case 14:
                _ended = true;
                break;
        }

        _stack.Clear();
    }

    private void Escaped(byte op, int depth)
    {
        switch (op)
        {
            case 7:
                (_x, _y) = (Arg(0), Arg(1));
                break;
            case 6:
                Accented(depth);
                break;
            case 12 when _stack.Count >= 2:
                var quotient = _stack[^2] / (_stack[^1] == 0 ? 1 : _stack[^1]);
                _stack.RemoveRange(_stack.Count - 2, 2);
                _stack.Add(quotient);
                return;
            case 16:
                OtherSubr();
                return;
            case 17:
                _stack.Add(_results.Count > 0 ? _results.Pop() : 0);
                return;
            case 33:
                (_x, _y) = (Arg(0) + _offsetX, Arg(1) + _offsetY);
                break;
        }

        _stack.Clear();
    }

    private void CallSubr(int depth)
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var index = (int)_stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        if (index >= 0 && index < font.Subrs.Count && depth < MaxDepth)
        {
            Execute(font.Subrs[index], depth + 1);
        }
    }

    // Other subroutines: 0 ends a flex, 1 starts one, 2 adds a point, 3 replaces hints; results go to "pop".
    private void OtherSubr()
    {
        if (_stack.Count < 2)
        {
            _stack.Clear();
            return;
        }

        var number = (int)_stack[^1];
        var count = Math.Min((int)_stack[^2], _stack.Count - 2);
        var args = _stack.GetRange(_stack.Count - 2 - count, count);
        _stack.RemoveRange(_stack.Count - 2 - count, count + 2);
        switch (number)
        {
            case 1:
                _flex = [];
                break;
            case 0 when _flex is { Count: >= 7 } points:
                _commands.Add(new GlyphCommand('C', points[1].X, points[1].Y, points[2].X, points[2].Y, points[3].X, points[3].Y));
                _commands.Add(new GlyphCommand('C', points[4].X, points[4].Y, points[5].X, points[5].Y, points[6].X, points[6].Y));
                (_x, _y) = (points[6].X - _offsetX, points[6].Y - _offsetY);
                _flex = null;
                _results.Push(_y);
                _results.Push(_x);
                return;
            case 3:
                _results.Push(3);
                return;
        }

        for (var i = 0; i < args.Count; i++)
        {
            _results.Push(args[i]);
        }
    }

    // seac: a base glyph and an accent moved by (adx - asb, ady), both found through the standard encoding.
    private void Accented(int depth)
    {
        if (_stack.Count < 5 || depth >= MaxDepth)
        {
            return;
        }

        var (asb, adx, ady, baseCode, accentCode) = (_stack[^5], _stack[^4], _stack[^3], (int)_stack[^2], (int)_stack[^1]);
        _stack.Clear();
        var baseName = StandardName(baseCode);
        var accentName = StandardName(accentCode);
        Close();
        if (baseName is not null && font.CharStrings.TryGetValue(baseName, out var baseProgram))
        {
            Part(baseProgram, 0, 0, depth);
        }

        if (accentName is not null && font.CharStrings.TryGetValue(accentName, out var accentProgram))
        {
            Part(accentProgram, adx - asb, ady, depth);
        }

        _ended = true;
    }

    private void Part(byte[] program, double dx, double dy, int depth)
    {
        var part = new Type1CharString(font) { _offsetX = dx, _offsetY = dy };
        part.Execute(program, depth + 1);
        part.Close();
        _commands.AddRange(part._commands);
    }

    private string? StandardName(int code)
    {
        var text = code is >= 0 and < 256 ? StandardEncodings.Named("StandardEncoding")[code] : null;
        return text is null ? null : font.CharStrings.Keys.FirstOrDefault(name => GlyphNames.ToUnicode(name) == text);
    }

    private double Arg(int index) => index < _stack.Count ? _stack[index] : 0;

    private void Move(double dx, double dy)
    {
        _x += dx;
        _y += dy;
        if (_flex is not null)
        {
            _flex.Add((_x + _offsetX, _y + _offsetY));
            return;
        }

        Close();
        _commands.Add(new GlyphCommand('M', _x + _offsetX, _y + _offsetY));
        _open = true;
    }

    private void Line(double dx, double dy)
    {
        _x += dx;
        _y += dy;
        _commands.Add(new GlyphCommand('L', _x + _offsetX, _y + _offsetY));
    }

    private void Curve(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
    {
        var (x1, y1) = (_x + dx1, _y + dy1);
        var (x2, y2) = (x1 + dx2, y1 + dy2);
        (_x, _y) = (x2 + dx3, y2 + dy3);
        _commands.Add(new GlyphCommand('C', x1 + _offsetX, y1 + _offsetY, x2 + _offsetX, y2 + _offsetY, _x + _offsetX, _y + _offsetY));
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
