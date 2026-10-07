// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>
/// The decoders read fields where the file says they are; in a truncated or corrupted file that place can lie
/// past the end of the data, and the read fails deep inside with an index error. At the public entry point
/// such a failure is reported for what it is: damaged data.
/// </summary>
internal static class DamagedData
{
    /// <summary>True for a read past the end of the data.</summary>
    public static bool IsOverrun(Exception exception) =>
        exception is IndexOutOfRangeException or ArgumentOutOfRangeException or OverflowException;

    /// <summary>The exception reported to the caller.</summary>
    public static InvalidDataException Error(string format, Exception overrun) =>
        new($"Damaged {format} data: a field points outside the file.", overrun);
}
