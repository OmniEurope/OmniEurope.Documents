// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Word.Writing;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>Builds the new runs of an edited paragraph and tidies the wrappers its old runs leave empty.</summary>
internal static class WordRunBuilder
{
    private static readonly string[] RunPropertyOrder =
    [
        "rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps", "strike", "dstrike", "outline", "shadow", "emboss", "imprint",
        "noProof", "snapToGrid", "vanish", "webHidden", "color", "spacing", "w", "kern", "position", "sz", "szCs", "highlight", "u",
        "effect", "bdr", "shd", "fitText", "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout", "specVanish", "oMath",
    ];

    /// <summary>Wrappers removed once the runs they held are gone.</summary>
    public static readonly HashSet<string> Removable = new(StringComparer.Ordinal) { "ins", "hyperlink", "smartTag", "customXml", "moveTo" };

    /// <summary>The base format: the first text run's own properties, else the paragraph mark's (revision marks dropped).</summary>
    public static XElement? Template(XElement paragraph, XElement? firstTextRun)
    {
        if (firstTextRun is not null)
        {
            return firstTextRun.Element(W + "rPr") is { } own ? new XElement(own) : null;
        }

        if (paragraph.Element(W + "pPr")?.Element(W + "rPr") is not { } mark)
        {
            return null;
        }

        var copy = new XElement(W + "rPr", mark.Elements().Where(e => e.Name.LocalName is not ("ins" or "del" or "moveFrom" or "moveTo" or "rPrChange")).Select(e => new XElement(e)));
        return copy.HasElements ? copy : null;
    }

    public static XElement NewRun(WordTextPiece piece, XElement? template, string? language)
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

    /// <summary>Removes <paramref name="container"/> and its ancestors below the paragraph while they are
    /// empty removable wrappers.</summary>
    public static void RemoveIfEmpty(XElement paragraph, XElement? container)
    {
        while (container is not null && container != paragraph && !container.HasElements && Removable.Contains(container.Name.LocalName))
        {
            var parent = container.Parent;
            container.Remove();
            container = parent;
        }
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
}
