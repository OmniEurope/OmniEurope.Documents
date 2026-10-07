// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Diff;

/// <summary>
/// Myers' O(ND) difference algorithm in its linear-space form: the middle snake of the optimal edit path
/// splits the problem in two, recursively (here with an explicit stack, so long inputs cannot overflow the
/// call stack). Works on integer symbols; equal symbols mean equal pieces.
/// </summary>
internal sealed class MyersDiff
{
    private readonly int[] _a;
    private readonly int[] _b;
    private readonly int[] _forward;
    private readonly int[] _backward;

    private MyersDiff(int[] a, int[] b)
    {
        _a = a;
        _b = b;
        var size = (2 * ((a.Length + b.Length + 1) / 2)) + 3;
        _forward = new int[size];
        _backward = new int[size];
        ModifiedA = new bool[a.Length];
        ModifiedB = new bool[b.Length];
    }

    public bool[] ModifiedA { get; }

    public bool[] ModifiedB { get; }

    /// <summary>Marks the symbols of <paramref name="a"/> deleted and of <paramref name="b"/> inserted by a
    /// shortest edit script.</summary>
    public static MyersDiff Run(int[] a, int[] b)
    {
        var diff = new MyersDiff(a, b);
        diff.Solve();
        return diff;
    }

    private void Solve()
    {
        var pending = new Stack<(int LowA, int HighA, int LowB, int HighB)>();
        pending.Push((0, _a.Length, 0, _b.Length));
        while (pending.Count > 0)
        {
            var (lowA, highA, lowB, highB) = pending.Pop();
            while (lowA < highA && lowB < highB && _a[lowA] == _b[lowB])
            {
                lowA++;
                lowB++;
            }

            while (lowA < highA && lowB < highB && _a[highA - 1] == _b[highB - 1])
            {
                highA--;
                highB--;
            }

            if (lowA == highA || lowB == highB)
            {
                Array.Fill(ModifiedA, true, lowA, highA - lowA);
                Array.Fill(ModifiedB, true, lowB, highB - lowB);
                continue;
            }

            var (x, y) = MiddleSnake(lowA, highA, lowB, highB);
            if ((x == lowA && y == lowB) || (x == highA && y == highB))
            {
                // Never expected after trimming; falling back to a full replacement keeps the result correct.
                Array.Fill(ModifiedA, true, lowA, highA - lowA);
                Array.Fill(ModifiedB, true, lowB, highB - lowB);
                continue;
            }

            pending.Push((x, highA, y, highB));
            pending.Push((lowA, x, lowB, y));
        }
    }

    // The middle snake of the box [lowA, highA) x [lowB, highB): forward and backward searches meet on it.
    private (int X, int Y) MiddleSnake(int lowA, int highA, int lowB, int highB)
    {
        var box = new Box(lowA, highA, lowB, highB);
        var max = (box.N + box.M + 1) / 2;
        var offset = max + 1;
        _forward[offset + 1] = 0;
        _backward[offset + 1] = 0;
        for (var d = 0; d <= max; d++)
        {
            if (ForwardPass(box, offset, d) is { } forward)
            {
                return forward;
            }

            if (BackwardPass(box, offset, d) is { } backward)
            {
                return backward;
            }
        }

        throw new InvalidOperationException("Myers middle snake not found.");
    }

    // Extends every forward d-path; with an odd delta, overlapping a backward (d-1)-path ends the search.
    private (int X, int Y)? ForwardPass(Box box, int offset, int d)
    {
        for (var k = -d; k <= d; k += 2)
        {
            var start = Start(_forward, offset, k, d);
            var x = start;
            while (x < box.N && x - k < box.M && _a[box.LowA + x] == _b[box.LowB + x - k])
            {
                x++;
            }

            _forward[offset + k] = x;
            var kb = box.Delta - k;
            if (box.Odd && Math.Abs(kb) <= d - 1 && x + _backward[offset + kb] >= box.N)
            {
                return (box.LowA + start, box.LowB + start - k);
            }
        }

        return null;
    }

    // Extends every backward d-path; with an even delta, overlapping a forward d-path ends the search.
    private (int X, int Y)? BackwardPass(Box box, int offset, int d)
    {
        for (var k = -d; k <= d; k += 2)
        {
            var x = Start(_backward, offset, k, d);
            while (x < box.N && x - k < box.M && _a[box.HighA - 1 - x] == _b[box.HighB - 1 - (x - k)])
            {
                x++;
            }

            _backward[offset + k] = x;
            var kf = box.Delta - k;
            if (!box.Odd && Math.Abs(kf) <= d && x + _forward[offset + kf] >= box.N)
            {
                return (box.HighA - x, box.HighB - (x - k));
            }
        }

        return null;
    }

    private readonly record struct Box(int LowA, int HighA, int LowB, int HighB)
    {
        public int N => HighA - LowA;

        public int M => HighB - LowB;

        public int Delta => N - M;

        public bool Odd => (Delta & 1) != 0;
    }
    private static int Start(int[] v, int offset, int k, int d) =>
        k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
}
