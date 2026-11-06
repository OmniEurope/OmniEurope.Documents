// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Conversion.HtmlLayout;

/// <summary>The codes of the gaps of HTML to Word; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class HtmlToWordGapCodes
{
    public static readonly GapCode HtmlToWordLinkedStyleSheetsNotRead = new(nameof(HtmlToWordLinkedStyleSheetsNotRead));
    public static readonly GapCode HtmlToWordImagesNotDataUris = new(nameof(HtmlToWordImagesNotDataUris));
    public static readonly GapCode HtmlToWordUnrecognisedImageSkipped = new(nameof(HtmlToWordUnrecognisedImageSkipped));
}
