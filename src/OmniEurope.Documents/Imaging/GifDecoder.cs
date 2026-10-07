// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Imaging;

/// <summary>GIF decoding of the first frame (global or local palette, transparency, interlacing) to RGBA.</summary>
public static class GifDecoder
{
    /// <summary>True when <paramref name="data"/> starts with a GIF signature.</summary>
    public static bool IsGif(ReadOnlySpan<byte> data) => data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8);

    /// <summary>Decodes the first frame, placed on the logical screen (outside it is transparent).</summary>
    /// <exception cref="InvalidDataException">The data is not a valid GIF.</exception>
    public static RasterImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsGif(data) || data.Length < 13)
        {
            throw new InvalidDataException("Not a GIF file.");
        }

        try
        {
            return Read(data);
        }
        catch (Exception exception) when (DamagedData.IsOverrun(exception))
        {
            throw DamagedData.Error("GIF", exception);
        }
    }

    private static RasterImage Read(ReadOnlySpan<byte> data)
    {
        var width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);
        var flags = data[10];
        var position = 13;
        byte[]? globalPalette = null;
        if ((flags & 0x80) != 0)
        {
            var size = 3 * (2 << (flags & 7));
            globalPalette = data.Slice(position, size).ToArray();
            position += size;
        }

        var transparent = -1;
        while (position < data.Length)
        {
            var block = data[position];
            if (block == 0x2C)
            {
                return DecodeFrame(data, position, width, height, globalPalette, transparent);
            }

            if (block != 0x21)
            {
                throw new InvalidDataException(block == 0x3B ? "GIF without an image." : "Unexpected GIF block.");
            }

            transparent = TransparentIndex(data, position) ?? transparent;
            position = SkipSubBlocks(data, position + 2);
        }

        throw new InvalidDataException("Truncated GIF.");
    }

    // The transparent colour index of a graphic control extension (21 F9) whose transparency flag is set.
    private static int? TransparentIndex(ReadOnlySpan<byte> data, int position) =>
        position + 6 < data.Length && data[position + 1] == 0xF9 && data[position + 2] >= 4 && (data[position + 3] & 1) != 0
            ? data[position + 6]
            : null;

    private static RasterImage DecodeFrame(ReadOnlySpan<byte> data, int position, int width, int height, byte[]? palette, int transparent)
    {
        var left = BinaryPrimitives.ReadUInt16LittleEndian(data[(position + 1)..]);
        var top = BinaryPrimitives.ReadUInt16LittleEndian(data[(position + 3)..]);
        var frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(data[(position + 5)..]);
        var frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(data[(position + 7)..]);
        var flags = data[position + 9];
        position += 10;
        if ((flags & 0x80) != 0)
        {
            var size = 3 * (2 << (flags & 7));
            palette = data.Slice(position, size).ToArray();
            position += size;
        }

        if (palette is null)
        {
            throw new InvalidDataException("GIF frame without a palette.");
        }

        width = width > 0 ? width : frameWidth;
        height = height > 0 ? height : frameHeight;
        var minimumCodeSize = data[position];
        var compressed = ReadSubBlocks(data, position + 1);
        var indices = GifLzw.Decode(compressed, minimumCodeSize, frameWidth * frameHeight);
        var image = new RasterImage(width, height, ImageColorType.Rgba);
        var rows = RowOrder(frameHeight, (flags & 0x40) != 0);
        for (var row = 0; row < frameHeight; row++)
        {
            var y = top + rows[row];
            for (var x = 0; x < frameWidth; x++)
            {
                var index = indices[(row * frameWidth) + x];
                if (index == transparent || left + x >= width || y >= height || (index * 3) + 2 >= palette.Length)
                {
                    continue;
                }

                var o = ((y * width) + left + x) * 4;
                image.Pixels[o] = palette[index * 3];
                image.Pixels[o + 1] = palette[(index * 3) + 1];
                image.Pixels[o + 2] = palette[(index * 3) + 2];
                image.Pixels[o + 3] = 255;
            }
        }

        return image;
    }

    // The image row each stored row lands on (interlaced files store rows in four passes).
    private static int[] RowOrder(int height, bool interlaced)
    {
        var order = new int[height];
        if (!interlaced)
        {
            for (var i = 0; i < height; i++)
            {
                order[i] = i;
            }

            return order;
        }

        var n = 0;
        foreach (var (start, step) in (ReadOnlySpan<(int, int)>)[(0, 8), (4, 8), (2, 4), (1, 2)])
        {
            for (var y = start; y < height; y += step)
            {
                order[n++] = y;
            }
        }

        return order;
    }

    private static int SkipSubBlocks(ReadOnlySpan<byte> data, int position)
    {
        while (position < data.Length && data[position] != 0)
        {
            position += data[position] + 1;
        }

        return position + 1;
    }

    private static byte[] ReadSubBlocks(ReadOnlySpan<byte> data, int position)
    {
        using var output = new MemoryStream();
        while (position < data.Length && data[position] != 0)
        {
            var length = Math.Min(data[position], data.Length - position - 1);
            output.Write(data.Slice(position + 1, length));
            position += data[position] + 1;
        }

        return output.ToArray();
    }
}

/// <summary>The variable-length LZW of GIF (least significant bit first, codes up to 12 bits).</summary>
internal static class GifLzw
{
    public static byte[] Decode(byte[] data, int minimumCodeSize, int pixelCount)
    {
        if (minimumCodeSize is < 2 or > 8)
        {
            throw new InvalidDataException("Invalid GIF LZW code size.");
        }

        var output = new byte[pixelCount];
        var table = new CodeTable(minimumCodeSize);
        var reader = new CodeReader(data);
        var o = 0;
        while (o < pixelCount && reader.TryRead(table.CodeSize, out var code) && code != table.End)
        {
            if (code == table.Clear)
            {
                table.Reset();
            }
            else
            {
                o = table.Emit(code, output, o);
            }
        }

        return output;
    }

    /// <summary>Reads codes least significant bit first.</summary>
    private sealed class CodeReader(byte[] data)
    {
        private int _buffer;
        private int _count;
        private int _position;

        public bool TryRead(int size, out int code)
        {
            while (_count < size && _position < data.Length)
            {
                _buffer |= data[_position++] << _count;
                _count += 8;
            }

            code = _buffer & ((1 << size) - 1);
            if (_count < size)
            {
                return false;
            }

            _buffer >>= size;
            _count -= size;
            return true;
        }
    }

    /// <summary>The string table: each code is a previous code plus one character.</summary>
    private sealed class CodeTable
    {
        private const int MaxCodes = 4096;
        private readonly short[] _prefix = new short[MaxCodes];
        private readonly byte[] _suffix = new byte[MaxCodes];
        private readonly byte[] _first = new byte[MaxCodes];
        private readonly byte[] _stack = new byte[MaxCodes + 1];
        private readonly int _minimumCodeSize;
        private int _next;
        private int _previous;

        public CodeTable(int minimumCodeSize)
        {
            _minimumCodeSize = minimumCodeSize;
            Clear = 1 << minimumCodeSize;
            End = Clear + 1;
            for (var i = 0; i < Clear; i++)
            {
                _suffix[i] = (byte)i;
                _first[i] = (byte)i;
            }

            Reset();
        }

        public int Clear { get; }

        public int End { get; }

        public int CodeSize { get; private set; }

        public void Reset()
        {
            CodeSize = _minimumCodeSize + 1;
            _next = Clear + 2;
            _previous = -1;
        }

        // Writes the string of code at o and returns the new output position.
        public int Emit(int code, byte[] output, int o)
        {
            if (_previous < 0)
            {
                output[o] = _suffix[code];
                _previous = code;
                return o + 1;
            }

            var top = Expand(code);
            while (top > 0 && o < output.Length)
            {
                output[o++] = _stack[--top];
            }

            _previous = code;
            return o;
        }

        // Pushes the string of code (reversed) and defines the next code; returns the stack height.
        private int Expand(int code)
        {
            var current = code;
            var top = 0;
            if (code >= _next)
            {
                // The code being defined: the previous string plus its own first character.
                _stack[top++] = _first[_previous];
                current = _previous;
            }

            while (current >= Clear)
            {
                _stack[top++] = _suffix[current];
                current = _prefix[current];
            }

            _stack[top++] = _suffix[current];
            Define(_suffix[current]);
            return top;
        }

        private void Define(byte firstOfCode)
        {
            if (_next >= MaxCodes)
            {
                return;
            }

            _prefix[_next] = (short)_previous;
            _suffix[_next] = firstOfCode;
            _first[_next] = _first[_previous];
            _next++;
            if (_next == 1 << CodeSize && CodeSize < 12)
            {
                CodeSize++;
            }
        }
    }
}
