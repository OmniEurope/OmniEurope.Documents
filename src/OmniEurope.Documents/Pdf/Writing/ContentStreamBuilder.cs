// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>Builds the operators of a content stream (ISO 32000-1 §8, §9).</summary>
internal sealed class ContentStreamBuilder
{
    private readonly StringBuilder _text = new();

    public int Length => _text.Length;

    public ContentStreamBuilder Op(string op, params ReadOnlySpan<double> operands)
    {
        foreach (var operand in operands)
        {
            _text.Append(PdfFormat.Real(operand)).Append(' ');
        }

        _text.Append(op).Append('\n');
        return this;
    }

    public ContentStreamBuilder Raw(string text)
    {
        _text.Append(text);
        return this;
    }

    public ContentStreamBuilder SaveState() => Op("q");

    public ContentStreamBuilder RestoreState() => Op("Q");

    public ContentStreamBuilder FillColor(PdfColor color) => Op("rg", color.R / 255.0, color.G / 255.0, color.B / 255.0);

    public ContentStreamBuilder StrokeColor(PdfColor color) => Op("RG", color.R / 255.0, color.G / 255.0, color.B / 255.0);

    public ContentStreamBuilder Name(string name)
    {
        _text.Append('/').Append(name).Append(' ');
        return this;
    }

    public ContentStreamBuilder Hex(ReadOnlySpan<byte> bytes)
    {
        _text.Append('<').Append(Convert.ToHexString(bytes)).Append("> ");
        return this;
    }

    public byte[] ToBytes() => Encoding.ASCII.GetBytes(_text.ToString());
}
