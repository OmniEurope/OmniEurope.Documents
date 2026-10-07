// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Text;

/// <summary>
/// The Windows-1252 code page, built in so callers never have to register
/// <c>CodePagesEncodingProvider</c>. Bytes 0x00-0x7F and 0xA0-0xFF map to the same code points; the
/// 0x80-0x9F block maps to the typographic characters Windows assigns there (euro sign, curly quotes,
/// dashes...). The five bytes Windows leaves unassigned (0x81, 0x8D, 0x8F, 0x90, 0x9D) decode to the C1
/// control with the same value, as Windows itself does, so every byte sequence round-trips.
/// Characters with no Windows-1252 byte encode as <c>?</c>.
/// </summary>
public sealed class Windows1252Encoding : Encoding
{
    /// <summary>The shared instance.</summary>
    public static Windows1252Encoding Instance { get; } = new();

    // Code points of bytes 0x80-0x9F. The unassigned bytes keep their own value (C1 control).
    private static readonly char[] HighBlock =
    [
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    ];

    private static readonly Dictionary<char, byte> HighReverse = BuildReverse();

    private static Dictionary<char, byte> BuildReverse()
    {
        var map = new Dictionary<char, byte>(HighBlock.Length);
        for (var i = 0; i < HighBlock.Length; i++)
        {
            map[HighBlock[i]] = (byte)(0x80 + i);
        }

        return map;
    }

    /// <inheritdoc />
    public override int CodePage => 1252;

    /// <inheritdoc />
    public override string WebName => "windows-1252";

    /// <inheritdoc />
    public override string EncodingName => "Western European (Windows)";

    /// <inheritdoc />
    public override string BodyName => "iso-8859-1";

    /// <inheritdoc />
    public override string HeaderName => "windows-1252";

    /// <inheritdoc />
    public override bool IsSingleByte => true;

    /// <summary>Decodes one byte.</summary>
    public static char DecodeByte(byte value) =>
        value is >= 0x80 and <= 0x9F ? HighBlock[value - 0x80] : (char)value;

    /// <summary>Encodes one character, or returns false when Windows-1252 has no byte for it.</summary>
    public static bool TryEncodeChar(char value, out byte encoded)
    {
        if (value < 0x80 || value is >= ' ' and <= 'ÿ')
        {
            encoded = (byte)value;
            return true;
        }

        return HighReverse.TryGetValue(value, out encoded);
    }

    /// <inheritdoc />
    public override int GetByteCount(char[] chars, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, chars.Length);
        var bytes = 0;
        for (var i = index; i < index + count; i++)
        {
            // A surrogate pair is one unmappable character: a single '?'.
            if (char.IsHighSurrogate(chars[i]) && i + 1 < index + count && char.IsLowSurrogate(chars[i + 1]))
            {
                i++;
            }

            bytes++;
        }

        return bytes;
    }

    /// <inheritdoc />
    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentNullException.ThrowIfNull(bytes);
        var needed = GetByteCount(chars, charIndex, charCount);
        if (byteIndex < 0 || byteIndex + needed > bytes.Length)
        {
            throw new ArgumentException("The output buffer is too small.", nameof(bytes));
        }

        var written = 0;
        for (var i = charIndex; i < charIndex + charCount; i++)
        {
            var c = chars[i];
            if (char.IsHighSurrogate(c) && i + 1 < charIndex + charCount && char.IsLowSurrogate(chars[i + 1]))
            {
                i++;
                bytes[byteIndex + written++] = (byte)'?';
                continue;
            }

            bytes[byteIndex + written++] = TryEncodeChar(c, out var b) ? b : (byte)'?';
        }

        return written;
    }

    /// <inheritdoc />
    public override int GetCharCount(byte[] bytes, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, bytes.Length);
        return count;
    }

    /// <inheritdoc />
    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentOutOfRangeException.ThrowIfNegative(byteIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(byteIndex + byteCount, bytes.Length);
        if (charIndex < 0 || charIndex + byteCount > chars.Length)
        {
            throw new ArgumentException("The output buffer is too small.", nameof(chars));
        }

        for (var i = 0; i < byteCount; i++)
        {
            chars[charIndex + i] = DecodeByte(bytes[byteIndex + i]);
        }

        return byteCount;
    }

    /// <inheritdoc />
    public override int GetMaxByteCount(int charCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(charCount);
        return charCount + 1;
    }

    /// <inheritdoc />
    public override int GetMaxCharCount(int byteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        return byteCount;
    }

    /// <inheritdoc />
    public override byte[] GetPreamble() => [];

    /// <inheritdoc />
    public override ReadOnlySpan<byte> Preamble => default;
}
