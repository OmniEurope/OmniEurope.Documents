// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>
/// The password hashes of the AES-256 standard security handler: revision 6 (ISO 32000-2 §7.6.4.3.4, algorithm 2.B)
/// and the deprecated revision 5 (one SHA-256). Built only on the base library's SHA-2 and AES.
/// </summary>
internal static class PdfPasswordHash
{
    /// <summary>The 32-byte hash of a password with an 8-byte salt and the user data (the 48-byte U entry when
    /// hashing an owner password, empty otherwise).</summary>
    public static byte[] Compute(byte[] password, byte[] salt, byte[] userData, int revision)
    {
        var k = SHA256.HashData([.. password, .. salt, .. userData]);
        return revision == 5 ? k : Revision6(password, k, userData);
    }

    // Algorithm 2.B: at least 64 rounds, then rounds until the last byte of E is at most the round count minus 32.
    private static byte[] Revision6(byte[] password, byte[] k, byte[] userData)
    {
        using var aes = Aes.Create();
        for (var round = 1; ; round++)
        {
            byte[] block = [.. password, .. k, .. userData];
            var k1 = new byte[block.Length * 64];
            for (var i = 0; i < 64; i++)
            {
                block.CopyTo(k1, i * block.Length);
            }

            aes.Key = k[..16];
            var e = aes.EncryptCbc(k1, k[16..32], PaddingMode.None);

            // The first 16 bytes of E as a big-endian number modulo 3: since 256 mod 3 is 1, the sum of the bytes modulo 3.
            var sum = 0;
            for (var i = 0; i < 16; i++)
            {
                sum += e[i];
            }

            k = (sum % 3) switch
            {
                0 => SHA256.HashData(e),
                1 => SHA384.HashData(e),
                _ => SHA512.HashData(e),
            };
            if (round >= 64 && e[^1] <= round - 32)
            {
                return k[..32];
            }
        }
    }
}
