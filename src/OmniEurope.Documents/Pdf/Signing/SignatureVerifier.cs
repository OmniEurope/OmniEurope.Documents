// SPDX-License-Identifier: EUPL-1.2
using System.Formats.Asn1;
using System.Security.Cryptography;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Signing;

/// <summary>
/// Checks one signature dictionary against the file bytes: the byte range (ISO 32000-1 §12.8.1: from the start of the
/// file, the gap exactly the hexadecimal <c>Contents</c> string), the CMS message digest against the digest of the
/// range, the signature of the signed attributes with the signer's key, the signing-certificate-v2 hash against the
/// signer's certificate, and what PAdES baseline B-B requires of the CMS (ETSI EN 319 142-1: detached, content-type
/// id-data, no signing-time attribute, nothing after the CMS in <c>Contents</c> but zero padding).
/// </summary>
internal static class SignatureVerifier
{
    // What PAdES baseline B-B requires of the CMS beyond its signature, each with the problem reported when it fails.
    private static readonly (Func<CmsContent, byte[], bool> Fails, string Problem)[] Rules =
    [
        ((cms, contents) => contents.AsSpan(cms.Length).ContainsAnyExcept((byte)0), "Contents holds bytes after the CMS signature"),
        ((cms, _) => !cms.Detached, "the CMS signature encapsulates its content instead of being detached"),
        ((cms, _) => !IsDataContentType(cms), "the content-type attribute is not id-data"),
        ((cms, _) => cms.Attributes.ContainsKey(CmsSignature.IdSigningTime), "a signing-time attribute, which PAdES baseline signatures leave to the M entry"),
        ((cms, _) => cms.Signer is null, "the signer's certificate is not in the signature"),
    ];

    public static PdfSignatureVerification Verify(byte[] pdf, PdfObjectStore store, string name, PdfDictionary signature)
    {
        var problems = new List<string>();
        var contents = (store.Get(signature, "Contents") as PdfString)?.Bytes ?? [];
        var range = Range(store, signature, pdf.Length);
        var result = Describe(store, signature, name, pdf, range, contents, problems);
        if (Read(contents, problems) is not { } cms)
        {
            return result with { Problems = problems };
        }

        problems.AddRange(Rules.Where(r => r.Fails(cms, contents)).Select(r => r.Problem));
        var hash = CmsSignature.HashOf(cms.DigestAlgorithm);
        return result with
        {
            Signer = cms.Signer,
            Certificates = cms.Certificates,
            DigestValid = DigestMatches(cms, pdf, range, hash),
            SignatureValid = SignatureMatches(cms, hash),
            SigningCertificateValid = SigningCertificate(cms),
            Problems = problems,
        };
    }

    // The signature dictionary's entries and the byte range checks.
    private static PdfSignatureVerification Describe(PdfObjectStore store, PdfDictionary signature, string name, byte[] pdf, int[]? range, byte[] contents, List<string> problems)
    {
        var subFilter = (store.Get(signature, "SubFilter") as PdfName)?.Value;
        if (subFilter != "ETSI.CAdES.detached")
        {
            problems.Add($"SubFilter {subFilter ?? "(none)"} is not ETSI.CAdES.detached");
        }

        var framed = range is not null && Frames(pdf, range, contents);
        if (!framed)
        {
            problems.Add("the byte range does not run from the start of the file around the Contents string alone");
        }

        return new PdfSignatureVerification(name)
        {
            SubFilter = subFilter,
            SigningTime = PdfDocument.ParseDate((store.Get(signature, "M") as PdfString)?.ToText()),
            Reason = (store.Get(signature, "Reason") as PdfString)?.ToText(),
            ByteRangeValid = framed,
            CoversWholeDocument = range is not null && range[2] + range[3] == pdf.Length,
        };
    }

    private static CmsContent? Read(byte[] contents, List<string> problems)
    {
        try
        {
            return CmsReader.Read(contents);
        }
        catch (Exception exception) when (exception is AsnContentException or CryptographicException or ArgumentException)
        {
            problems.Add("the CMS signature cannot be read: " + exception.Message);
            return null;
        }
    }

    private static bool DigestMatches(CmsContent cms, byte[] pdf, int[]? range, HashAlgorithmName? hash) =>
        range is not null && hash is { } h && Octets(cms, CmsSignature.IdMessageDigest) is { } signed
        && signed.AsSpan().SequenceEqual(CmsSignature.Hash(h, [.. pdf.AsSpan(0, range[1]), .. pdf.AsSpan(range[2], range[3])]));

    private static bool SignatureMatches(CmsContent cms, HashAlgorithmName? hash) =>
        cms.Signer is not null && hash is not null && cms.SignedAttributes.Length > 0
        && CmsSignature.Verify(cms.SignedAttributes, cms.Signature, cms.SignatureAlgorithm, hash.Value, cms.Signer);

    private static bool IsDataContentType(CmsContent cms)
    {
        try
        {
            return cms.Attributes.TryGetValue(CmsSignature.IdContentType, out var type) && AsnDecoder.ReadObjectIdentifier(type.Span, AsnEncodingRules.BER, out _) == CmsSignature.IdData;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    // The four numbers of ByteRange, each within the file, or null.
    private static int[]? Range(PdfObjectStore store, PdfDictionary signature, int length)
    {
        var values = store.Get<PdfArray>(signature, "ByteRange")?.Items.Select(store.Resolve).OfType<PdfNumber>().Select(n => n.Value).ToArray() ?? [];
        return values.Length == 4 && values.All(v => v >= 0 && v == Math.Floor(v)) && values[0] + values[1] <= values[2] && values[2] + values[3] <= length
            ? [.. values.Select(v => (int)v)]
            : null;
    }

    // The range starts the file and its gap is the hexadecimal string <...> of exactly the Contents bytes.
    private static bool Frames(byte[] pdf, int[] range, byte[] contents)
    {
        var (start, end) = (range[1], range[2]);
        if (range[0] != 0 || end - start != (2 * contents.Length) + 2 || pdf[start] != (byte)'<' || pdf[end - 1] != (byte)'>')
        {
            return false;
        }

        try
        {
            return Convert.FromHexString(System.Text.Encoding.Latin1.GetString(pdf, start + 1, end - start - 2)).AsSpan().SequenceEqual(contents);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[]? Octets(CmsContent cms, string attribute)
    {
        try
        {
            return cms.Attributes.TryGetValue(attribute, out var value) ? AsnDecoder.ReadOctetString(value.Span, AsnEncodingRules.BER, out _) : null;
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    // ESSCertIDv2 (RFC 5035): the first certificate hash, by its algorithm (SHA-256 when absent), names the signer.
    private static bool SigningCertificate(CmsContent cms)
    {
        if (cms.Signer is null || !cms.Attributes.TryGetValue(CmsSignature.IdSigningCertificateV2, out var value))
        {
            return false;
        }

        try
        {
            var id = new AsnReader(value, AsnEncodingRules.BER).ReadSequence().ReadSequence().ReadSequence();
            var algorithm = id.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence) ? id.ReadSequence().ReadObjectIdentifier() : "2.16.840.1.101.3.4.2.1";
            return CmsSignature.HashOf(algorithm) is { } hash && id.ReadOctetString().AsSpan().SequenceEqual(CmsSignature.Hash(hash, cms.Signer.RawData));
        }
        catch (AsnContentException)
        {
            return false;
        }
    }
}
