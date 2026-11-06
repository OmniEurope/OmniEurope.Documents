// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>The codes of the gaps of Word to HTML; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class HtmlGapCodes
{
    public static readonly GapCode HtmlParagraphWithoutAddress = new(nameof(HtmlParagraphWithoutAddress));
    public static readonly GapCode HtmlUnreadablePictureLeftOut = new(nameof(HtmlUnreadablePictureLeftOut));
    public static readonly GapCode HtmlPictureFormatLeftOut = new(nameof(HtmlPictureFormatLeftOut));
    public static readonly GapCode HtmlTabStopsAsFixedTabs = new(nameof(HtmlTabStopsAsFixedTabs));
    public static readonly GapCode HtmlInkLeftOut = new(nameof(HtmlInkLeftOut));
    public static readonly GapCode HtmlChartsLeftOut = new(nameof(HtmlChartsLeftOut));
    public static readonly GapCode HtmlColumnBreaksAsLineBreaks = new(nameof(HtmlColumnBreaksAsLineBreaks));
    public static readonly GapCode HtmlPageNumberFieldsLastValue = new(nameof(HtmlPageNumberFieldsLastValue));
    public static readonly GapCode HtmlLinksOtherSchemeWithoutAddress = new(nameof(HtmlLinksOtherSchemeWithoutAddress));
    public static readonly GapCode HtmlBookmarksNotKept = new(nameof(HtmlBookmarksNotKept));
    public static readonly GapCode HtmlFloatingShapesInTextFlow = new(nameof(HtmlFloatingShapesInTextFlow));
    public static readonly GapCode HtmlBreaksComputedByBrowser = new(nameof(HtmlBreaksComputedByBrowser));
    public static readonly GapCode HtmlLineHeightsApproximated = new(nameof(HtmlLineHeightsApproximated));
    public static readonly GapCode HtmlCommentsLeftOut = new(nameof(HtmlCommentsLeftOut));
    public static readonly GapCode HtmlFootnoteRestartContinues = new(nameof(HtmlFootnoteRestartContinues));
    public static readonly GapCode HtmlUnequalColumnsAsEqual = new(nameof(HtmlUnequalColumnsAsEqual));
    public static readonly GapCode HtmlHeadersFootersLeftOut = new(nameof(HtmlHeadersFootersLeftOut));
    public static readonly GapCode HtmlFootnotesAtPageEnd = new(nameof(HtmlFootnotesAtPageEnd));
}
