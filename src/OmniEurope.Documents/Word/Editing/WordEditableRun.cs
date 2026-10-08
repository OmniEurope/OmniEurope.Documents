// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Editing;

/// <summary>What a run of <see cref="WordEditableParagraph.Runs"/> holds.</summary>
public enum WordRunKind
{
    /// <summary>Only text: <c>w:t</c>, tabs, text-wrapping breaks, carriage returns and hyphens.</summary>
    Text,

    /// <summary>A run of a field: every run from a complex field's <c>begin</c> to its <c>end</c> character
    /// (instruction and result), and every run inside a <c>w:fldSimple</c>.</summary>
    Field,

    /// <summary>A footnote reference (<c>w:footnoteReference</c>).</summary>
    FootnoteReference,

    /// <summary>An endnote reference (<c>w:endnoteReference</c>).</summary>
    EndnoteReference,

    /// <summary>A picture, shape or embedded object (<c>w:drawing</c>, <c>w:pict</c>, <c>w:object</c>).</summary>
    Drawing,

    /// <summary>A symbol character (<c>w:sym</c>).</summary>
    Symbol,

    /// <summary>A page or column break (<c>w:br</c> of type <c>page</c> or <c>column</c>).</summary>
    PageBreak,

    /// <summary>Anything else that is not text (<see cref="WordEditableRun.ElementName"/> names it), or an empty run.</summary>
    Other,
}

/// <summary>
/// A run of a paragraph opened with <see cref="WordEditor"/>, as <see cref="WordEditableParagraph.Runs"/> lists
/// them: a snapshot, its <see cref="Index"/> is the run index <see cref="WordKeptRun"/> refers to.
/// </summary>
public sealed record WordEditableRun
{
    internal WordEditableRun(int index, WordRunKind kind, string text, WordRunProperties properties, WordRunProperties resolvedProperties)
    {
        Index = index;
        Kind = kind;
        Text = text;
        Properties = properties;
        ResolvedProperties = resolvedProperties;
    }

    /// <summary>The position of the run in <see cref="WordEditableParagraph.Runs"/>, from 0.</summary>
    public int Index { get; }

    /// <summary>What the run holds.</summary>
    public WordRunKind Kind { get; }

    /// <summary>The text of a <see cref="WordRunKind.Text"/> run (tab as <c>\t</c>, line break and carriage return
    /// as <c>\n</c>, non-breaking hyphen as U+2011, soft hyphen as U+00AD); empty for the other kinds.</summary>
    public string Text { get; }

    /// <summary>For <see cref="WordRunKind.Other"/>, the local name of the element that makes the run not text
    /// (<c>commentReference</c>, <c>ptab</c>...); null for an empty run and for the other kinds.</summary>
    public string? ElementName { get; init; }

    /// <summary>The run's direct formatting (<c>w:rPr</c>).</summary>
    public WordRunProperties Properties { get; }

    /// <summary>The formatting the run shows: document defaults, table style, paragraph style, character style,
    /// then the direct formatting, theme fonts resolved.</summary>
    public WordRunProperties ResolvedProperties { get; }
}

/// <summary>A piece of the new content of a paragraph, for <see cref="WordEditableParagraph.SetContent"/>:
/// a <see cref="WordTextPiece"/> or a <see cref="WordKeptRun"/>.</summary>
public abstract record WordContentPiece
{
    private protected WordContentPiece()
    {
    }
}

/// <summary>A piece of replacement text with formatting applied over the paragraph's own (its first text run).</summary>
/// <param name="Text">The text; <c>\t</c> becomes a tab and <c>\n</c> a line break.</param>
/// <param name="Format">Formatting set on top of the base format; null keeps the base format.</param>
public sealed record WordTextPiece(string Text, WordRunProperties? Format = null) : WordContentPiece;

/// <summary>Keeps a run of the paragraph (a footnote reference, a field, a picture...) at this place of the new
/// content; a run of a field keeps the whole field.</summary>
/// <param name="RunIndex">The run's <see cref="WordEditableRun.Index"/>.</param>
public sealed record WordKeptRun(int RunIndex) : WordContentPiece;
