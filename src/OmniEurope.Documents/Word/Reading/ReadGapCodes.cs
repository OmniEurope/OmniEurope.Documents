// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Reading;

/// <summary>The codes of the gaps the reading of a Word document reports (the Excel reading shares the theme and chart
/// ones); each is a key of <c>Resources/GapMessages.resx</c>.</summary>
internal static class ReadGapCodes
{
    public static readonly GapCode ReadImagePartMissing = new(nameof(ReadImagePartMissing));
    public static readonly GapCode ReadAltChunkSkipped = new(nameof(ReadAltChunkSkipped));
    public static readonly GapCode ReadEquationsAsPlainText = new(nameof(ReadEquationsAsPlainText));
    public static readonly GapCode ReadThemeUnreadable = new(nameof(ReadThemeUnreadable));
    public static readonly GapCode ReadPictureLinkedSkipped = new(nameof(ReadPictureLinkedSkipped));
    public static readonly GapCode ReadChartLinkedSkipped = new(nameof(ReadChartLinkedSkipped));
    public static readonly GapCode ReadChartUnreadable = new(nameof(ReadChartUnreadable));
    public static readonly GapCode ReadShapeWithoutTextSkipped = new(nameof(ReadShapeWithoutTextSkipped));
    public static readonly GapCode ReadVmlShapeSkipped = new(nameof(ReadVmlShapeSkipped));
    public static readonly GapCode ReadDrawingSkipped = new(nameof(ReadDrawingSkipped));
    public static readonly GapCode ReadChartSkipped = new(nameof(ReadChartSkipped));
    public static readonly GapCode ReadChartExSkipped = new(nameof(ReadChartExSkipped));
    public static readonly GapCode ReadDiagramSkipped = new(nameof(ReadDiagramSkipped));
    public static readonly GapCode ReadShapeGroupSkipped = new(nameof(ReadShapeGroupSkipped));
    public static readonly GapCode ReadInkSkipped = new(nameof(ReadInkSkipped));
    public static readonly GapCode ReadInkPartUnreadable = new(nameof(ReadInkPartUnreadable));
}
