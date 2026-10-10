// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// Parses a formula as stored in a workbook (ECMA-376 part 1, §18.17: invariant numbers, comma separators) into a
/// tree. Precedence from tightest: range, negation, percent, power, multiplication and division, addition and
/// subtraction, concatenation, comparison; so <c>-2^2</c> is 4, as in Excel. References are read by
/// <see cref="FormulaReferenceReader"/>.
/// </summary>
internal sealed class FormulaParser
{
    private static readonly string[] Errors = ["#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A", "#NULL!", "#GETTING_DATA"];
    private static readonly string[] Comparisons = ["<=", ">=", "<>", "=", "<", ">"];
    private static readonly string[] Additions = ["+", "-"];
    private static readonly string[] Multiplications = ["*", "/"];
    private static readonly string[] Concatenations = ["&"];
    private static readonly string[] Powers = ["^"];
    private static readonly string[] Percents = ["%"];
    private static readonly string[] FunctionPrefixes = ["_XLFN._XLWS.", "_XLFN.", "_XLWS."];

    private readonly FormulaCursor _cursor;

    private FormulaParser(string text)
    {
        _cursor = new FormulaCursor(text);
    }

    /// <summary>The tree of <paramref name="formula"/> (with or without its leading <c>=</c>).</summary>
    /// <exception cref="FormatException">The formula is not well formed.</exception>
    public static FormulaNode Parse(string formula)
    {
        var parser = new FormulaParser(formula.StartsWith('=') ? formula[1..] : formula);
        var node = parser.Comparison();
        parser._cursor.SkipSpaces();
        return parser._cursor.AtEnd ? node : throw parser._cursor.Error("unexpected text");
    }

    private FormulaNode Comparison() => LeftAssociative(Comparisons, Concatenation);

    private FormulaNode Concatenation() => LeftAssociative(Concatenations, Additive);

    private FormulaNode Additive() => LeftAssociative(Additions, Multiplicative);

    private FormulaNode Multiplicative() => LeftAssociative(Multiplications, Power);

    private FormulaNode Power() => LeftAssociative(Powers, Unary);

    // operand (op operand)*, grouped from the left: 2^3^2 is (2^3)^2, as in Excel.
    private FormulaNode LeftAssociative(string[] operators, Func<FormulaNode> operand)
    {
        var left = operand();
        while (_cursor.Match(operators) is { } op)
        {
            left = new BinaryNode(op, left, operand());
        }

        return left;
    }

    private FormulaNode Unary()
    {
        if (_cursor.Match(Additions) is { } op)
        {
            var operand = Unary();
            return op == "-" ? new UnaryNode('-', operand) : operand;
        }

        var node = Primary();
        while (_cursor.Match(Percents) is not null)
        {
            node = new PercentNode(node);
        }

        return node;
    }

    private FormulaNode Primary()
    {
        _cursor.SkipSpaces();
        if (_cursor.AtEnd)
        {
            throw _cursor.Error("missing operand");
        }

        var c = _cursor.Current;
        return c switch
        {
            '(' => Parenthesized(),
            '"' => new ConstantNode(FormulaValue.Of(ReadString())),
            '#' => new ConstantNode(FormulaValue.Fail(ReadError())),
            '{' => ReadArray(),
            _ when char.IsAsciiDigit(c) || c == '.' => NumberOrRows(),
            _ => NameOrReference(),
        };
    }

    private FormulaNode Parenthesized()
    {
        _cursor.Position++;
        var inner = Comparison();
        _cursor.Expect(')');
        return inner;
    }

    // A number, or a range of whole rows such as 1:3.
    private FormulaNode NumberOrRows()
    {
        if (FormulaReferenceReader.TryRead(_cursor, null) is { } rows)
        {
            return rows;
        }

        var start = _cursor.Position;
        _cursor.SkipWhile(c => char.IsAsciiDigit(c) || c == '.');
        SkipExponent();
        return double.TryParse(_cursor.Text.AsSpan(start, _cursor.Position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? new ConstantNode(FormulaValue.Of(number))
            : throw _cursor.Error("bad number");
    }

    // E2, e+2, E-2 after the digits of a number.
    private void SkipExponent()
    {
        if (_cursor.Current is 'e' or 'E' && (char.IsAsciiDigit(_cursor.Next) || _cursor.Next is '+' or '-'))
        {
            _cursor.Position += 2;
            _cursor.SkipWhile(char.IsAsciiDigit);
        }
    }

    private FormulaNode NameOrReference()
    {
        var start = _cursor.Position;
        if (_cursor.Is('\''))
        {
            return SheetReference(FormulaReferenceReader.ReadQuotedSheet(_cursor), start);
        }

        if (FormulaReferenceReader.TryRead(_cursor, null) is { } reference)
        {
            return reference;
        }

        var name = ReadName();
        if (_cursor.Skip('!') || ThreeDimensionalSheets(ref name))
        {
            return SheetReference(name, start);
        }

        _cursor.SkipSpaces();
        if (_cursor.Skip('('))
        {
            return new CallNode(FunctionName(name), Arguments());
        }

        return _cursor.Is('[') ? StructuredReference(start) : NamedConstant(name);
    }

    private string ReadName()
    {
        var start = _cursor.Position;
        if (_cursor.SkipWhile(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '\\') == 0)
        {
            throw _cursor.Error("unexpected character");
        }

        return _cursor.Since(start);
    }

    // First:Last! names the sheets from First to Last (a 3-D reference); the cursor stays put otherwise.
    private bool ThreeDimensionalSheets(ref string name)
    {
        var colon = _cursor.Position;
        if (_cursor.Skip(':') && _cursor.SkipWhile(c => char.IsLetterOrDigit(c) || c is '_' or '.') > 0 && _cursor.Skip('!'))
        {
            name = _cursor.Text[(colon - name.Length)..(_cursor.Position - 1)];
            return true;
        }

        _cursor.Position = colon;
        return false;
    }

    // A sheet name cannot hold ':', so First:Last is a 3-D reference over several sheets: not computed.
    private FormulaNode SheetReference(string sheet, int start)
    {
        var reference = FormulaReferenceReader.TryRead(_cursor, sheet) ?? throw _cursor.Error("reference expected after the sheet name");
        return sheet.Contains(':', StringComparison.Ordinal) ? new UnsupportedNode(_cursor.Since(start)) : reference;
    }

    // A structured reference to a table (Table1[Column]): not computed.
    private UnsupportedNode StructuredReference(int start)
    {
        var close = _cursor.Text.IndexOf(']', _cursor.Position);
        _cursor.Position = close < 0 ? _cursor.Text.Length : close + 1;
        return new UnsupportedNode(_cursor.Since(start));
    }

    // TRUE and FALSE; any other name is a defined name, not computed.
    private static FormulaNode NamedConstant(string name) => name.ToUpperInvariant() switch
    {
        "TRUE" => new ConstantNode(FormulaValue.True),
        "FALSE" => new ConstantNode(FormulaValue.False),
        _ => new UnsupportedNode(name),
    };

    // Newer functions are stored with a prefix (_xlfn.CONCAT, _xlfn._xlws.FILTER).
    private static string FunctionName(string name)
    {
        var upper = name.ToUpperInvariant();
        var prefix = FunctionPrefixes.FirstOrDefault(p => upper.StartsWith(p, StringComparison.Ordinal));
        return prefix is null ? upper : upper[prefix.Length..];
    }

    private List<FormulaNode> Arguments()
    {
        var arguments = new List<FormulaNode>();
        _cursor.SkipSpaces();
        if (_cursor.Skip(')'))
        {
            return arguments;
        }

        do
        {
            _cursor.SkipSpaces();
            // An omitted argument (IF(A1,,2)) is a blank.
            arguments.Add(_cursor.Current is ',' or ')' ? new ConstantNode(FormulaValue.Blank) : Comparison());
            _cursor.SkipSpaces();
        }
        while (_cursor.Skip(','));

        _cursor.Expect(')');
        return arguments;
    }

    private string ReadString()
    {
        _cursor.Position++;
        var builder = new System.Text.StringBuilder();
        while (!_cursor.AtEnd)
        {
            var c = _cursor.Current;
            _cursor.Position++;
            if (c != '"')
            {
                builder.Append(c);
            }
            else if (_cursor.Skip('"'))
            {
                builder.Append('"');
            }
            else
            {
                return builder.ToString();
            }
        }

        throw _cursor.Error("unterminated text");
    }

    private string ReadError()
    {
        var error = Errors.FirstOrDefault(e => string.Compare(_cursor.Text, _cursor.Position, e, 0, e.Length, StringComparison.OrdinalIgnoreCase) == 0)
            ?? throw _cursor.Error("unknown error value");
        _cursor.Position += error.Length;
        return error;
    }

    // An array constant: {1,2;3,4} holds rows separated by semicolons.
    private ConstantNode ReadArray()
    {
        _cursor.Position++;
        var rows = new List<List<FormulaValue>> { new() };
        while (true)
        {
            rows[^1].Add(ArrayElement());
            _cursor.SkipSpaces();
            var separator = _cursor.Current;
            _cursor.Skip(separator);
            if (separator == '}')
            {
                break;
            }

            if (separator == ';')
            {
                rows.Add([]);
            }
            else if (separator != ',')
            {
                throw _cursor.Error("bad array");
            }
        }

        return new ConstantNode(FormulaValue.Of(ToGrid(rows)));
    }

    private FormulaValue ArrayElement() => Unary() switch
    {
        ConstantNode { Value: var constant } => constant,
        UnaryNode { Operator: '-', Operand: ConstantNode { Value.Kind: ValueKind.Number } negated } => FormulaValue.Of(-negated.Value.Number),
        _ => throw _cursor.Error("an array holds constants only"),
    };

    private FormulaValue[,] ToGrid(List<List<FormulaValue>> rows)
    {
        var width = rows[0].Count;
        if (rows.Any(r => r.Count != width))
        {
            throw _cursor.Error("array rows of different lengths");
        }

        var array = new FormulaValue[rows.Count, width];
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < width; c++)
            {
                array[r, c] = rows[r][c];
            }
        }

        return array;
    }
}
