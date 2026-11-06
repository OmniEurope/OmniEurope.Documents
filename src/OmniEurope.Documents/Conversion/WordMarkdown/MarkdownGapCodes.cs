// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Conversion.WordMarkdown;

/// <summary>The codes of the gaps of Word to Markdown; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class MarkdownGapCodes
{
    public static readonly GapCode MarkdownReferencesToMissingNotesLeftOut = new(nameof(MarkdownReferencesToMissingNotesLeftOut));
    public static readonly GapCode MarkdownEndnotesAsFootnotes = new(nameof(MarkdownEndnotesAsFootnotes));
    public static readonly GapCode MarkdownHeadersFootersLeftOut = new(nameof(MarkdownHeadersFootersLeftOut));
    public static readonly GapCode MarkdownCommentsLeftOut = new(nameof(MarkdownCommentsLeftOut));
    public static readonly GapCode MarkdownTextBoxesAfterAnchor = new(nameof(MarkdownTextBoxesAfterAnchor));
    public static readonly GapCode MarkdownEmptyParagraphsLeftOut = new(nameof(MarkdownEmptyParagraphsLeftOut));
    public static readonly GapCode MarkdownListNumbersAsDigits = new(nameof(MarkdownListNumbersAsDigits));
    public static readonly GapCode MarkdownMergedCellsKeepFirstText = new(nameof(MarkdownMergedCellsKeepFirstText));
    public static readonly GapCode MarkdownNestedTablesAsText = new(nameof(MarkdownNestedTablesAsText));
    public static readonly GapCode MarkdownCellParagraphsJoined = new(nameof(MarkdownCellParagraphsJoined));
    public static readonly GapCode MarkdownCellListItemsAsParagraphs = new(nameof(MarkdownCellListItemsAsParagraphs));
    public static readonly GapCode MarkdownCellTextBoxesLeftOut = new(nameof(MarkdownCellTextBoxesLeftOut));
    public static readonly GapCode MarkdownParagraphFormattingNotPreserved = new(nameof(MarkdownParagraphFormattingNotPreserved));
    public static readonly GapCode MarkdownTabsAsTabCharacters = new(nameof(MarkdownTabsAsTabCharacters));
    public static readonly GapCode MarkdownInkLeftOut = new(nameof(MarkdownInkLeftOut));
    public static readonly GapCode MarkdownChartsLeftOut = new(nameof(MarkdownChartsLeftOut));
    public static readonly GapCode MarkdownFontsNotPreserved = new(nameof(MarkdownFontsNotPreserved));
    public static readonly GapCode MarkdownColoursNotPreserved = new(nameof(MarkdownColoursNotPreserved));
    public static readonly GapCode MarkdownUnderlineScriptsAsPlainText = new(nameof(MarkdownUnderlineScriptsAsPlainText));
    public static readonly GapCode MarkdownPageBreaksLeftOut = new(nameof(MarkdownPageBreaksLeftOut));
    public static readonly GapCode MarkdownColumnBreaksAsLineBreaks = new(nameof(MarkdownColumnBreaksAsLineBreaks));
    public static readonly GapCode MarkdownLinksOtherSchemeTextOnly = new(nameof(MarkdownLinksOtherSchemeTextOnly));
    public static readonly GapCode MarkdownBookmarkLinksTextOnly = new(nameof(MarkdownBookmarkLinksTextOnly));
    public static readonly GapCode MarkdownFloatingPicturesInTextFlow = new(nameof(MarkdownFloatingPicturesInTextFlow));
}
