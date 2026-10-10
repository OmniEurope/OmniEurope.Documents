// SPDX-License-Identifier: EUPL-1.2
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OmniEurope.Documents.Pdf.Signing;

/// <summary>The parts of a CMS SignedData (RFC 5652) a signature check needs, as found in the encoding.</summary>
internal sealed record CmsContent(int Length, bool Detached, IReadOnlyList<X509Certificate2> Certificates, X509Certificate2? Signer, string DigestAlgorithm,
    byte[] SignedAttributes, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Attributes, string SignatureAlgorithm, byte[] Signature);

/// <summary>Reads the first signer of a CMS SignedData with the base library's ASN.1 reader (BER, as signers may write it).</summary>
internal static class CmsReader
{
    private static readonly Asn1Tag Context0 = new(TagClass.ContextSpecific, 0, isConstructed: true);
    private static readonly Asn1Tag Context1 = new(TagClass.ContextSpecific, 1, isConstructed: true);

    /// <exception cref="AsnContentException">The bytes are not a SignedData.</exception>
    /// <exception cref="CryptographicException">A certificate cannot be read.</exception>
    public static CmsContent Read(ReadOnlyMemory<byte> data)
    {
        AsnDecoder.ReadEncodedValue(data.Span, AsnEncodingRules.BER, out _, out _, out var length);
        var contentInfo = new AsnReader(data[..length], AsnEncodingRules.BER).ReadSequence();
        if (contentInfo.ReadObjectIdentifier() != CmsSignature.IdSignedData)
        {
            throw new AsnContentException("The CMS content is not SignedData.");
        }

        var signedData = contentInfo.ReadSequence(Context0).ReadSequence();
        signedData.ReadInteger();
        signedData.ReadSetOf();
        var encapsulated = signedData.ReadSequence();
        encapsulated.ReadObjectIdentifier();
        var detached = !encapsulated.HasData;
        var certificates = new List<X509Certificate2>();
        if (signedData.PeekTag().HasSameClassAndValue(Context0))
        {
            var set = signedData.ReadSetOf(skipSortOrderValidation: true, expectedTag: Context0);
            while (set.HasData)
            {
                certificates.Add(X509CertificateLoader.LoadCertificate(set.ReadEncodedValue().Span));
            }
        }

        if (signedData.HasData && signedData.PeekTag().HasSameClassAndValue(Context1))
        {
            signedData.ReadEncodedValue();
        }

        var signerInfo = signedData.ReadSetOf(skipSortOrderValidation: true).ReadSequence();
        signerInfo.ReadInteger();
        var signer = Signer(signerInfo, certificates);
        var digestAlgorithm = signerInfo.ReadSequence().ReadObjectIdentifier();
        byte[] signedAttributes = signerInfo.PeekTag().HasSameClassAndValue(Context0) ? [.. signerInfo.ReadEncodedValue().Span] : [];
        var attributes = Attributes(signedAttributes);
        var signatureAlgorithm = signerInfo.ReadSequence().ReadObjectIdentifier();
        var signature = signerInfo.ReadOctetString();
        return new CmsContent(length, detached, certificates, signer, digestAlgorithm, signedAttributes, attributes, signatureAlgorithm, signature);
    }

    // The signer identifier: issuer and serial number, or (version 3) a subject key identifier.
    private static X509Certificate2? Signer(AsnReader signerInfo, List<X509Certificate2> certificates)
    {
        if (signerInfo.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence))
        {
            var sid = signerInfo.ReadSequence();
            var issuer = sid.ReadEncodedValue().ToArray();
            var serial = sid.ReadIntegerBytes().ToArray();
            return certificates.Find(c => c.IssuerName.RawData.AsSpan().SequenceEqual(issuer) && c.SerialNumberBytes.Span.SequenceEqual(serial));
        }

        var keyIdentifier = Convert.ToHexString(signerInfo.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0)));
        return certificates.Find(c => c.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Any(e => string.Equals(e.SubjectKeyIdentifier, keyIdentifier, StringComparison.OrdinalIgnoreCase)));
    }

    // The signed attributes by type, each with its first value; the set is retagged as the SET OF that was signed.
    private static Dictionary<string, ReadOnlyMemory<byte>> Attributes(byte[] signedAttributes)
    {
        var attributes = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        if (signedAttributes.Length == 0)
        {
            return attributes;
        }

        signedAttributes[0] = 0x31;
        var set = new AsnReader(signedAttributes, AsnEncodingRules.BER).ReadSetOf(skipSortOrderValidation: true);
        while (set.HasData)
        {
            var attribute = set.ReadSequence();
            var type = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf(skipSortOrderValidation: true);
            attributes.TryAdd(type, values.ReadEncodedValue());
        }

        return attributes;
    }
}
