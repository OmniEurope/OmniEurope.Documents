// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Forms;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Signing;

/// <summary>
/// PAdES baseline B-B signatures (ETSI EN 319 142-1) with a certificate the application provides: an incremental
/// update adds an invisible signature field on the first page whose signature dictionary (<c>/Filter
/// /Adobe.PPKLite /SubFilter /ETSI.CAdES.detached</c>) holds a detached CMS SignedData over its byte range, the
/// whole file but the <c>Contents</c> string (ISO 32000-1 §12.8). Earlier revisions and signatures stay intact.
/// <see cref="Verify"/> checks each signature of a file: byte range, digest, CMS signature and signing certificate.
/// </summary>
public static class PdfSigner
{
    /// <summary>Signs the file with the certificate's RSA or ECDSA private key.</summary>
    /// <exception cref="ArgumentException">The certificate has no RSA or ECDSA private key, or the digest is not SHA-2.</exception>
    /// <exception cref="InvalidOperationException">The signature does not fit in <see cref="PdfSignatureOptions.ReservedSize"/>.</exception>
    /// <exception cref="NotSupportedException">An encrypted or damaged file, or a first page written as a direct object.</exception>
    public static byte[] Sign(byte[] original, X509Certificate2 certificate, PdfSignatureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(certificate);
        options ??= new PdfSignatureOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ReservedSize, 256);
        CmsSignature.Identifiers(options.DigestAlgorithm);
        if (!certificate.HasPrivateKey)
        {
            throw new ArgumentException("The certificate has no private key.", nameof(certificate));
        }

        var document = PdfForm.OpenForUpdate(original);
        var prepared = SignatureField.Prepare(document, options);
        var file = PdfIncrementalWriter.Append(original, document.Store, prepared);
        var text = Encoding.Latin1.GetString(file);
        var contents = text.IndexOf("/Contents <", original.Length, StringComparison.Ordinal) + "/Contents ".Length;
        var end = contents + (2 * options.ReservedSize) + 2;
        var range = string.Create(CultureInfo.InvariantCulture, $"[0 {contents} {end} {file.Length - end}").PadRight(SignatureField.ByteRangePlaceholder.Length - 1) + "]";
        Encoding.ASCII.GetBytes(range).CopyTo(file, text.IndexOf("/ByteRange " + SignatureField.ByteRangePlaceholder, original.Length, StringComparison.Ordinal) + "/ByteRange ".Length);
        var digest = CmsSignature.Hash(options.DigestAlgorithm, [.. file.AsSpan(0, contents), .. file.AsSpan(end)]);
        var cms = CmsSignature.Create(digest, options.DigestAlgorithm, certificate, options.ExtraCertificates);
        if (cms.Length > options.ReservedSize)
        {
            throw new InvalidOperationException($"The signature takes {cms.Length} bytes, more than the {options.ReservedSize} reserved.");
        }

        Encoding.ASCII.GetBytes(Convert.ToHexString(cms)).CopyTo(file, contents + 1);
        return file;
    }

    /// <summary>Checks every signed signature field of the file, in field order (none when it is not signed).</summary>
    /// <exception cref="InvalidDataException">The data is not a readable PDF.</exception>
    public static IReadOnlyList<PdfSignatureVerification> Verify(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        var document = PdfDocument.Open(pdf);
        return FormTree.Collect(document)
            .Where(f => f.Kind == PdfFieldKind.Signature && document.Store.Resolve(f.Value) is PdfDictionary)
            .Select(f => SignatureVerifier.Verify(pdf, document.Store, f.Name, (PdfDictionary)document.Store.Resolve(f.Value)!))
            .ToList();
    }
}
