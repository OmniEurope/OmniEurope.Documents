// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>The encryption schemes of the standard security handler a test can ask for.</summary>
public enum PdfCipher
{
    /// <summary>Revision 2: RC4 with a 40-bit key.</summary>
    Rc4Bits40,

    /// <summary>Revision 3: RC4 with a 128-bit key.</summary>
    Rc4Bits128,

    /// <summary>Revision 4: AES-128 through the StdCF crypt filter, metadata left in clear.</summary>
    Aes128,

    /// <summary>Revision 6: AES-256 (ISO 32000-2).</summary>
    Aes256,

    /// <summary>Revision 5: AES-256 with a single SHA-256 password hash (Adobe extension level 3, deprecated).</summary>
    Aes256Revision5,

    /// <summary>Revision 4: AES-128 for streams, strings left in clear (StrF /Identity).</summary>
    Aes128ClearStrings,
}

/// <summary>
/// Writes a one-page PDF encrypted by the standard security handler, written from ISO 32000-1 §7.6.3
/// (algorithms 1 to 5), ISO 32000-2 §7.6.4.3 (algorithms 2.B, 8, 9 and 10) and, for revision 5, Adobe's extension level 3
/// (the same entries hashed by one SHA-256). The page shows "Secret"
/// in Helvetica, the Info dictionary carries an encrypted title and the XMP metadata stream is left in
/// clear when the cipher says so. Self-contained, so a probe outside the test project can link it.
/// </summary>
internal static class EncryptedPdf
{
    public const string Title = "Confidential report";

    public const string Content = "BT /F1 36 Tf 20 100 Td (Secret) Tj ET 1 0 0 rg 20 20 60 40 re f";

    private static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    private static readonly byte[] FileId = Convert.FromHexString("0123456789ABCDEF0F1E2D3C4B5A6978");

    // Printing allowed, the high-order bits set as the specification requires.
    private const int Permissions = unchecked((int)0xFFFFF0C4);

    public static byte[] Create(PdfCipher cipher, string userPassword, string ownerPassword)
    {
        var (fileKey, encrypt) = cipher is PdfCipher.Aes256 or PdfCipher.Aes256Revision5
            ? Revision5Or6(userPassword, ownerPassword, cipher == PdfCipher.Aes256 ? 6 : 5)
            : Revision2To4(cipher, userPassword, ownerPassword);
        var metadataInClear = cipher is PdfCipher.Aes128 or PdfCipher.Aes128ClearStrings;
        byte[] Text(string value, int number) => Hex(cipher == PdfCipher.Aes128ClearStrings ? Encoding.Latin1.GetBytes(value) : Encrypt(cipher, fileKey, number, Encoding.Latin1.GetBytes(value)));
        byte[] Data(byte[] value, int number) => Encrypt(cipher, fileKey, number, value);

        var xmp = Ascii("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>");
        var objects = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R /Metadata 7 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"),
            Stream(string.Empty, Data(Ascii(Content), 4)),
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            Ascii("<< /Title ").Concat(Text(Title, 6)).Concat(Ascii(" >>")).ToArray(),
            Stream("/Type /Metadata /Subtype /XML", metadataInClear ? xmp : Data(xmp, 7)),
            encrypt,
        };

        return Build(objects);
    }

    private static (byte[] Key, byte[] Dictionary) Revision2To4(PdfCipher cipher, string userPassword, string ownerPassword)
    {
        var (revision, length) = cipher switch
        {
            PdfCipher.Rc4Bits40 => (2, 5),
            PdfCipher.Rc4Bits128 => (3, 16),
            _ => (4, 16),
        };
        var encryptMetadata = cipher is not (PdfCipher.Aes128 or PdfCipher.Aes128ClearStrings);

        // Algorithm 3: the O entry.
        var ownerHash = MD5.HashData(Pad(ownerPassword.Length > 0 ? ownerPassword : userPassword));
        for (var i = 0; revision >= 3 && i < 50; i++)
        {
            ownerHash = MD5.HashData(ownerHash);
        }

        var ownerKey = ownerHash[..length];
        var o = Rc4(ownerKey, Pad(userPassword));
        for (var i = 1; revision >= 3 && i <= 19; i++)
        {
            o = Rc4(Xor(ownerKey, i), o);
        }

        // Algorithm 2: the file key.
        var p = BitConverter.GetBytes(Permissions);
        byte[] input = [.. Pad(userPassword), .. o, .. p, .. FileId, .. encryptMetadata ? Array.Empty<byte>() : [0xFF, 0xFF, 0xFF, 0xFF]];
        var key = MD5.HashData(input)[..length];
        for (var i = 0; revision >= 3 && i < 50; i++)
        {
            key = MD5.HashData(key)[..length];
        }

        // Algorithms 4 and 5: the U entry.
        byte[] u;
        if (revision == 2)
        {
            u = Rc4(key, Padding);
        }
        else
        {
            var check = Rc4(key, MD5.HashData([.. Padding, .. FileId]));
            for (var i = 1; i <= 19; i++)
            {
                check = Rc4(Xor(key, i), check);
            }

            u = [.. check, .. new byte[16]];
        }

        var filters = revision == 4
            ? "/CF << /StdCF << /CFM /AESV2 /AuthEvent /DocOpen /Length 16 >> >> /StmF /StdCF /StrF " + (cipher == PdfCipher.Aes128ClearStrings ? "/Identity" : "/StdCF") + " /EncryptMetadata false "
            : string.Empty;
        var version = revision switch { 2 => 1, 3 => 2, _ => 4 };
        var dictionary = $"<< /Filter /Standard /V {version} /R {revision} /Length {length * 8} {filters}/O <{Convert.ToHexString(o)}> /U <{Convert.ToHexString(u)}> /P {Permissions} >>";
        return (key, Ascii(dictionary));
    }

    private static (byte[] Key, byte[] Dictionary) Revision5Or6(string userPassword, string ownerPassword, int revision)
    {
        var fileKey = RandomNumberGenerator.GetBytes(32);
        var user = Utf8(userPassword);
        var owner = Utf8(ownerPassword);

        // Algorithm 8: U and UE.
        var userValidation = RandomNumberGenerator.GetBytes(8);
        var userKeySalt = RandomNumberGenerator.GetBytes(8);
        byte[] Hash(byte[] password, byte[] salt, byte[] userKey) => revision == 6 ? HashB(password, salt, userKey) : SHA256.HashData([.. password, .. salt, .. userKey]);
        byte[] u = [.. Hash(user, userValidation, []), .. userValidation, .. userKeySalt];
        var ue = AesNoPadding(Hash(user, userKeySalt, []), fileKey);

        // Algorithm 9: O and OE.
        var ownerValidation = RandomNumberGenerator.GetBytes(8);
        var ownerKeySalt = RandomNumberGenerator.GetBytes(8);
        byte[] o = [.. Hash(owner, ownerValidation, u), .. ownerValidation, .. ownerKeySalt];
        var oe = AesNoPadding(Hash(owner, ownerKeySalt, u), fileKey);

        // Algorithm 10: Perms.
        byte[] perms = [.. BitConverter.GetBytes(Permissions), 0xFF, 0xFF, 0xFF, 0xFF, (byte)'T', (byte)'a', (byte)'d', (byte)'b', 1, 2, 3, 4];
        using var aes = Aes.Create();
        aes.Key = fileKey;
        var encryptedPerms = aes.EncryptEcb(perms, PaddingMode.None);

        var dictionary = $"<< /Filter /Standard /V 5 /R {revision} /Length 256 /CF << /StdCF << /CFM /AESV3 /AuthEvent /DocOpen /Length 32 >> >> /StmF /StdCF /StrF /StdCF "
            + $"/O <{Convert.ToHexString(o)}> /U <{Convert.ToHexString(u)}> /OE <{Convert.ToHexString(oe)}> /UE <{Convert.ToHexString(ue)}> "
            + $"/Perms <{Convert.ToHexString(encryptedPerms)}> /P {Permissions} >>";
        return (fileKey, Ascii(dictionary));
    }

    // ISO 32000-2 algorithm 2.B: at least 64 rounds, then until the last byte of E is at most the round count minus 32.
    private static byte[] HashB(byte[] password, byte[] salt, byte[] userKey)
    {
        var k = SHA256.HashData([.. password, .. salt, .. userKey]);
        using var aes = Aes.Create();
        var rounds = 0;
        byte[] e;
        do
        {
            var single = password.Concat(k).Concat(userKey).ToArray();
            var k1 = Enumerable.Repeat(single, 64).SelectMany(b => b).ToArray();
            aes.Key = k[..16];
            e = aes.EncryptCbc(k1, k[16..32], PaddingMode.None);
            var remainder = new System.Numerics.BigInteger(e[..16], isUnsigned: true, isBigEndian: true) % 3;
            k = (int)remainder switch
            {
                0 => SHA256.HashData(e),
                1 => SHA384.HashData(e),
                _ => SHA512.HashData(e),
            };
            rounds++;
        }
        while (rounds < 64 || e[^1] > rounds - 32);

        return k[..32];
    }

    private static byte[] Encrypt(PdfCipher cipher, byte[] fileKey, int number, byte[] data)
    {
        if (cipher is PdfCipher.Aes256 or PdfCipher.Aes256Revision5)
        {
            return AesWithIv(fileKey, data);
        }

        var aes = cipher is PdfCipher.Aes128 or PdfCipher.Aes128ClearStrings;
        byte[] material = [.. fileKey, (byte)number, (byte)(number >> 8), (byte)(number >> 16), 0, 0, .. aes ? "sAlT"u8.ToArray() : []];
        var objectKey = MD5.HashData(material)[..Math.Min(fileKey.Length + 5, 16)];
        return aes ? AesWithIv(objectKey, data) : Rc4(objectKey, data);
    }

    private static byte[] AesWithIv(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = RandomNumberGenerator.GetBytes(16);
        return [.. iv, .. aes.EncryptCbc(data, iv, PaddingMode.PKCS7)];
    }

    private static byte[] AesNoPadding(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(data, new byte[16], PaddingMode.None);
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var state = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + state[i] + key[i % key.Length]) % 256;
            (state[i], state[j]) = (state[j], state[i]);
        }

        var result = new byte[data.Length];
        int x = 0, y = 0;
        for (var n = 0; n < data.Length; n++)
        {
            x = (x + 1) % 256;
            y = (y + state[x]) % 256;
            (state[x], state[y]) = (state[y], state[x]);
            result[n] = (byte)(data[n] ^ state[(state[x] + state[y]) % 256]);
        }

        return result;
    }

    private static byte[] Xor(byte[] key, int value) => key.Select(b => (byte)(b ^ value)).ToArray();

    private static byte[] Pad(string password)
    {
        var bytes = Encoding.Latin1.GetBytes(password);
        return [.. bytes.Take(32), .. Padding.Take(Math.Max(0, 32 - bytes.Length))];
    }

    private static byte[] Utf8(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        return bytes.Length > 127 ? bytes[..127] : bytes;
    }

    private static byte[] Hex(byte[] data) => Ascii($"<{Convert.ToHexString(data)}>");

    private static byte[] Stream(string dictionary, byte[] data) => [.. Ascii($"<< /Length {data.Length} {dictionary} >>\nstream\n"), .. data, .. Ascii("\nendstream")];

    private static byte[] Ascii(string text) => Encoding.Latin1.GetBytes(text);

    private static byte[] Build(List<byte[]> objects)
    {
        using var output = new MemoryStream();
        output.Write(Ascii("%PDF-1.7\n"));
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            output.Write(Ascii($"{i + 1} 0 obj\n"));
            output.Write(objects[i]);
            output.Write(Ascii("\nendobj\n"));
        }

        var xref = output.Position;
        output.Write(Ascii($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            output.Write(Ascii(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n"));
        }

        var id = Convert.ToHexString(FileId);
        output.Write(Ascii($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 6 0 R /Encrypt {objects.Count} 0 R /ID [<{id}> <{id}>] >>\nstartxref\n{xref}\n%%EOF\n"));
        return output.ToArray();
    }
}
