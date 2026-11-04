// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word.Editing;

namespace OmniEurope.Documents.Word;

/// <summary>
/// A content control (<c>w:sdt</c>) that a block or an inline was read from, as <see cref="WordBlock.ContentControls"/>
/// and <see cref="WordInline.ContentControls"/> list them. The control stays transparent to every consumer of the
/// model; only the HTML conversion reads the marks. Two items belong to the same control when they share the same
/// mark instance.
/// </summary>
public sealed class WordContentControlMark
{
    internal WordContentControlMark()
    {
    }

    /// <summary>The tag (<c>w:tag</c>); null when absent.</summary>
    public string? Tag { get; internal init; }

    /// <summary>The title shown by Word (<c>w:alias</c>); null when absent.</summary>
    public string? Alias { get; internal init; }

    /// <summary>The identifier (<c>w:id</c>); null when absent.</summary>
    public int? Id { get; internal init; }

    /// <summary>What the control holds.</summary>
    public WordContentControlKind Kind { get; internal init; }

    /// <summary>True while the control shows its placeholder text (<c>w:showingPlcHdr</c>).</summary>
    public bool ShowingPlaceholder { get; internal init; }

    internal static IReadOnlyList<WordContentControlMark> None { get; } = [];
}
