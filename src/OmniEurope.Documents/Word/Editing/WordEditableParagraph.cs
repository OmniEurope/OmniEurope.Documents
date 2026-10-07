// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Word.Reading;
using OmniEurope.Documents.Word.Writing;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>A piece of replacement text with formatting applied over the paragraph's own.</summary>
public sealed record WordTextPiece(string Text, WordRunProperties? Format = null);

/// <summary>
/// A paragraph of a document opened with <see cref="WordEditor"/>, addressed as <c>part#index</c> (the
/// index counts the paragraphs of the part in document order). Addresses stay valid while no paragraph is
/// added or removed.
/// </summary>
public sealed class WordEditableParagraph
{
    private static readonly string[] RunPropertyOrder =
    [
        "rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps", "strike", "dstrike", "outline", "shadow", "emboss", "imprint",
        "noProof", "snapToGrid", "vanish", "webHidden", "color", "spacing", "w", "kern", "position", "sz", "szCs", "highlight", "u",
        "effect", "bdr", "shd", "fitText", "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout", "specVanish", "oMath",
    ];

    private readonly WordReadContext _context;
    private readonly XElement _element;

    internal WordEditableParagraph(WordReadContext context, string partName, int index, XElement element)
    {
        _context = context;
        _element = element;
        PartName = partName;
        Index = index;
    }

    /// <summary>The part holding the paragraph (<c>word/document.xml</c>, <c>word/header1.xml</c>...).</summary>
    public string PartName { get; }

    /// <summary>Position among the paragraphs of the part, from 0.</summary>
    public int Index { get; }

    /// <summary>The stable address <c>part#index</c>.</summary>
    public string Address => PartName + "#" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The paragraph style id.</summary>
    public string? StyleId => Val(_element.Element(W + "pPr"), "pStyle");

    /// <summary>The visible text, field results included, deleted revisions and text boxes excluded.</summary>
    public string Text => Read().Text;

    /// <summary>The text that <see cref="SetText(string, string?)"/> replaces: text runs only, fields excluded.</summary>
    public string EditableText => WordRunScanner.Text(WordRunScanner.Runs(_element));

    /// <summary>A model snapshot of the paragraph as it is now.</summary>
    public WordParagraph Read() => new WordContentReader(_context, PartName).Paragraph(_element);

    /// <summary>
    /// Replaces the text runs with <paramref name="text"/> in the formatting of the first text run; fields,
    /// notes, pictures and breaks stay where they are. <paramref name="language"/> sets <c>w:lang</c>. Nothing
    /// changes when the text and language are already those.
    /// </summary>
    public void SetText(string text, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        SetText([new WordTextPiece(text)], language);
    }

    /// <summary>Replaces the text runs with pieces, each formatted as the first text run with its own
    /// <see cref="WordTextPiece.Format"/> applied on top.</summary>
    public void SetText(IEnumerable<WordTextPiece> pieces, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        var list = pieces.Where(p => p.Text.Length > 0).ToList();
        var runs = WordRunScanner.Runs(_element);
        var textRuns = runs.Where(r => r.IsText).Select(r => r.Element).ToList();
        if (language is null && list.All(p => p.Format is null) && string.Concat(list.Select(p => p.Text)) == WordRunScanner.Text(runs))
        {
            return;
        }

        var template = TemplateProperties(textRuns);
        var created = list.Select(p => NewRun(p, template, language)).ToList();
        if (textRuns.Count > 0)
        {
            textRuns[0].AddBeforeSelf(created);
        }
        else
        {
            _element.Add(created);
        }

        foreach (var run in textRuns)
        {
            var parent = run.Parent;
            run.Remove();
            RemoveIfEmpty(parent);
        }
    }

    private XElement? TemplateProperties(List<XElement> textRuns)
    {
        if (textRuns.Count > 0)
        {
            return textRuns[0].Element(W + "rPr") is { } own ? new XElement(own) : null;
        }

        if (_element.Element(W + "pPr")?.Element(W + "rPr") is not { } mark)
        {
            return null;
        }

        var copy = new XElement(W + "rPr", mark.Elements().Where(e => e.Name.LocalName is not ("ins" or "del" or "moveFrom" or "moveTo" or "rPrChange")).Select(e => new XElement(e)));
        return copy.HasElements ? copy : null;
    }

    private static XElement NewRun(WordTextPiece piece, XElement? template, string? language)
    {
        var properties = template is null ? new XElement(W + "rPr") : new XElement(template);
        if (piece.Format is not null && WordPropertyWriter.Run(piece.Format) is { } format)
        {
            foreach (var child in format.Elements().ToList())
            {
                properties.Elements(child.Name).Remove();
                Insert(properties, new XElement(child));
            }
        }

        if (language is not null)
        {
            var lang = properties.Element(W + "lang");
            if (lang is null)
            {
                lang = new XElement(W + "lang");
                Insert(properties, lang);
            }

            lang.SetAttributeValue(W + "val", language);
        }

        var run = new XElement(W + "r");
        if (properties.HasElements)
        {
            run.Add(properties);
        }

        foreach (var inline in WordText.Split(piece.Text, WordRunProperties.Empty))
        {
            run.Add(inline switch
            {
                WordText text => TextElement(WordContentWriter.Clean(text.Value)),
                WordTab => new XElement(W + "tab"),
                _ => new XElement(W + "br"),
            });
        }

        return run;
    }

    // Inserts a property element where the schema sequence puts it.
    private static void Insert(XElement properties, XElement child)
    {
        var rank = Array.IndexOf(RunPropertyOrder, child.Name.LocalName);
        var next = properties.Elements().FirstOrDefault(e => Array.IndexOf(RunPropertyOrder, e.Name.LocalName) is var r && (r > rank || r < 0));
        if (next is null)
        {
            properties.Add(child);
        }
        else
        {
            next.AddBeforeSelf(child);
        }
    }

    private void RemoveIfEmpty(XElement? container)
    {
        while (container is not null && container != _element && !container.HasElements
            && container.Name.LocalName is "ins" or "hyperlink" or "smartTag" or "customXml" or "moveTo")
        {
            var parent = container.Parent;
            container.Remove();
            container = parent;
        }
    }
}
