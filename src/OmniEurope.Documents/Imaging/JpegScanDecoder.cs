// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>
/// Decodes the entropy-coded data of one scan into the coefficient buffers: baseline (sequential) scans
/// and the four progressive kinds (DC first, DC refinement, AC first, AC refinement), interleaved or not,
/// with restart intervals.
/// </summary>
internal sealed class JpegScanDecoder(
    JpegFrame frame,
    IReadOnlyList<JpegComponent> components,
    JpegHuffmanTable?[] dcTables,
    JpegHuffmanTable?[] acTables,
    int spectralStart,
    int spectralEnd,
    int approximationHigh,
    int approximationLow,
    int restartInterval)
{
    private int _eobRun;

    public int Decode(byte[] data, int position)
    {
        var reader = new JpegBitReader(data, position);
        foreach (var component in components)
        {
            component.DcPredictor = 0;
        }

        var mcu = 0;
        if (components.Count == 1)
        {
            var component = components[0];
            var total = component.UsedBlocksPerLine * component.UsedBlocksPerColumn;
            for (var row = 0; row < component.UsedBlocksPerColumn; row++)
            {
                for (var column = 0; column < component.UsedBlocksPerLine; column++)
                {
                    DecodeBlock(reader, component, component.BlockOffset(row, column));
                    AfterMcu(reader, ++mcu, total);
                }
            }
        }
        else
        {
            var total = frame.McusPerLine * frame.McusPerColumn;
            for (var mcuRow = 0; mcuRow < frame.McusPerColumn; mcuRow++)
            {
                for (var mcuColumn = 0; mcuColumn < frame.McusPerLine; mcuColumn++)
                {
                    DecodeMcu(reader, mcuRow, mcuColumn);
                    AfterMcu(reader, ++mcu, total);
                }
            }
        }

        return reader.SkipToMarker();
    }

    private void DecodeMcu(JpegBitReader reader, int mcuRow, int mcuColumn)
    {
        foreach (var component in components)
        {
            for (var v = 0; v < component.V; v++)
            {
                for (var h = 0; h < component.H; h++)
                {
                    var offset = component.BlockOffset((mcuRow * component.V) + v, (mcuColumn * component.H) + h);
                    DecodeBlock(reader, component, offset);
                }
            }
        }
    }

    private void AfterMcu(JpegBitReader reader, int mcu, int total)
    {
        if (restartInterval <= 0 || mcu % restartInterval != 0 || mcu >= total)
        {
            return;
        }

        reader.Restart();
        _eobRun = 0;
        foreach (var component in components)
        {
            component.DcPredictor = 0;
        }
    }

    private void DecodeBlock(JpegBitReader reader, JpegComponent component, int offset)
    {
        var block = component.Coefficients.AsSpan(offset, 64);
        if (!frame.Progressive)
        {
            DecodeBaseline(reader, component, block);
        }
        else if (spectralStart == 0)
        {
            DecodeDc(reader, component, block);
        }
        else if (approximationHigh == 0)
        {
            DecodeAcFirst(reader, component, block);
        }
        else
        {
            DecodeAcRefine(reader, component, block);
        }
    }

    private void DecodeBaseline(JpegBitReader reader, JpegComponent component, Span<short> block)
    {
        var size = Table(dcTables, component.DcTable).Decode(reader);
        component.DcPredictor += reader.ReceiveExtend(size);
        block[0] = (short)component.DcPredictor;
        var ac = Table(acTables, component.AcTable);
        for (var k = 1; k < 64;)
        {
            var rs = ac.Decode(reader);
            var run = rs >> 4;
            var bits = rs & 15;
            if (bits == 0)
            {
                if (run != 15)
                {
                    break;
                }

                k += 16;
                continue;
            }

            k += run;
            if (k > 63)
            {
                break;
            }

            block[JpegFrame.ZigZag[k++]] = (short)reader.ReceiveExtend(bits);
        }
    }

    private void DecodeDc(JpegBitReader reader, JpegComponent component, Span<short> block)
    {
        if (approximationHigh == 0)
        {
            var size = Table(dcTables, component.DcTable).Decode(reader);
            component.DcPredictor += reader.ReceiveExtend(size);
            block[0] = (short)(component.DcPredictor << approximationLow);
        }
        else if (reader.ReadBit() == 1)
        {
            block[0] |= (short)(1 << approximationLow);
        }
    }

    private void DecodeAcFirst(JpegBitReader reader, JpegComponent component, Span<short> block)
    {
        if (_eobRun > 0)
        {
            _eobRun--;
            return;
        }

        var ac = Table(acTables, component.AcTable);
        for (var k = spectralStart; k <= spectralEnd;)
        {
            var rs = ac.Decode(reader);
            var run = rs >> 4;
            var bits = rs & 15;
            if (bits == 0)
            {
                if (run < 15)
                {
                    _eobRun = (1 << run) - 1 + (run > 0 ? reader.Receive(run) : 0);
                    break;
                }

                k += 16;
                continue;
            }

            k += run;
            if (k > 63)
            {
                break;
            }

            block[JpegFrame.ZigZag[k++]] = (short)(reader.ReceiveExtend(bits) * (1 << approximationLow));
        }
    }

    private void DecodeAcRefine(JpegBitReader reader, JpegComponent component, Span<short> block)
    {
        var plus = 1 << approximationLow;
        var minus = -1 << approximationLow;
        var k = spectralStart;
        if (_eobRun == 0)
        {
            var ac = Table(acTables, component.AcTable);
            while (k <= spectralEnd)
            {
                var rs = ac.Decode(reader);
                var run = rs >> 4;
                var bits = rs & 15;
                var value = 0;
                if (bits != 0)
                {
                    value = reader.ReadBit() == 1 ? plus : minus;
                }
                else if (run != 15)
                {
                    _eobRun = (1 << run) + (run > 0 ? reader.Receive(run) : 0);
                    break;
                }

                k = SkipZeros(reader, block, k, run, value, plus, minus);
            }
        }

        if (_eobRun > 0)
        {
            for (; k <= spectralEnd; k++)
            {
                RefineNonZero(reader, block, JpegFrame.ZigZag[k], plus, minus);
            }

            _eobRun--;
        }
    }

    // Steps over `run` zero-history coefficients (refining the non-zero ones met on the way), then places
    // `value` on the next zero-history coefficient. Returns the position after it.
    private int SkipZeros(JpegBitReader reader, Span<short> block, int k, int run, int value, int plus, int minus)
    {
        while (k <= spectralEnd)
        {
            var z = JpegFrame.ZigZag[k];
            if (block[z] != 0)
            {
                RefineNonZero(reader, block, z, plus, minus);
            }
            else
            {
                if (run == 0)
                {
                    if (value != 0)
                    {
                        block[z] = (short)value;
                    }

                    return k + 1;
                }

                run--;
            }

            k++;
        }

        return k;
    }

    private static void RefineNonZero(JpegBitReader reader, Span<short> block, int z, int plus, int minus)
    {
        if (block[z] != 0 && reader.ReadBit() == 1 && (block[z] & plus) == 0)
        {
            block[z] = (short)(block[z] >= 0 ? block[z] + plus : block[z] + minus);
        }
    }

    private static JpegHuffmanTable Table(JpegHuffmanTable?[] tables, int index) =>
        index < tables.Length && tables[index] is { } table ? table : throw new InvalidDataException("JPEG scan uses an undefined Huffman table.");
}
