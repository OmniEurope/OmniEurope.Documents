// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf;

/// <summary>
/// What a user who opens an encrypted PDF with the user password may do (the P entry, ISO 32000-2 §7.6.4.2 table
/// 22). The owner password grants everything. These flags ask conforming readers to restrict; they are not a
/// protection of the content, which the user password decrypts entirely.
/// </summary>
[Flags]
public enum PdfPermissions
{
    /// <summary>Nothing beyond viewing.</summary>
    None = 0,

    /// <summary>Print (at low quality unless <see cref="PrintHighQuality"/> is also granted).</summary>
    Print = 1 << 2,

    /// <summary>Modify the content (other than through the rights below).</summary>
    Modify = 1 << 3,

    /// <summary>Copy or extract text and graphics.</summary>
    Copy = 1 << 4,

    /// <summary>Add or change annotations and fill form fields.</summary>
    Annotate = 1 << 5,

    /// <summary>Fill existing form fields, even without <see cref="Annotate"/>.</summary>
    FillForms = 1 << 8,

    /// <summary>Assemble: insert, rotate or delete pages, create bookmarks and thumbnails.</summary>
    Assemble = 1 << 10,

    /// <summary>Print at full quality.</summary>
    PrintHighQuality = 1 << 11,

    /// <summary>Every permission.</summary>
    All = Print | Modify | Copy | Annotate | FillForms | Assemble | PrintHighQuality,
}

/// <summary>
/// AES-256 encryption of a written PDF (ISO 32000-2 standard security handler, revision 6): the user password
/// opens the file with <see cref="Permissions"/>, the owner password opens it with every right. Passwords are
/// prepared with SASLprep and cut to 127 UTF-8 bytes; the file key, salts and initialisation vectors come from the
/// system's cryptographic random generator, so an encrypted file is never byte-identical to another.
/// </summary>
/// <param name="UserPassword">The password that opens the file; empty opens it without asking.</param>
/// <param name="OwnerPassword">The password that grants every right; it must not be empty.</param>
public sealed record PdfEncryption(string UserPassword, string OwnerPassword)
{
    /// <summary>The rights of the user password; all by default.</summary>
    public PdfPermissions Permissions { get; init; } = PdfPermissions.All;

    /// <summary>Encrypt the XMP metadata stream too (default); false leaves it readable for indexing.</summary>
    public bool EncryptMetadata { get; init; } = true;
}
