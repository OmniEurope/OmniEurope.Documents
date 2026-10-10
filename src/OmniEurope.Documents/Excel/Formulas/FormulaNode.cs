// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>A rectangle of cells, on a named sheet or (null) on the sheet of the formula.</summary>
internal readonly record struct FormulaArea(string? Sheet, int Top, int Left, int Bottom, int Right)
{
    public int Rows => Bottom - Top + 1;

    public int Columns => Right - Left + 1;
}

/// <summary>A node of a parsed formula.</summary>
internal abstract record FormulaNode;

internal sealed record ConstantNode(FormulaValue Value) : FormulaNode;

internal sealed record ReferenceNode(FormulaArea Area) : FormulaNode;

internal sealed record UnaryNode(char Operator, FormulaNode Operand) : FormulaNode;

internal sealed record PercentNode(FormulaNode Operand) : FormulaNode;

internal sealed record BinaryNode(string Operator, FormulaNode Left, FormulaNode Right) : FormulaNode;

internal sealed record CallNode(string Name, IReadOnlyList<FormulaNode> Arguments) : FormulaNode;

/// <summary>Something the engine does not compute: a defined name, a structured or external reference.</summary>
internal sealed record UnsupportedNode(string Text) : FormulaNode;
