// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Imaging.Jbig2;

namespace OmniEurope.Documents.Tests.Imaging.Jbig2;

/// <summary>The MQ decoder and the arithmetic integer and symbol ID procedures (T.88 annex A, annex E).</summary>
public sealed class Jbig2ArithmeticTests
{
    [Fact]
    public void Integers_of_every_prefix_range_and_the_out_of_band_value_decode()
    {
        int?[] values = [0, 3, -3, 4, 19, -20, 83, 84, 339, -340, 4435, 4436, 1_000_000, -2_000_000, null, 7];
        var writer = new ArithmeticWriter();
        foreach (var value in values)
        {
            writer.Integer("IAx", value);
        }

        writer.SymbolId(5, 3);
        writer.SymbolId(2, 3);
        var data = writer.Mq.Finish();
        var decoder = new MqDecoder(data, 0, data.Length);
        var integer = new Jbig2IntegerDecoder();
        var id = new Jbig2SymbolIdDecoder(3);

        Assert.Equal(values, values.Select(_ => integer.Decode(decoder)));
        Assert.Equal(5, id.Decode(decoder));
        Assert.Equal(2, id.Decode(decoder));
    }

    [Fact]
    public void Data_that_would_be_read_far_past_its_end_is_refused()
    {
        var contexts = new byte[1 << 16];
        var decoder = new MqDecoder([0x12, 0x34], 0, 2);

        Assert.Throws<InvalidDataException>(() =>
        {
            for (var i = 0; i < 10_000_000; i++)
            {
                decoder.Decode(contexts, i & 0xFFFF);
            }
        });
    }

    [Fact]
    public void A_long_run_of_one_symbol_survives_the_marker_and_the_end_of_the_data()
    {
        var contexts = new byte[1];
        var writer = new MqWriter();
        var bits = Enumerable.Range(0, 5000).Select(i => i % 997 == 0 ? 1 : 0).ToArray();
        foreach (var bit in bits)
        {
            writer.Encode(contexts, 0, bit);
        }

        var data = writer.Finish();
        var decoded = new byte[1];
        var decoder = new MqDecoder(data, 0, data.Length);

        Assert.Equal(bits, bits.Select(_ => decoder.Decode(decoded, 0)));
        Assert.Equal(0xAC, data[^1]);
    }
}
