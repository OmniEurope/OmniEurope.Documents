// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>The codes of the gaps the reading of a workbook reports itself (an unreadable theme or chart shares the
/// code of the Word reading, <c>ReadGapCodes</c>); each is a key of <c>Resources/GapMessages.resx</c>.</summary>
internal static class ExcelGapCodes
{
    public static readonly GapCode ExcelDrawingUnreadable = new(nameof(ExcelDrawingUnreadable));
}
