// SPDX-License-Identifier: EUPL-1.2
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OmniEurope.Documents.Pdf.Signing;

/// <summary>
/// A detached CMS SignedData (RFC 5652) as PAdES baseline B-B wants it (ETSI EN 319 142-1, ETSI EN 319 122-1): one
/// signer identified by issuer and serial number, signed attributes content-type (id-data), message-digest and
/// signing-certificate-v2 (RFC 5035), no signing-time (the PDF's <c>M</c> carries it), the signer's certificate and
/// any extra ones in <c>certificates</c>, RSA PKCS #1 v1.5 or ECDSA. Encoded in DER with the base library's ASN.1 writer.
/// </summary>
internal static class CmsSignature
{
    public const string IdData = "1.2.840.113549.1.7.1";
    public const string IdSignedData = "1.2.840.113549.1.7.2";
    public const string IdContentType = "1.2.840.113549.1.9.3";
    public const string IdMessageDigest = "1.2.840.113549.1.9.4";
    public const string IdSigningTime = "1.2.840.113549.1.9.5";
    public const string IdSigningCertificateV2 = "1.2.840.113549.1.9.16.2.47";
    private const string RsaEncryption = "1.2.840.113549.1.1.1";

    // Digest, RSA with that digest, ECDSA with that digest (RFC 5754, RFC 4055, RFC 5758).
    private static readonly Dictionary<string, (string Digest, string Rsa, string Ecdsa)> Algorithms = new(StringComparer.Ordinal)
    {
        ["SHA256"] = ("2.16.840.1.101.3.4.2.1", "1.2.840.113549.1.1.11", "1.2.840.10045.4.3.2"),
        ["SHA384"] = ("2.16.840.1.101.3.4.2.2", "1.2.840.113549.1.1.12", "1.2.840.10045.4.3.3"),
        ["SHA512"] = ("2.16.840.1.101.3.4.2.3", "1.2.840.113549.1.1.13", "1.2.840.10045.4.3.4"),
    };

    /// <summary>The digest algorithm a name stands for.</summary>
    /// <exception cref="ArgumentException">Not SHA-256, SHA-384 or SHA-512.</exception>
    public static (string Digest, string Rsa, string Ecdsa) Identifiers(HashAlgorithmName hash) =>
        hash.Name is { } name && Algorithms.TryGetValue(name, out var ids) ? ids : throw new ArgumentException($"Digest {hash.Name} is not SHA-256, SHA-384 or SHA-512.", nameof(hash));

    /// <summary>The digest algorithm of an object identifier, or null.</summary>
    public static HashAlgorithmName? HashOf(string oid) =>
        Algorithms.FirstOrDefault(a => a.Value.Digest == oid || a.Value.Rsa == oid || a.Value.Ecdsa == oid).Key is { } name ? new HashAlgorithmName(name) : null;

    public static byte[] Create(byte[] digest, HashAlgorithmName hash, X509Certificate2 signer, IEnumerable<X509Certificate2> extra)
    {
        var ids = Identifiers(hash);
        var attributes = SignedAttributes(digest, hash, signer);
        var (signatureAlgorithm, signature, rsa) = Sign(attributes, hash, signer, ids);
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(IdSignedData);
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)))
            using (writer.PushSequence())
            {
                writer.WriteInteger(1);
                using (writer.PushSetOf())
                {
                    Algorithm(writer, ids.Digest);
                }

                // Detached: the encapsulated content has its type and no content.
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(IdData);
                }

                using (writer.PushSetOf(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)))
                {
                    foreach (var certificate in extra.Prepend(signer).DistinctBy(c => c.Thumbprint))
                    {
                        writer.WriteEncodedValue(certificate.RawData);
                    }
                }

                using (writer.PushSetOf())
                {
                    SignerInfo(writer, signer, ids.Digest, attributes, (signatureAlgorithm, signature, rsa));
                }
            }
        }

        return writer.Encode();
    }

    private static void SignerInfo(AsnWriter writer, X509Certificate2 signer, string digestAlgorithm, byte[] attributes, (string Algorithm, byte[] Value, bool Rsa) signature)
    {
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            IssuerAndSerial(writer, signer);
            Algorithm(writer, digestAlgorithm);

            // The attributes are signed as a SET OF and carried as [0] IMPLICIT: same bytes, other first byte.
            byte[] tagged = [.. attributes];
            tagged[0] = 0xA0;
            writer.WriteEncodedValue(tagged);
            // RSA identifiers carry NULL parameters (RFC 4055), ECDSA ones none (RFC 5758).
            Algorithm(writer, signature.Algorithm, nullParameters: signature.Rsa);
            writer.WriteOctetString(signature.Value);
        }
    }

    // The DER SET OF the signed attributes (sorted by the writer, as DER requires).
    private static byte[] SignedAttributes(byte[] digest, HashAlgorithmName hash, X509Certificate2 signer)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf())
        {
            Attribute(writer, IdContentType, w => w.WriteObjectIdentifier(IdData));
            Attribute(writer, IdMessageDigest, w => w.WriteOctetString(digest));
            Attribute(writer, IdSigningCertificateV2, w =>
            {
                // SigningCertificateV2 { certs SEQUENCE OF ESSCertIDv2 { hashAlgorithm DEFAULT sha256, certHash, issuerSerial } }
                using (w.PushSequence())
                using (w.PushSequence())
                using (w.PushSequence())
                {
                    if (hash != HashAlgorithmName.SHA256)
                    {
                        Algorithm(w, Identifiers(hash).Digest);
                    }

                    w.WriteOctetString(Hash(hash, signer.RawData));
                    using (w.PushSequence())
                    {
                        using (w.PushSequence())
                        using (w.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true)))
                        {
                            w.WriteEncodedValue(signer.IssuerName.RawData);
                        }

                        w.WriteInteger(signer.SerialNumberBytes.Span);
                    }
                }
            });
        }

        return writer.Encode();
    }

    private static (string Algorithm, byte[] Signature, bool Rsa) Sign(byte[] attributes, HashAlgorithmName hash, X509Certificate2 signer, (string Digest, string Rsa, string Ecdsa) ids)
    {
        using var rsa = signer.GetRSAPrivateKey();
        if (rsa is not null)
        {
            return (ids.Rsa, rsa.SignData(attributes, hash, RSASignaturePadding.Pkcs1), true);
        }

        using var ecdsa = signer.GetECDsaPrivateKey();
        return ecdsa is not null
            ? (ids.Ecdsa, ecdsa.SignData(attributes, hash, DSASignatureFormat.Rfc3279DerSequence), false)
            : throw new ArgumentException("The certificate has no RSA or ECDSA private key.", nameof(signer));
    }

    /// <summary>Checks a signature over the signed attributes with the certificate's RSA or ECDSA public key.</summary>
    public static bool Verify(byte[] attributes, byte[] signature, string signatureAlgorithm, HashAlgorithmName hash, X509Certificate2 signer)
    {
        using var rsa = signer.GetRSAPublicKey();
        if (rsa is not null)
        {
            return (signatureAlgorithm == RsaEncryption || HashOf(signatureAlgorithm) is not null)
                && rsa.VerifyData(attributes, signature, hash, RSASignaturePadding.Pkcs1);
        }

        using var ecdsa = signer.GetECDsaPublicKey();
        return ecdsa is not null && ecdsa.VerifyData(attributes, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
    }

    public static byte[] Hash(HashAlgorithmName hash, ReadOnlySpan<byte> data) => hash.Name switch
    {
        "SHA384" => SHA384.HashData(data),
        "SHA512" => SHA512.HashData(data),
        _ => SHA256.HashData(data),
    };

    private static void IssuerAndSerial(AsnWriter writer, X509Certificate2 certificate)
    {
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(certificate.IssuerName.RawData);
            writer.WriteInteger(certificate.SerialNumberBytes.Span);
        }
    }

    private static void Attribute(AsnWriter writer, string type, Action<AsnWriter> value)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(type);
            using (writer.PushSetOf())
            {
                value(writer);
            }
        }
    }

    private static void Algorithm(AsnWriter writer, string oid, bool nullParameters = false)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
            if (nullParameters)
            {
                writer.WriteNull();
            }
        }
    }
}
