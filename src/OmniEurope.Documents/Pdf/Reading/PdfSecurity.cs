// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using System.Text;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>The PDF is encrypted and the password given (or the empty one) does not open it.</summary>
public sealed class PdfPasswordException(string message) : IOException(message);

/// <summary>
/// The standard security handler (ISO 32000-1 §7.6.3, ISO 32000-2 for AES-256): revisions 2 to 4 (RC4 40-128
/// bits, AES-128) and 5 to 6 (AES-256). Opens with the user or the owner password (empty by default) and
/// decrypts strings and streams object by object.
/// </summary>
internal sealed class PdfSecurity
{
    private static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    private readonly byte[] _key;
    private readonly string _streamMethod;
    private readonly string _stringMethod;

    private PdfSecurity(byte[] key, string streamMethod, string stringMethod, bool encryptMetadata)
    {
        _key = key;
        _streamMethod = streamMethod;
        _stringMethod = stringMethod;
        EncryptMetadata = encryptMetadata;
    }

    public bool EncryptMetadata { get; }

    public static PdfSecurity Create(PdfDictionary encrypt, byte[] firstId, string? password)
    {
        if (encrypt["Filter"] is PdfName { Value: not "Standard" } filter)
        {
            throw new NotSupportedException($"The PDF uses the '{filter.Value}' security handler, which is not supported.");
        }

        var revision = Int(encrypt, "R");
        var version = Int(encrypt, "V");
        var owner = Bytes(encrypt, "O");
        var user = Bytes(encrypt, "U");
        var permissions = Int(encrypt, "P");
        var encryptMetadata = encrypt["EncryptMetadata"] is not PdfBoolean { Value: false };
        var (streamMethod, stringMethod, lengthBits) = Methods(encrypt, version);
        var pass = password ?? string.Empty;
        byte[]? key;
        if (revision >= 5)
        {
            key = Aes256Key(pass, owner, user, Bytes(encrypt, "OE"), Bytes(encrypt, "UE"), revision);
        }
        else
        {
            var length = revision == 2 ? 5 : Math.Clamp(lengthBits / 8, 5, 16);
            key = UserKey(Pad(pass), owner, permissions, firstId, revision, length, encryptMetadata, user)
                ?? OwnerKey(pass, owner, permissions, firstId, revision, length, encryptMetadata, user);
        }

        return key is null
            ? throw new PdfPasswordException("The PDF is password protected; the password given does not open it.")
            : new PdfSecurity(key, streamMethod, stringMethod, encryptMetadata);
    }

    public byte[] DecryptString(byte[] data, int number, int generation) => Decrypt(data, number, generation, _stringMethod);

    public byte[] DecryptStream(byte[] data, int number, int generation) => Decrypt(data, number, generation, _streamMethod);

    private byte[] Decrypt(byte[] data, int number, int generation, string method)
    {
        if (method == "None" || data.Length == 0)
        {
            return data;
        }

        if (method == "AESV3")
        {
            return AesDecrypt(_key, data);
        }

        var salt = method == "AESV2" ? "sAlT"u8.ToArray() : [];
        byte[] material = [.. _key, (byte)number, (byte)(number >> 8), (byte)(number >> 16), (byte)generation, (byte)(generation >> 8), .. salt];
        var objectKey = MD5.HashData(material)[..Math.Min(_key.Length + 5, 16)];
        return method == "AESV2" ? AesDecrypt(objectKey, data) : Rc4.Apply(objectKey, data);
    }

    private static (string Stream, string String, int LengthBits) Methods(PdfDictionary encrypt, int version)
    {
        var length = encrypt["Length"] is PdfNumber n ? n.IntValue : 40;
        if (version < 4)
        {
            return ("V2", "V2", length);
        }

        string Method(string key)
        {
            var name = encrypt[key] is PdfName filter ? filter.Value : "Identity";
            if (name == "Identity")
            {
                return "None";
            }

            var cf = (encrypt["CF"] as PdfDictionary)?[name] as PdfDictionary;
            return cf?["CFM"] is PdfName cfm ? cfm.Value : "None";
        }

        var stream = Method("StmF");
        var text = Method("StrF");
        var bits = stream == "AESV3" || text == "AESV3" ? 256 : stream == "AESV2" || text == "AESV2" ? 128 : length;
        return (stream, text, bits);
    }

    private static byte[]? UserKey(byte[] padded, byte[] owner, int permissions, byte[] id, int revision, int length, bool encryptMetadata, byte[] user)
    {
        byte[] input = [.. padded, .. owner, (byte)permissions, (byte)(permissions >> 8), (byte)(permissions >> 16), (byte)(permissions >> 24), .. id];
        if (revision >= 4 && !encryptMetadata)
        {
            input = [.. input, 0xFF, 0xFF, 0xFF, 0xFF];
        }

        var key = MD5.HashData(input)[..length];
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                key = MD5.HashData(key)[..length];
            }
        }

        return CheckUser(key, id, revision, user) ? key : null;
    }

    private static bool CheckUser(byte[] key, byte[] id, int revision, byte[] user)
    {
        if (revision == 2)
        {
            return Rc4.Apply(key, Padding).AsSpan().SequenceEqual(user.AsSpan(0, Math.Min(32, user.Length)));
        }

        var value = Rc4.Apply(key, MD5.HashData([.. Padding, .. id]));
        for (var i = 1; i <= 19; i++)
        {
            value = Rc4.Apply(key.Select(b => (byte)(b ^ i)).ToArray(), value);
        }

        return user.Length >= 16 && value.AsSpan().SequenceEqual(user.AsSpan(0, 16));
    }

    // The owner password decrypts the stored user password, which then gives the key.
    private static byte[]? OwnerKey(string password, byte[] owner, int permissions, byte[] id, int revision, int length, bool encryptMetadata, byte[] user)
    {
        var hash = MD5.HashData(Pad(password));
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                hash = MD5.HashData(hash);
            }
        }

        var key = hash[..length];
        var userPassword = owner;
        if (revision == 2)
        {
            userPassword = Rc4.Apply(key, owner);
        }
        else
        {
            for (var i = 19; i >= 0; i--)
            {
                userPassword = Rc4.Apply(key.Select(b => (byte)(b ^ i)).ToArray(), userPassword);
            }
        }

        return UserKey(userPassword[..Math.Min(32, userPassword.Length)], owner, permissions, id, revision, length, encryptMetadata, user);
    }

    private static byte[]? Aes256Key(string password, byte[] owner, byte[] user, byte[] ownerKey, byte[] userKey, int revision)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        bytes = bytes[..Math.Min(bytes.Length, 127)];
        if (user.Length >= 48 && Hash(bytes, user[32..40], [], revision).AsSpan().SequenceEqual(user.AsSpan(0, 32)))
        {
            return AesNoIv(Hash(bytes, user[40..48], [], revision), userKey);
        }

        if (owner.Length >= 48 && user.Length >= 48 && Hash(bytes, owner[32..40], user[..48], revision).AsSpan().SequenceEqual(owner.AsSpan(0, 32)))
        {
            return AesNoIv(Hash(bytes, owner[40..48], user[..48], revision), ownerKey);
        }

        return null;
    }

    // Revision 5: one SHA-256; revision 6: the iterated hash of ISO 32000-2 algorithm 2.B.
    private static byte[] Hash(byte[] password, byte[] salt, byte[] extra, int revision)
    {
        var k = SHA256.HashData([.. password, .. salt, .. extra]);
        if (revision == 5)
        {
            return k;
        }

        using var aes = Aes.Create();
        for (var round = 0; ; round++)
        {
            byte[] block = [.. password, .. k, .. extra];
            var k1 = new byte[block.Length * 64];
            for (var i = 0; i < 64; i++)
            {
                block.CopyTo(k1, i * block.Length);
            }

            aes.Key = k[..16];
            var e = aes.EncryptCbc(k1, k[16..32], PaddingMode.None);
            var selector = e.Take(16).Sum(b => b) % 3;
            k = selector switch
            {
                0 => SHA256.HashData(e),
                1 => SHA384.HashData(e),
                _ => SHA512.HashData(e),
            };
            if (round >= 63 && e[^1] <= round + 1 - 32)
            {
                return k[..32];
            }
        }
    }

    private static byte[] AesNoIv(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.DecryptCbc(data, new byte[16], PaddingMode.None);
    }

    private static byte[] AesDecrypt(byte[] key, byte[] data)
    {
        if (data.Length < 32 || data.Length % 16 != 0)
        {
            return [];
        }

        using var aes = Aes.Create();
        aes.Key = key;
        try
        {
            return aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16), PaddingMode.PKCS7);
        }
        catch (CryptographicException)
        {
            return aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16), PaddingMode.None);
        }
    }

    private static byte[] Pad(string password)
    {
        var bytes = Encoding.Latin1.GetBytes(password);
        return [.. bytes.Take(32), .. Padding.Take(32 - Math.Min(32, bytes.Length))];
    }

    private static int Int(PdfDictionary dictionary, string key) => dictionary[key] is PdfNumber n ? (int)(long)n.Value : 0;

    private static byte[] Bytes(PdfDictionary dictionary, string key) => dictionary[key] is PdfString s ? s.Bytes : [];
}

/// <summary>The RC4 stream cipher (used by PDF encryption revisions 2 to 4).</summary>
internal static class Rc4
{
    public static byte[] Apply(byte[] key, byte[] data)
    {
        var s = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            s[i] = (byte)i;
        }

        for (int i = 0, j = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var output = new byte[data.Length];
        for (int k = 0, i = 0, j = 0; k < data.Length; k++)
        {
            i = (i + 1) & 0xFF;
            j = (j + s[i]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
            output[k] = (byte)(data[k] ^ s[(s[i] + s[j]) & 0xFF]);
        }

        return output;
    }
}
