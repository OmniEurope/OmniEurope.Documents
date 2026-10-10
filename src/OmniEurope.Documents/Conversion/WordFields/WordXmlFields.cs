// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using System.Xml.Linq;
using OmniEurope.Documents.Word.Editing;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Conversion.WordFields;

/// <summary>
/// A field of a part's markup: a complex field (<c>w:fldChar</c> begin, instruction, separate, result, end) or a
/// simple one (<c>w:fldSimple</c>). <see cref="InInstruction"/> marks a field nested in another field's instruction,
/// whose value only that field uses.
/// </summary>
internal sealed class XmlField
{
    public required XElement Begin { get; init; }

    public XElement? Separate { get; set; }

    public XElement? End { get; set; }

    public bool IsSimple { get; init; }

    public bool InInstruction { get; init; }

    public StringBuilder InstructionText { get; } = new();

    public string Instruction => InstructionText.ToString().Trim();

    public IReadOnlyList<string> Tokens => WordXmlFields.Tokens(Instruction);

    public string Kind => Tokens.Count > 0 ? Tokens[0].ToUpperInvariant() : string.Empty;

    /// <summary>The paragraph the field begins in.</summary>
    public XElement? Paragraph => Begin.Ancestors(W + "p").FirstOrDefault();
}

/// <summary>Finds the fields of a part and rewrites their results.</summary>
internal static class WordXmlFields
{
    /// <summary>The fields in document order of their end; deleted fields and alternate-content fallbacks are skipped.</summary>
    public static List<XmlField> Scan(XElement root)
    {
        var fields = new List<XmlField>();
        var open = new Stack<XmlField>();
        foreach (var element in root.Descendants().Where(e => (e.Name == W + "r" || e.Name == W + "fldSimple") && Live(e)))
        {
            if (element.Name == W + "fldSimple")
            {
                var simple = new XmlField { Begin = element, IsSimple = true, InInstruction = open.Count > 0 && open.Peek().Separate is null };
                simple.InstructionText.Append(Attr(element, "instr"));
                fields.Add(simple);
                continue;
            }

            foreach (var child in element.Elements())
            {
                Step(child, element, open, fields);
            }
        }

        return fields;
    }

    private static void Step(XElement child, XElement run, Stack<XmlField> open, List<XmlField> fields)
    {
        if (child.Name == W + "instrText" && open.Count > 0 && open.Peek().Separate is null)
        {
            open.Peek().InstructionText.Append(child.Value);
            return;
        }

        if (child.Name != W + "fldChar")
        {
            return;
        }

        switch (Attr(child, "fldCharType"))
        {
            case "begin":
                open.Push(new XmlField { Begin = run, InInstruction = open.Count > 0 && open.Peek().Separate is null });
                break;
            case "separate" when open.Count > 0:
                open.Peek().Separate ??= run;
                break;
            case "end" when open.Count > 0:
                var field = open.Pop();
                field.End = run;
                fields.Add(field);
                break;
        }
    }

    private static bool Live(XElement element) =>
        element.Ancestors().All(a => a.Name != W + "del" && a.Name != W + "moveFrom" && a.Name != Mc + "Fallback");

    /// <summary>The words of an instruction: quoted text is one word, without its quotes.</summary>
    public static List<string> Tokens(string instruction)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var c in instruction)
        {
            if (c == '"')
            {
                quoted = !quoted;
                if (!quoted)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            if (!quoted && char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    /// <summary>The argument following a switch (<c>\o "1-3"</c>), or null when the switch is absent.</summary>
    public static string? Switch(IReadOnlyList<string> tokens, string name, bool hasArgument = true)
    {
        for (var i = 1; i < tokens.Count; i++)
        {
            if (string.Equals(tokens[i], "\\" + name, StringComparison.OrdinalIgnoreCase))
            {
                return hasArgument && i + 1 < tokens.Count && !tokens[i + 1].StartsWith('\\') ? tokens[i + 1] : string.Empty;
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces the field's result by <paramref name="text"/> in one run formatted like the former result's first run
    /// (or the field's first run); returns false when the result already reads so or spans several paragraphs.
    /// </summary>
    public static bool SetResult(XmlField field, string text)
    {
        if (field.IsSimple)
        {
            var simpleRuns = field.Begin.Elements(W + "r").ToList();
            if (Text(simpleRuns) == text)
            {
                return false;
            }

            var template = simpleRuns.FirstOrDefault()?.Element(W + "rPr");
            field.Begin.Nodes().Where(n => n is not XElement { Name.LocalName: "fldData" }).ToList().ForEach(n => n.Remove());
            field.Begin.Add(Run(template, text));
            return true;
        }

        var paragraph = field.Paragraph;
        if (field.End is null || paragraph is null || field.End.Ancestors(W + "p").FirstOrDefault() != paragraph)
        {
            return false;
        }

        var runs = paragraph.Descendants(W + "r").ToList();
        var start = field.Separate is { } separate ? runs.IndexOf(separate) : -1;
        var result = start < 0 ? [] : runs.Skip(start + 1).TakeWhile(r => r != field.End).ToList();
        if (start >= 0 && Text(result) == text)
        {
            return false;
        }

        var formatting = result.Select(r => r.Element(W + "rPr")).FirstOrDefault(p => p is not null) ?? field.Begin.Element(W + "rPr");
        foreach (var run in result)
        {
            var parent = run.Parent;
            run.Remove();
            WordRunBuilder.RemoveIfEmpty(paragraph, parent);
        }

        var value = Run(formatting, text);
        if (field.Separate is null)
        {
            field.Separate = Run(formatting, null, new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "separate")));
            field.End.AddBeforeSelf(field.Separate);
        }

        field.Separate.AddAfterSelf(value);
        return true;
    }

    private static string Text(IEnumerable<XElement> runs) => string.Concat(runs.SelectMany(r => r.Elements(W + "t")).Select(t => t.Value));

    public static XElement Run(XElement? formatting, string? text, params object[] content)
    {
        var run = new XElement(W + "r");
        if (formatting is not null)
        {
            run.Add(new XElement(W + "rPr", formatting.Elements().Where(e => e.Name.LocalName != "rPrChange").Select(e => new XElement(e))));
        }

        if (text is not null)
        {
            run.Add(TextElement(text));
        }

        run.Add(content);
        return run;
    }
}
