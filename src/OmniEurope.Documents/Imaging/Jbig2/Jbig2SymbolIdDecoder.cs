// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>The symbol ID decoding procedure IAID of T.88 A.3: a fixed number of bits with their own contexts.</summary>
internal sealed class Jbig2SymbolIdDecoder(int codeLength)
{
    private readonly byte[] _contexts = new byte[1 << Math.Min(codeLength + 1, 31)];

    public int Decode(MqDecoder decoder)
    {
        var previous = 1;
        for (var i = 0; i < codeLength; i++)
        {
            previous = (previous << 1) | decoder.Decode(_contexts, previous);
        }

        return previous - (1 << codeLength);
    }
}
