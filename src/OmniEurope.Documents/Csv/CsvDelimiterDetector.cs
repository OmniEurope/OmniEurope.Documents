// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Csv;

/// <summary>
/// Picks the delimiter of a CSV file from its first line: the candidate that occurs most often outside
/// quoted text wins, ties go to the earlier candidate, and a line with none of them yields a comma.
/// </summary>
internal static class CsvDelimiterDetector
{
    public static char Detect(ReadOnlySpan<char> start, CsvReaderOptions options)
    {
        var line = FirstLine(start, options);
        var best = ',';
        var bestCount = 0;
        foreach (var candidate in options.CandidateDelimiters)
        {
            var count = CountOutsideQuotes(line, candidate, options);
            if (count > bestCount)
            {
                best = candidate;
                bestCount = count;
            }
        }

        return best;
    }

    private static ReadOnlySpan<char> FirstLine(ReadOnlySpan<char> text, CsvReaderOptions options)
    {
        // Skip leading blank lines when the reader skips them too.
        while (options.SkipEmptyLines && text.Length > 0 && text[0] is '\r' or '\n')
        {
            text = text[1..];
        }

        var end = text.IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }

    private static int CountOutsideQuotes(ReadOnlySpan<char> line, char candidate, CsvReaderOptions options)
    {
        var quoting = options.Quoting == CsvQuoting.Rfc4180;
        var inQuotes = false;
        var count = 0;
        foreach (var c in line)
        {
            if (quoting && c == options.Quote)
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && c == candidate)
            {
                count++;
            }
        }

        return count;
    }
}
