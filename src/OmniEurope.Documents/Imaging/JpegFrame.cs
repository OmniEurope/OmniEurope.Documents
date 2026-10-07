// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>One colour component of a JPEG frame and its decoded coefficients.</summary>
internal sealed class JpegComponent(int id, int horizontal, int vertical, int quantTable)
{
    public int Id { get; } = id;

    public int H { get; } = horizontal;

    public int V { get; } = vertical;

    public int QuantTable { get; } = quantTable;

    /// <summary>Blocks per row and column, padded to whole MCUs.</summary>
    public int BlocksPerLine { get; set; }

    public int BlocksPerColumn { get; set; }

    /// <summary>Blocks per row and column actually covering the component (non-interleaved scans).</summary>
    public int UsedBlocksPerLine { get; set; }

    public int UsedBlocksPerColumn { get; set; }

    /// <summary>Coefficients of every block, natural order, 64 per block.</summary>
    public short[] Coefficients { get; set; } = [];

    public int DcPredictor { get; set; }

    public int DcTable { get; set; }

    public int AcTable { get; set; }

    public int BlockOffset(int blockRow, int blockColumn) => ((blockRow * BlocksPerLine) + blockColumn) * 64;
}

/// <summary>The frame header (SOF) and derived MCU geometry.</summary>
internal sealed class JpegFrame
{
    public JpegFrame(int width, int height, bool progressive, List<JpegComponent> components)
    {
        Width = width;
        Height = height;
        Progressive = progressive;
        Components = components;
        MaxH = components.Max(c => c.H);
        MaxV = components.Max(c => c.V);
        McusPerLine = (width + (8 * MaxH) - 1) / (8 * MaxH);
        McusPerColumn = (height + (8 * MaxV) - 1) / (8 * MaxV);
        foreach (var component in components)
        {
            component.BlocksPerLine = McusPerLine * component.H;
            component.BlocksPerColumn = McusPerColumn * component.V;
            component.UsedBlocksPerLine = (((width * component.H) + MaxH - 1) / MaxH + 7) / 8;
            component.UsedBlocksPerColumn = (((height * component.V) + MaxV - 1) / MaxV + 7) / 8;
            component.Coefficients = new short[(long)component.BlocksPerLine * component.BlocksPerColumn * 64];
        }
    }

    public int Width { get; }

    public int Height { get; }

    public bool Progressive { get; }

    public List<JpegComponent> Components { get; }

    public int MaxH { get; }

    public int MaxV { get; }

    public int McusPerLine { get; }

    public int McusPerColumn { get; }

    /// <summary>Zig-zag position to natural (row-major) position.</summary>
    public static readonly int[] ZigZag =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    ];
}
