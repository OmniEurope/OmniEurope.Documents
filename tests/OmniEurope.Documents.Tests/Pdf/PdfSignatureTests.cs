// SPDX-License-Identifier: EUPL-1.2
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Forms;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Signing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfSignatureTests
{
    private static readonly Lazy<X509Certificate2> RsaCertificate = new(() =>
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Test Signer, O=OmniEurope Tests, C=BE", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2036, 1, 1, 0, 0, 0, TimeSpan.Zero));
    });

    private static X509Certificate2 EcdsaCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new CertificateRequest("CN=EC Signer", key, HashAlgorithmName.SHA384).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static byte[] Source() => RawPdf.Page("BT /F1 12 Tf 20 100 Td (Hello signed world) Tj ET", "/Font << /F1 5 0 R >>", string.Empty,
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

    private static readonly DateTimeOffset When = new(2026, 10, 10, 14, 0, 0, TimeSpan.FromHours(2));

    private static byte[] Signed() => PdfSigner.Sign(Source(), RsaCertificate.Value, new PdfSignatureOptions { SigningTime = When, Reason = "Approval", Name = "Test Signer" });

    [Fact]
    public void A_signature_verifies_and_keeps_the_original_bytes()
    {
        var source = Source();
        var signed = Signed();

        var result = Assert.Single(PdfSigner.Verify(signed));

        Assert.Equal(source, signed[..source.Length]);
        Assert.True(result.IsValid, string.Join("; ", result.Problems));
        Assert.True(result.CoversWholeDocument);
        Assert.Equal(("Signature1", "ETSI.CAdES.detached", "Approval", When), (result.FieldName, result.SubFilter, result.Reason, result.SigningTime));
        Assert.Equal(RsaCertificate.Value.Thumbprint, result.Signer!.Thumbprint);
        Assert.Equal(PdfFieldKind.Signature, Assert.Single(PdfForm.Read(PdfDocument.Open(signed))).Kind);
        Assert.Equal("Hello signed world", PdfDocument.Open(signed).GetPage(1).Text);
    }

    // The CMS read with the base library's ASN.1 reader, independently of the package's reader: SignedData, detached,
    // the signer's certificate, signed attributes content-type, message-digest (the SHA-256 of the byte range) and
    // signing-certificate-v2 (the SHA-256 of the certificate), no signing-time, and an RSA signature that the
    // certificate's public key verifies over the DER SET OF those attributes.
    [Fact]
    public void The_cms_is_a_pades_baseline_b_detached_signed_data()
    {
        var signed = Signed();
        var (range, cms) = Parts(signed);

        var signedData = new AsnReader(cms, AsnEncodingRules.DER).ReadSequence();
        Assert.Equal("1.2.840.113549.1.7.2", signedData.ReadObjectIdentifier());
        var content = signedData.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0)).ReadSequence();
        Assert.Equal(1, (int)content.ReadInteger());
        Assert.Equal("2.16.840.1.101.3.4.2.1", content.ReadSetOf().ReadSequence().ReadObjectIdentifier());
        var encapsulated = content.ReadSequence();
        Assert.Equal("1.2.840.113549.1.7.1", encapsulated.ReadObjectIdentifier());
        Assert.False(encapsulated.HasData);
        var certificates = content.ReadSetOf(new Asn1Tag(TagClass.ContextSpecific, 0));
        Assert.Equal(RsaCertificate.Value.RawData, certificates.ReadEncodedValue().ToArray());
        var signer = content.ReadSetOf().ReadSequence();
        signer.ReadInteger();
        signer.ReadSequence();
        signer.ReadSequence();
        var attributes = signer.ReadEncodedValue().ToArray();
        attributes[0] = 0x31;
        var values = new Dictionary<string, byte[]>();
        var set = new AsnReader(attributes, AsnEncodingRules.DER).ReadSetOf();
        while (set.HasData)
        {
            var attribute = set.ReadSequence();
            values[attribute.ReadObjectIdentifier()] = attribute.ReadSetOf().ReadEncodedValue().ToArray();
        }

        Assert.Equal(["1.2.840.113549.1.9.16.2.47", "1.2.840.113549.1.9.3", "1.2.840.113549.1.9.4"], values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(SHA256.HashData(range), AsnDecoder.ReadOctetString(values["1.2.840.113549.1.9.4"], AsnEncodingRules.DER, out _));
        var essCertId = new AsnReader(values["1.2.840.113549.1.9.16.2.47"], AsnEncodingRules.DER).ReadSequence().ReadSequence().ReadSequence();
        Assert.Equal(SHA256.HashData(RsaCertificate.Value.RawData), essCertId.ReadOctetString());
        Assert.Equal("1.2.840.113549.1.1.11", signer.ReadSequence().ReadObjectIdentifier());
        using var key = RsaCertificate.Value.GetRSAPublicKey()!;
        Assert.True(key.VerifyData(attributes, signer.ReadOctetString(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void An_ecdsa_signature_with_sha384_verifies()
    {
        using var certificate = EcdsaCertificate();

        var result = Assert.Single(PdfSigner.Verify(PdfSigner.Sign(Source(), certificate, new PdfSignatureOptions { DigestAlgorithm = HashAlgorithmName.SHA384 })));

        Assert.True(result.IsValid, string.Join("; ", result.Problems));
    }

    [Fact]
    public void A_byte_changed_inside_the_byte_range_breaks_the_digest()
    {
        var signed = Signed();
        var at = Encoding.Latin1.GetString(signed).IndexOf("Hello", StringComparison.Ordinal);
        signed[at] = (byte)'J';

        var result = Assert.Single(PdfSigner.Verify(signed));

        Assert.Equal("Jello signed world", PdfDocument.Open(signed).GetPage(1).Text);
        Assert.False(result.DigestValid);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_byte_changed_in_the_signature_outside_the_byte_range_is_detected()
    {
        var signed = Signed();
        var (start, length) = ContentsHex(signed);
        var cmsLength = Parts(signed).Cms.Length;
        var inSignature = (byte[])signed.Clone();
        inSignature[start + (2 * cmsLength) - 3] = inSignature[start + (2 * cmsLength) - 3] == (byte)'0' ? (byte)'1' : (byte)'0';
        var inPadding = (byte[])signed.Clone();
        inPadding[start + length - 2] = (byte)'7';

        var signature = Assert.Single(PdfSigner.Verify(inSignature));
        var padding = Assert.Single(PdfSigner.Verify(inPadding));

        Assert.True(signature.DigestValid && signature.ByteRangeValid);
        Assert.False(signature.SignatureValid);
        Assert.False(signature.IsValid);
        Assert.True(padding.DigestValid && padding.SignatureValid);
        Assert.Contains(padding.Problems, p => p.Contains("after the CMS", StringComparison.Ordinal));
        Assert.False(padding.IsValid);
    }

    [Fact]
    public void Updates_after_signing_leave_the_signature_valid_but_not_covering()
    {
        var signed = Signed();
        var stamped = PdfStamper.StampText(signed, "Copie");
        var resigned = PdfSigner.Sign(stamped, RsaCertificate.Value);

        var first = Assert.Single(PdfSigner.Verify(stamped));
        var both = PdfSigner.Verify(resigned);

        Assert.True(first.IsValid && !first.CoversWholeDocument);
        Assert.Equal(["Signature1", "Signature2"], both.Select(s => s.FieldName));
        Assert.All(both, s => Assert.True(s.IsValid, string.Join("; ", s.Problems)));
        Assert.Equal([false, true], both.Select(s => s.CoversWholeDocument));
    }

    [Fact]
    public void Signing_is_refused_without_a_key_room_or_a_supported_digest()
    {
        using var publicOnly = X509CertificateLoader.LoadCertificate(RsaCertificate.Value.RawData);
        var builder = new OmniEurope.Documents.Pdf.Writing.PdfDocumentBuilder { Encryption = new PdfEncryption(string.Empty, "owner") };
        builder.AddPage();

        Assert.Throws<ArgumentException>(() => PdfSigner.Sign(Source(), publicOnly));
        Assert.Throws<ArgumentException>(() => PdfSigner.Sign(Source(), RsaCertificate.Value, new PdfSignatureOptions { DigestAlgorithm = HashAlgorithmName.SHA1 }));
        Assert.Throws<InvalidOperationException>(() => PdfSigner.Sign(Source(), RsaCertificate.Value, new PdfSignatureOptions { ReservedSize = 300 }));
        Assert.Throws<NotSupportedException>(() => PdfSigner.Sign(builder.ToArray(), RsaCertificate.Value));
        Assert.Empty(PdfSigner.Verify(Source()));
    }

    [Fact]
    public void A_signature_that_is_not_pades_or_cannot_be_read_is_reported()
    {
        var signed = Signed();
        var text = Encoding.Latin1.GetString(signed);
        var other = (byte[])signed.Clone();
        Encoding.ASCII.GetBytes("/SubFilter /adbe.pkcs7.detached").CopyTo(other, text.IndexOf("/SubFilter /ETSI.CAdES.detached", StringComparison.Ordinal));
        var unreadable = (byte[])signed.Clone();
        var (start, _) = ContentsHex(signed);
        Encoding.ASCII.GetBytes("FFFF").CopyTo(unreadable, start);

        var renamed = Assert.Single(PdfSigner.Verify(other));
        var broken = Assert.Single(PdfSigner.Verify(unreadable));

        Assert.Contains(renamed.Problems, p => p.StartsWith("SubFilter", StringComparison.Ordinal));
        Assert.False(renamed.DigestValid);
        Assert.Contains(broken.Problems, p => p.Contains("cannot be read", StringComparison.Ordinal));
        Assert.False(broken.IsValid);
    }

    // The bytes the byte range covers and the DER signature from Contents (its length from its own header).
    private static (byte[] Range, byte[] Cms) Parts(byte[] signed)
    {
        var (start, length) = ContentsHex(signed);
        var contents = Convert.FromHexString(Encoding.ASCII.GetString(signed, start, length));
        AsnDecoder.ReadEncodedValue(contents, AsnEncodingRules.DER, out _, out _, out var consumed);
        return ([.. signed.AsSpan(0, start - 1), .. signed.AsSpan(start + length + 1)], contents[..consumed]);
    }

    // Where the hexadecimal digits of the signature's Contents start in the file, and how many there are.
    private static (int Start, int Length) ContentsHex(byte[] signed)
    {
        var text = Encoding.Latin1.GetString(signed);
        var start = text.IndexOf("/Contents <", StringComparison.Ordinal) + "/Contents <".Length;
        return (start, text.IndexOf('>', start) - start);
    }
}
