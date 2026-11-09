// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>The OOXML Strict profile (ISO/IEC 29500 strict namespaces), which the package does not read: it is named
/// in an error instead of showing up as a workbook without worksheet.</summary>
internal static class XlsxStrictProfile
{
    public const string MainNamespace = "http://purl.oclc.org/ooxml/spreadsheetml/main";

    public static DocumentFormatException Unsupported() =>
        new("The workbook uses the OOXML Strict profile, which is not supported; save it as an Excel Workbook (Transitional) instead.");
}
