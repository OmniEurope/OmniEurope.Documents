// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;

namespace OmniEurope.Documents.StressTests;

/// <summary>
/// Deterministic damage for robustness runs: the same seed gives the same broken files, so a failure
/// replays exactly. Every variant is a valid byte array; most are no longer valid files.
/// </summary>
internal static class Damage
{
    /// <summary>Bytes overwritten, runs zeroed and the file cut short, <paramref name="count"/> variants.</summary>
    public static IEnumerable<byte[]> Bytes(byte[] original, int count, int seed)
    {
        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            var copy = (byte[])original.Clone();
            switch (i % 3)
            {
                case 0:
                    for (var k = random.Next(1, 9); k > 0; k--)
                    {
                        copy[random.Next(copy.Length)] = (byte)random.Next(256);
                    }

                    yield return copy;
                    break;
                case 1:
                    var at = random.Next(copy.Length);
                    copy.AsSpan(at, Math.Min(copy.Length - at, random.Next(4, 65))).Clear();
                    yield return copy;
                    break;
                default:
                    yield return copy[..random.Next(copy.Length)];
                    break;
            }
        }
    }

    /// <summary>
    /// For each part of a ZIP package: the part removed, its content cut at random points and with random
    /// bytes overwritten, each variant written back as a sound archive so the damage reaches the parsers.
    /// </summary>
    public static IEnumerable<byte[]> Package(byte[] original, int perPart, int seed)
    {
        var random = new Random(seed);
        var parts = Read(original);
        foreach (var name in parts.Keys)
        {
            yield return Write(parts.Where(p => p.Key != name));
            for (var i = 0; i < perPart; i++)
            {
                var content = (byte[])parts[name].Clone();
                if (content.Length == 0)
                {
                    break;
                }

                if (i % 2 == 0)
                {
                    content = content[..random.Next(content.Length)];
                }
                else
                {
                    for (var k = random.Next(1, 5); k > 0; k--)
                    {
                        content[random.Next(content.Length)] = (byte)"<>\"'=/&;: x0-9#"[random.Next(15)];
                    }
                }

                yield return Write(parts.Select(p => p.Key == name ? KeyValuePair.Create(name, content) : p));
            }
        }
    }

    /// <summary>Random text drawn from characters that matter to markup parsers.</summary>
    public static IEnumerable<string> Text(string alphabet, int count, int maxLength, int seed)
    {
        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            var builder = new StringBuilder();
            for (var n = random.Next(maxLength); n > 0; n--)
            {
                builder.Append(alphabet[random.Next(alphabet.Length)]);
            }

            yield return builder.ToString();
        }
    }

    private static Dictionary<string, byte[]> Read(byte[] package)
    {
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            parts[entry.FullName] = buffer.ToArray();
        }

        return parts;
    }

    private static byte[] Write(IEnumerable<KeyValuePair<string, byte[]>> parts)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(content);
            }
        }

        return output.ToArray();
    }
}
