// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>One pen trace of an InkML part: its points and its pen, all in centimetres.</summary>
/// <param name="Points">The sampled positions, x to the right and y downwards.</param>
/// <param name="Width">The pen width.</param>
/// <param name="Color">The pen colour as six hexadecimal digits.</param>
/// <param name="Opacity">1 for an opaque pen, less for a translucent one (a highlighter).</param>
internal sealed record InkTrace(IReadOnlyList<(double X, double Y)> Points, double Width, string Color, double Opacity);

/// <summary>
/// Reads the traces of an InkML part (W3C InkML): the X and Y channels of the context a trace names, in the channel
/// units and resolution that context declares, decoded from the compact text form (explicit values, first and second
/// differences) and paired with the pen of the brush the trace names.
/// </summary>
internal static class InkMlReader
{
    private const string DefaultColor = "000000";
    private const double DefaultWidth = 0.05;
    private const double DigitiserUnit = 0.001;

    /// <summary>The most points an ink part may hold, all traces together: past it the part is refused.</summary>
    internal const int MaxPoints = 1_000_000;
    private static readonly XNamespace Ink = "http://www.w3.org/2003/InkML";

    // A unit as centimetres; a value without a known unit is a hundredth of a millimetre (the digitiser unit).
    private static readonly Dictionary<string, double> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cm"] = 1,
        ["mm"] = 0.1,
        ["in"] = 2.54,
        ["pt"] = 2.54 / 72,
        ["pc"] = 2.54 / 6,
        ["m"] = 100,
    };

    /// <summary>The traces of an InkML part, in order; pen-up traces (movements without ink) are left out.</summary>
    /// <exception cref="DocumentFormatException">The part holds more than <see cref="MaxPoints"/> points.</exception>
    public static List<InkTrace> Read(XElement root)
    {
        var contexts = ById(root, "context");
        var brushes = ById(root, "brush");
        var defaultContext = contexts.Values.FirstOrDefault();
        var defaultBrush = brushes.Values.FirstOrDefault();
        var traces = new List<InkTrace>();
        var total = 0;
        foreach (var trace in root.Descendants(Ink + "trace"))
        {
            if ((string?)trace.Attribute("type") is "penUp" or "indeterminate")
            {
                continue;
            }

            var context = Lookup(contexts, (string?)trace.Attribute("contextRef"))
                ?? trace.Ancestors(Ink + "traceGroup").Select(g => Lookup(contexts, (string?)g.Attribute("contextRef"))).FirstOrDefault(c => c is not null)
                ?? defaultContext;
            var brush = Lookup(brushes, (string?)trace.Attribute("brushRef")) ?? defaultBrush;
            var points = Decode(trace.Value, Channels(context), MaxPoints - total);
            total += points.Count;
            if (points.Count > 0)
            {
                traces.Add(Pen(brush, points));
            }
        }

        return traces;
    }

    private static Dictionary<string, XElement> ById(XElement root, string name)
    {
        var table = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var element in root.Descendants(Ink + name))
        {
            if ((string?)element.Attribute(XNamespace.Xml + "id") is { } id)
            {
                table.TryAdd(id, element);
            }
        }

        return table;
    }

    private static XElement? Lookup(Dictionary<string, XElement> table, string? reference) =>
        reference is not null && table.TryGetValue(reference.TrimStart('#'), out var found) ? found : null;

    // The channels of the context's trace format, each with the centimetres one value stands for.
    private static List<(string Name, double Scale)> Channels(XElement? context)
    {
        var channels = new List<(string Name, double Scale)>();
        var format = context?.Descendants(Ink + "traceFormat").FirstOrDefault();
        foreach (var channel in format?.Elements(Ink + "channel") ?? [])
        {
            var name = (string?)channel.Attribute("name") ?? string.Empty;
            var resolution = context!.Descendants(Ink + "channelProperty")
                .Where(p => (string?)p.Attribute("channel") == name && (string?)p.Attribute("name") == "resolution")
                .Select(p => Number((string?)p.Attribute("value"))).FirstOrDefault(v => v > 0);
            var perUnit = resolution > 0 ? resolution : 1;
            channels.Add((name, UnitInCentimetres((string?)channel.Attribute("units")) / perUnit));
        }

        return channels.Count >= 2 ? channels : [("X", DigitiserUnit), ("Y", DigitiserUnit)];
    }

    private static double UnitInCentimetres(string? unit) => unit is not null && Units.TryGetValue(unit, out var factor) ? factor : DigitiserUnit;

    private static InkTrace Pen(XElement? brush, List<(double X, double Y)> points)
    {
        var color = Property(brush, "color") is { } text && text.TrimStart('#') is { Length: 6 } hex && hex.All(char.IsAsciiHexDigit) ? hex.ToUpperInvariant() : DefaultColor;
        var transparency = Number(Property(brush, "transparency"));
        var opacity = transparency > 0 ? Math.Clamp(1 - (transparency / 255), 0, 1) : 1;
        return new InkTrace(points, Length(brush, "width") ?? Length(brush, "height") ?? DefaultWidth, color, opacity);
    }

    private static XElement? PropertyElement(XElement? brush, string name) =>
        brush?.Elements(Ink + "brushProperty").FirstOrDefault(p => (string?)p.Attribute("name") == name);

    private static string? Property(XElement? brush, string name) => (string?)PropertyElement(brush, name)?.Attribute("value");

    private static double? Length(XElement? brush, string name)
    {
        var value = Number(Property(brush, name));
        return value > 0 ? value * UnitInCentimetres((string?)PropertyElement(brush, name)?.Attribute("units")) : null;
    }

    private static double Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0;

    // The compact text form: points separated by commas, the values of a point by blanks (or by the sign of the next
    // value). A value may carry a prefix: "!" explicit, "'" first difference, '"' second difference; a prefix stays
    // in force for its channel until another replaces it. A missing value ("?" or "*") repeats the previous one.
    private static List<(double X, double Y)> Decode(string text, List<(string Name, double Scale)> channels, int budget)
    {
        var x = Math.Max(0, channels.FindIndex(c => c.Name == "X"));
        var y = Math.Max(0, channels.FindIndex(c => c.Name == "Y"));
        var decoder = new TraceDecoder(channels.Count);
        var points = new List<(double X, double Y)>();

        void EndPoint()
        {
            decoder.EndValue();
            if (decoder.Count > Math.Max(x, y))
            {
                if (points.Count >= budget)
                {
                    throw new DocumentFormatException($"The ink part holds more than {MaxPoints} points.");
                }

                points.Add((decoder.Values[x] * channels[x].Scale, decoder.Values[y] * channels[y].Scale));
            }

            decoder.NewPoint();
        }

        foreach (var c in text)
        {
            if (c == ',')
            {
                EndPoint();
            }
            else
            {
                decoder.Add(c);
            }
        }

        EndPoint();
        return points;
    }

    // Splits the characters of a trace into values and applies each to its channel.
    private sealed class TraceDecoder(int channelCount)
    {
        private readonly ChannelState[] _channels = new ChannelState[channelCount];
        private readonly StringBuilder _token = new();
        private char _prefix;

        public double[] Values { get; } = new double[channelCount];

        public int Count { get; private set; }

        public void Add(char c)
        {
            if (c is '?' or '*')
            {
                EndValue();
                Store(Count < _channels.Length ? _channels[Count].Last : 0);
            }
            else if (c is '!' or '\'' or '"')
            {
                EndValue();
                _prefix = c;
            }
            else if (c is '-' or '+')
            {
                EndValue();
                _token.Append(c);
            }
            else if (char.IsAsciiDigit(c) || c == '.')
            {
                _token.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                EndValue();
            }
        }

        public void EndValue()
        {
            if (_token.Length == 0)
            {
                return;
            }

            Store(Count < _channels.Length ? _channels[Count].Apply(_prefix, _token.ToString()) : 0);
            _token.Clear();
            _prefix = '\0';
        }

        private void Store(double value)
        {
            if (Count < Values.Length)
            {
                Values[Count] = value;
            }

            Count++;
        }

        public void NewPoint() => Count = 0;
    }

    // The decoding state of one channel: its mode, its last value and its last first difference.
    private struct ChannelState
    {
        private char _mode;
        private double _value;
        private double _velocity;

        public readonly double Last => _value;

        public double Apply(char prefix, string text)
        {
            _mode = prefix != '\0' ? prefix : _mode;
            var number = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            switch (_mode)
            {
                case '\'':
                    _velocity = number;
                    _value += _velocity;
                    break;
                case '"':
                    _velocity += number;
                    _value += _velocity;
                    break;
                default:
                    _value = number;
                    _velocity = 0;
                    break;
            }

            return _value;
        }
    }
}
