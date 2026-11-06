// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Conversion.WordFields;

/// <summary>The codes of the gaps <see cref="WordFieldUpdater"/> reports; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class FieldGapCodes
{
    public static readonly GapCode FieldTocSwitchLeftAsIs = new(nameof(FieldTocSwitchLeftAsIs));
    public static readonly GapCode FieldTocNotWholeParagraphs = new(nameof(FieldTocNotWholeParagraphs));
    public static readonly GapCode FieldTocTcEntriesNotCollected = new(nameof(FieldTocTcEntriesNotCollected));
    public static readonly GapCode FieldPageNumbersStillMoving = new(nameof(FieldPageNumbersStillMoving));
    public static readonly GapCode FieldKindNotUpdated = new(nameof(FieldKindNotUpdated));
    public static readonly GapCode FieldDateWithoutCoreDate = new(nameof(FieldDateWithoutCoreDate));
    public static readonly GapCode FieldOutsideLaidOutBody = new(nameof(FieldOutsideLaidOutBody));
    public static readonly GapCode FieldBookmarkMissing = new(nameof(FieldBookmarkMissing));
    public static readonly GapCode FieldRefParagraphNumber = new(nameof(FieldRefParagraphNumber));
    public static readonly GapCode FieldPageRefRelativePosition = new(nameof(FieldPageRefRelativePosition));
}
