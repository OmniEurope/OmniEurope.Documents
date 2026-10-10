// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OmniEurope.Documents.Pdf.Signing;

/// <summary>Options of <see cref="PdfSigner.Sign"/>.</summary>
public sealed record PdfSignatureOptions
{
    /// <summary>The digest algorithm: SHA-256 (default), SHA-384 or SHA-512.</summary>
    public HashAlgorithmName DigestAlgorithm { get; init; } = HashAlgorithmName.SHA256;

    /// <summary>The signing time written to the signature dictionary (<c>M</c>, PAdES puts it there rather than in
    /// the CMS); the current time by default.</summary>
    public DateTimeOffset? SigningTime { get; init; }

    /// <summary>The signer's name (<c>Name</c>), optional.</summary>
    public string? Name { get; init; }

    /// <summary>The reason for signing (<c>Reason</c>), optional.</summary>
    public string? Reason { get; init; }

    /// <summary>Where the document was signed (<c>Location</c>), optional.</summary>
    public string? Location { get; init; }

    /// <summary>How to reach the signer (<c>ContactInfo</c>), optional.</summary>
    public string? ContactInfo { get; init; }

    /// <summary>Further certificates to carry in the signature (the chain up to a trust anchor), in any order.</summary>
    public IReadOnlyList<X509Certificate2> ExtraCertificates { get; init; } = [];

    /// <summary>Bytes reserved in the file for the CMS signature. Default 16384.</summary>
    public int ReservedSize { get; init; } = 16384;
}

/// <summary>
/// What <see cref="PdfSigner.Verify"/> established for one signature field. <see cref="IsValid"/> means the signed
/// bytes are those the signer signed, with the signer's certificate; it says nothing about trusting that
/// certificate (chain, revocation and trust anchors are the application's to check).
/// </summary>
/// <param name="FieldName">The full name of the signature field.</param>
public sealed record PdfSignatureVerification(string FieldName)
{
    /// <summary>The <c>SubFilter</c> of the signature dictionary.</summary>
    public string? SubFilter { get; init; }

    /// <summary>The certificate that signed (the CMS signer's), or null when it is not found.</summary>
    public X509Certificate2? Signer { get; init; }

    /// <summary>Every certificate the signature carries.</summary>
    public IReadOnlyList<X509Certificate2> Certificates { get; init; } = [];

    /// <summary>The signing time of the signature dictionary (<c>M</c>).</summary>
    public DateTimeOffset? SigningTime { get; init; }

    /// <summary>The <c>Reason</c> of the signature dictionary.</summary>
    public string? Reason { get; init; }

    /// <summary>The byte range starts the file and leaves out exactly the <c>Contents</c> string.</summary>
    public bool ByteRangeValid { get; init; }

    /// <summary>The digest of the byte range equals the signed message digest.</summary>
    public bool DigestValid { get; init; }

    /// <summary>The signed attributes verify with the signer's public key.</summary>
    public bool SignatureValid { get; init; }

    /// <summary>The signing-certificate-v2 attribute names the signer's certificate.</summary>
    public bool SigningCertificateValid { get; init; }

    /// <summary>The byte range runs to the end of the file: nothing was appended after this signature.</summary>
    public bool CoversWholeDocument { get; init; }

    /// <summary>What does not conform, in words.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>The signature is intact: byte range, digest, signature and signing certificate check, no problem found.</summary>
    public bool IsValid => ByteRangeValid && DigestValid && SignatureValid && SigningCertificateValid && Problems.Count == 0;
}
