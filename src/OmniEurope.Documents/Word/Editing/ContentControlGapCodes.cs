// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Editing;

/// <summary>The codes of the reasons <see cref="WordEditor.FillContentControls"/> gives for refusing a value; each is a
/// key of <c>Resources/GapMessages.resx</c>.</summary>
internal static class ContentControlGapCodes
{
    public static readonly GapCode ContentControlNotDate = new(nameof(ContentControlNotDate));
    public static readonly GapCode ContentControlNotList = new(nameof(ContentControlNotList));
    public static readonly GapCode ContentControlNotCheckbox = new(nameof(ContentControlNotCheckbox));
    public static readonly GapCode ContentControlNotBoolean = new(nameof(ContentControlNotBoolean));
    public static readonly GapCode ContentControlKindNotFillable = new(nameof(ContentControlKindNotFillable));
    public static readonly GapCode ContentControlValueNotInList = new(nameof(ContentControlValueNotInList));
    public static readonly GapCode ContentControlDateUnparsable = new(nameof(ContentControlDateUnparsable));
    public static readonly GapCode ContentControlDateUnparsableLanguageMissing = new(nameof(ContentControlDateUnparsableLanguageMissing));
    public static readonly GapCode ContentControlTableStructure = new(nameof(ContentControlTableStructure));
    public static readonly GapCode ContentControlHoldsMoreThanText = new(nameof(ContentControlHoldsMoreThanText));
    public static readonly GapCode ContentControlHoldsMoreThanParagraphs = new(nameof(ContentControlHoldsMoreThanParagraphs));
}
