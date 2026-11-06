// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Editing;

/// <summary>The kind of a content control (<c>w:sdt</c>), from the type element of its properties.</summary>
public enum WordContentControlKind
{
    /// <summary>Plain text (<c>w:text</c>).</summary>
    Text,

    /// <summary>Rich text (<c>w:richText</c>, or no type element at all).</summary>
    RichText,

    /// <summary>A date picker (<c>w:date</c>).</summary>
    Date,

    /// <summary>A combo box (<c>w:comboBox</c>).</summary>
    ComboBox,

    /// <summary>A drop-down list (<c>w:dropDownList</c>).</summary>
    DropDownList,

    /// <summary>A check box (<c>w14:checkbox</c>).</summary>
    Checkbox,

    /// <summary>A picture (<c>w:picture</c>).</summary>
    Picture,

    /// <summary>Any other control: building block, citation, bibliography, equation, group, repeating section.</summary>
    Other,
}

/// <summary>Where a content control sits.</summary>
public enum WordContentControlLevel
{
    /// <summary>Wraps whole paragraphs or tables.</summary>
    Block,

    /// <summary>Wraps runs inside a paragraph.</summary>
    Inline,

    /// <summary>Wraps whole table rows.</summary>
    Row,

    /// <summary>Wraps table cells inside a row.</summary>
    Cell,
}

/// <summary>The date settings of a date content control.</summary>
/// <param name="Format">The display format (<c>w:dateFormat</c>), for example <c>dd/MM/yyyy</c>; null when absent.</param>
/// <param name="Locale">The language (<c>w:lid</c>), for example <c>fr-FR</c>; null when absent.</param>
/// <param name="FullDate">The stored date (<c>w:fullDate</c>); null when absent or unparsable.</param>
public sealed record WordContentControlDate(string? Format, string? Locale, DateTimeOffset? FullDate);

/// <summary>An entry of a combo box or drop-down list.</summary>
/// <param name="DisplayText">The text shown (<c>w:displayText</c>; the value when absent).</param>
/// <param name="Value">The stored value (<c>w:value</c>; the display text when absent).</param>
public sealed record WordContentControlItem(string DisplayText, string Value);

/// <summary>A content control (<c>w:sdt</c>) of a document opened with <see cref="WordEditor"/>, as
/// <see cref="WordEditor.ContentControls"/> lists them: a snapshot, read it again after an edit.</summary>
public sealed record WordContentControl
{
    /// <summary>The tag (<c>w:tag</c>), the key <see cref="WordEditor.FillContentControls"/> matches; null when absent.</summary>
    public string? Tag { get; init; }

    /// <summary>The title shown by Word (<c>w:alias</c>); null when absent.</summary>
    public string? Alias { get; init; }

    /// <summary>The identifier (<c>w:id</c>); null when absent.</summary>
    public int? Id { get; init; }

    /// <summary>What the control holds.</summary>
    public WordContentControlKind Kind { get; init; }

    /// <summary>The text currently shown in the control (paragraphs of a block control separated by a line feed).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>True while the control shows its placeholder text (<c>w:showingPlcHdr</c>).</summary>
    public bool ShowingPlaceholder { get; init; }

    /// <summary>The part holding the control (<c>word/document.xml</c>, <c>word/header1.xml</c>...).</summary>
    public string PartName { get; init; } = string.Empty;

    /// <summary>Where the control sits.</summary>
    public WordContentControlLevel Level { get; init; }

    /// <summary>True when the control wraps whole paragraphs or tables.</summary>
    public bool IsBlock => Level == WordContentControlLevel.Block;

    /// <summary>The date settings of a <see cref="WordContentControlKind.Date"/> control; null for the other kinds.</summary>
    public WordContentControlDate? Date { get; init; }

    /// <summary>The entries of a combo box or drop-down list; empty for the other kinds.</summary>
    public IReadOnlyList<WordContentControlItem> ListItems { get; init; } = [];

    /// <summary>The state of a <see cref="WordContentControlKind.Checkbox"/> control; null for the other kinds.</summary>
    public bool? Checked { get; init; }
}

/// <summary>A value that <see cref="WordEditor.FillContentControls"/> refused; the control is left unchanged.</summary>
/// <param name="Tag">The tag of the control.</param>
/// <param name="Reason">Why the value was refused.</param>
public sealed record WordContentControlGap(string Tag, string Reason)
{
    /// <summary>The reason with its code, to describe it in another language (<see cref="DocumentGap.Describe"/>);
    /// its <see cref="DocumentGap.Message"/> is <see cref="Reason"/>.</summary>
    public DocumentGap ReasonDetail { get; init; } = DocumentGap.FromText(Reason);
}

/// <summary>What filling content controls did.</summary>
/// <param name="Filled">The tags of which at least one control was filled, in the order given.</param>
/// <param name="UnknownTags">The tags that matched no control.</param>
/// <param name="Gaps">The values refused (unparsable date, value not in the list, unsupported kind...).</param>
/// <param name="ControlsFilled">The number of controls changed (several controls may share a tag).</param>
public sealed record WordContentControlFill(
    IReadOnlyList<string> Filled,
    IReadOnlyList<string> UnknownTags,
    IReadOnlyList<WordContentControlGap> Gaps,
    int ControlsFilled);
