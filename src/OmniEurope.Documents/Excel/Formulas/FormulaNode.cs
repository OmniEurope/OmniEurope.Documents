// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>A rectangle of cells, on a named sheet or (null) on the sheet of the formula.</summary>
internal readonly record struct FormulaArea(string? Sheet, int Top, int Left, int Bottom, int Right)
{
    public bool WholeColumns { get; init; }

    public bool WholeRows { get; init; }

    public int Rows => Bottom - Top + 1;

    public int Columns => Right - Left + 1;
}

/// <summary>
/// A node of a parsed formula. <paramref name="Depth"/> is the number of levels the evaluator nests below the
/// node (0 for a leaf); the left operand of an operator is evaluated in a loop and adds none.
/// </summary>
internal abstract record FormulaNode(int Depth);

internal sealed record ConstantNode(FormulaValue Value) : FormulaNode(0);

internal sealed record ReferenceNode(FormulaArea Area) : FormulaNode(0);

internal sealed record UnaryNode(char Operator, FormulaNode Operand) : FormulaNode(Operand.Depth + 1);

internal sealed record PercentNode(FormulaNode Operand) : FormulaNode(Operand.Depth + 1);

internal sealed record BinaryNode(string Operator, FormulaNode Left, FormulaNode Right) : FormulaNode(Math.Max(Left.Depth, Right.Depth + 1));

internal sealed record CallNode(string Name, IReadOnlyList<FormulaNode> Arguments) : FormulaNode(DepthOf(Arguments))
{
    private static int DepthOf(IReadOnlyList<FormulaNode> arguments) => arguments.Count == 0 ? 1 : arguments.Max(a => a.Depth) + 1;
}

/// <summary>Something the engine does not compute: a defined name, a structured or external reference.</summary>
internal sealed record UnsupportedNode(string Text) : FormulaNode(0);

internal static class FormulaTree
{
    /// <summary>
    /// The node and all its descendants, each node before its operands and operands from left to right. Uses a
    /// stack of its own, so a tree of any depth is walked safely.
    /// </summary>
    public static IEnumerable<FormulaNode> PreOrder(FormulaNode root)
    {
        var pending = new Stack<FormulaNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            switch (node)
            {
                case UnaryNode unary:
                    pending.Push(unary.Operand);
                    break;
                case PercentNode percent:
                    pending.Push(percent.Operand);
                    break;
                case BinaryNode binary:
                    pending.Push(binary.Right);
                    pending.Push(binary.Left);
                    break;
                case CallNode call:
                    for (var i = call.Arguments.Count - 1; i >= 0; i--)
                    {
                        pending.Push(call.Arguments[i]);
                    }

                    break;
            }
        }
    }
}
