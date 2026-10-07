// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents;

/// <summary>Limits applied when opening an untrusted ZIP-based document (DOCX, XLSX).</summary>
public sealed class PackageLimits
{
    /// <summary>The defaults: 2,000 entries, 256 MiB for one part, 1 GiB in total, a ratio of 200.</summary>
    public static PackageLimits Default { get; } = new();

    /// <summary>Most entries the archive may hold.</summary>
    public int MaxEntries { get; init; } = 2000;

    /// <summary>Most bytes one part may inflate to.</summary>
    public long MaxPartSize { get; init; } = 256L << 20;

    /// <summary>Most bytes all parts read together may inflate to.</summary>
    public long MaxTotalSize { get; init; } = 1L << 30;

    /// <summary>Highest inflated/compressed ratio accepted for a part larger than 1 MiB.</summary>
    public int MaxCompressionRatio { get; init; } = 200;
}

/// <summary>The archive breaks a <see cref="PackageLimits"/> rule or is not a valid package.</summary>
public sealed class DocumentFormatException : IOException
{
    /// <summary>Creates the exception.</summary>
    public DocumentFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with its cause.</summary>
    public DocumentFormatException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
