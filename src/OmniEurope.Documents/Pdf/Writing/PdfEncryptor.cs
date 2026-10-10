// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>
/// The writing side of the AES-256 standard security handler (ISO 32000-2 §7.6.4, V 5, R 6, crypt filter AESV3):
/// the encryption dictionary (algorithms 8, 9 and 10) and the encryption of every string and stream of the file
/// with the file key, each behind a random 16-byte initialisation vector (AES-256-CBC, PKCS#7 padding, §7.6.3.3).
/// </summary>
internal sealed class PdfEncryptor
{
    // Bits 1 and 2 clear, bits 7 and 8 and 13 to 32 set (reserved), bit 10 set (ISO 32000-2 table 22: always 1).
    private const int ReservedBits = unchecked((int)0xFFFFF2C0);

    private readonly byte[] _fileKey;
    private readonly bool _encryptMetadata;

    private PdfEncryptor(byte[] fileKey, PdfDictionary dictionary, bool encryptMetadata)
    {
        _fileKey = fileKey;
        Dictionary = dictionary;
        _encryptMetadata = encryptMetadata;
    }

    /// <summary>The encryption dictionary, written in clear and named by the trailer's Encrypt entry.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>An encryptor with a random file key and random salts.</summary>
    public static PdfEncryptor Create(PdfEncryption encryption)
    {
        ArgumentNullException.ThrowIfNull(encryption);
        return Create(encryption, RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), RandomNumberGenerator.GetBytes(16), RandomNumberGenerator.GetBytes(4));
    }

    /// <summary>An encryptor from given key material: the 32-byte file key, the user and owner salts (validation salt
    /// then key salt, 16 bytes each) and the 4 random bytes that end Perms.</summary>
    internal static PdfEncryptor Create(PdfEncryption encryption, byte[] fileKey, byte[] userSalts, byte[] ownerSalts, byte[] permsTail)
    {
        ArgumentNullException.ThrowIfNull(encryption.UserPassword);
        ArgumentException.ThrowIfNullOrEmpty(encryption.OwnerPassword);
        var user = SaslPrep.PasswordBytes(encryption.UserPassword);
        var owner = SaslPrep.PasswordBytes(encryption.OwnerPassword);

        // Algorithm 8: U is the hash of the user password with the validation salt, then both salts; UE the file key
        // encrypted with the hash of the user password and the key salt.
        byte[] u = [.. PdfPasswordHash.Compute(user, userSalts[..8], [], 6), .. userSalts];
        var ue = NoIv(PdfPasswordHash.Compute(user, userSalts[8..], [], 6), fileKey);

        // Algorithm 9: the same for the owner password, with U as the user data.
        byte[] o = [.. PdfPasswordHash.Compute(owner, ownerSalts[..8], u, 6), .. ownerSalts];
        var oe = NoIv(PdfPasswordHash.Compute(owner, ownerSalts[8..], u, 6), fileKey);

        // Algorithm 10: P (low byte first), four 0xFF, T or F for EncryptMetadata, "adb", four random bytes, in AES-256 ECB.
        var permissions = ReservedBits | (int)(encryption.Permissions & PdfPermissions.All);
        byte[] perms = [.. BitConverter.GetBytes(permissions), 0xFF, 0xFF, 0xFF, 0xFF, (byte)(encryption.EncryptMetadata ? 'T' : 'F'), (byte)'a', (byte)'d', (byte)'b', .. permsTail];
        using var aes = Aes.Create();
        aes.Key = fileKey;
        var encryptedPerms = aes.EncryptEcb(perms, PaddingMode.None);

        var filter = new PdfDictionary().SetName("Type", "CryptFilter").SetName("CFM", "AESV3").SetName("AuthEvent", "DocOpen").SetNumber("Length", 32);
        var dictionary = new PdfDictionary()
            .SetName("Filter", "Standard").SetNumber("V", 5).SetNumber("R", 6).SetNumber("Length", 256)
            .Set("CF", new PdfDictionary().Set("StdCF", filter)).SetName("StmF", "StdCF").SetName("StrF", "StdCF")
            .Set("O", new PdfString(o, hex: true)).Set("U", new PdfString(u, hex: true))
            .Set("OE", new PdfString(oe, hex: true)).Set("UE", new PdfString(ue, hex: true))
            .SetNumber("P", permissions).Set("Perms", new PdfString(encryptedPerms, hex: true));
        if (!encryption.EncryptMetadata)
        {
            dictionary.Set("EncryptMetadata", PdfBoolean.False);
        }

        return new PdfEncryptor(fileKey, dictionary, encryption.EncryptMetadata);
    }

    /// <summary>A copy of an object whose strings and stream data are encrypted (an unencrypted metadata stream keeps its data).</summary>
    public PdfObject Encrypt(PdfObject value)
    {
        switch (value)
        {
            case PdfString text:
                return new PdfString(Encrypt(text.Bytes), hex: true);
            case PdfArray array:
                return new PdfArray(array.Items.Select(Encrypt));
            case PdfStream stream:
                var clear = !_encryptMetadata && stream["Type"] is PdfName { Value: "Metadata" };
                return Entries(stream, new PdfStream(clear ? stream.Data : Encrypt(stream.Data)));
            case PdfDictionary dictionary:
                return Entries(dictionary, new PdfDictionary());
            default:
                return value;
        }
    }

    private PdfDictionary Entries(PdfDictionary source, PdfDictionary target)
    {
        foreach (var (key, entry) in source.Entries)
        {
            target.Set(key, Encrypt(entry));
        }

        return target;
    }

    private byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _fileKey;
        var iv = RandomNumberGenerator.GetBytes(16);
        return [.. iv, .. aes.EncryptCbc(data, iv, PaddingMode.PKCS7)];
    }

    private static byte[] NoIv(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(data, new byte[16], PaddingMode.None);
    }
}
