// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using System.Text;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// AES-256 encryption at writing (ISO 32000-2, revision 6). The known-answer values below are self-derived: no
/// published revision 6 vectors were available offline, so they were computed once by the independent test-side
/// implementation of algorithms 2.B, 8, 9 and 10 (<see cref="EncryptedPdf"/>, written separately from the package
/// code) and frozen; a test checks that this implementation still gives them. The SASLprep cases are the examples
/// of RFC 4013 §3.
/// </summary>
public sealed class PdfEncryptionTests
{
    private static readonly byte[] FileKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] UserSalts = Enumerable.Range(0xA0, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] OwnerSalts = Enumerable.Range(0xB0, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] PermsTail = [0xC0, 0xC1, 0xC2, 0xC3];

    private const string HashUser = "FA0B561D5F7697D2CD87F1F87394024AB6AD55A9F0A2857A4A9C7598556404C0";
    private const string ExpectedU = "FA0B561D5F7697D2CD87F1F87394024AB6AD55A9F0A2857A4A9C7598556404C0A0A1A2A3A4A5A6A7A8A9AAABACADAEAF";
    private const string ExpectedUe = "183D7FA30D3E742C6CAD9A7C9AD60C55D04E511C2F1A71FB109BBDAE8D471ED1";
    private const string ExpectedO = "EA4A86A44030EE149C1B8CB776B5B92056A63E68E61C41A224676EE88D2A23E1B0B1B2B3B4B5B6B7B8B9BABBBCBDBEBF";
    private const string ExpectedOe = "EB0C2374450B5F3D8F86DC3D16F11B41A1432427064CD74D2708A5AA57F3F65E";
    private const string ExpectedPerms = "50B245F367C0564FB9F50348E2072C88";

    private static byte[] Protected(PdfEncryption encryption, string title = "Secret report")
    {
        var builder = new PdfDocumentBuilder { Title = title, Encryption = encryption };
        builder.AddPage(300, 200).DrawText("Confidential figures", 20, 40, new PdfFont("Arial"), 14);
        return builder.ToArray();
    }

    // The test-side derivation of algorithms 8, 9 and 10 from fixed key material.
    private static (byte[] U, byte[] Ue, byte[] O, byte[] Oe, byte[] Perms) Derive(string user, string owner, int permissions)
    {
        var userBytes = Encoding.UTF8.GetBytes(user);
        var ownerBytes = Encoding.UTF8.GetBytes(owner);
        byte[] u = [.. EncryptedPdf.HashB(userBytes, UserSalts[..8], []), .. UserSalts];
        var ue = EncryptedPdf.AesNoPadding(EncryptedPdf.HashB(userBytes, UserSalts[8..], []), FileKey);
        byte[] o = [.. EncryptedPdf.HashB(ownerBytes, OwnerSalts[..8], u), .. OwnerSalts];
        var oe = EncryptedPdf.AesNoPadding(EncryptedPdf.HashB(ownerBytes, OwnerSalts[8..], u), FileKey);
        byte[] perms = [.. BitConverter.GetBytes(permissions), 0xFF, 0xFF, 0xFF, 0xFF, (byte)'T', (byte)'a', (byte)'d', (byte)'b', .. PermsTail];
        using var aes = Aes.Create();
        aes.Key = FileKey;
        return (u, ue, o, oe, aes.EncryptEcb(perms, PaddingMode.None));
    }

    private static string Hex(PdfDictionary dictionary, string key) => Convert.ToHexString(((PdfString)dictionary[key]!).Bytes);

    [Fact]
    public void The_independent_derivation_still_gives_the_frozen_values()
    {
        var (u, ue, o, oe, perms) = Derive("user", "owner", unchecked((int)0xFFFFF2D4));

        Assert.Equal(HashUser, Convert.ToHexString(EncryptedPdf.HashB("user"u8.ToArray(), UserSalts[..8], [])));
        Assert.Equal([ExpectedU, ExpectedUe, ExpectedO, ExpectedOe, ExpectedPerms], new[] { u, ue, o, oe, perms }.Select(Convert.ToHexString));
    }

    [Fact]
    public void The_encryption_dictionary_matches_the_known_answers()
    {
        var encryption = new PdfEncryption("user", "owner") { Permissions = PdfPermissions.Print | PdfPermissions.Copy };

        var dictionary = PdfEncryptor.Create(encryption, FileKey, UserSalts, OwnerSalts, PermsTail).Dictionary;

        Assert.Equal(HashUser, Convert.ToHexString(PdfPasswordHash.Compute("user"u8.ToArray(), UserSalts[..8], [], 6)));
        Assert.Equal([ExpectedU, ExpectedUe, ExpectedO, ExpectedOe, ExpectedPerms], new[] { "U", "UE", "O", "OE", "Perms" }.Select(k => Hex(dictionary, k)));
        Assert.Equal(-3372, ((PdfNumber)dictionary["P"]!).IntValue);
        Assert.Equal((5, 6, 256), (((PdfNumber)dictionary["V"]!).IntValue, ((PdfNumber)dictionary["R"]!).IntValue, ((PdfNumber)dictionary["Length"]!).IntValue));
        var filter = (PdfDictionary)((PdfDictionary)dictionary["CF"]!)["StdCF"]!;
        Assert.Equal("AESV3", ((PdfName)filter["CFM"]!).Value);
    }

    [Fact]
    public void Metadata_left_in_clear_is_recorded_in_perms()
    {
        var dictionary = PdfEncryptor.Create(new PdfEncryption("u", "o") { EncryptMetadata = false }, FileKey, UserSalts, OwnerSalts, PermsTail).Dictionary;

        using var aes = Aes.Create();
        aes.Key = FileKey;
        var perms = aes.DecryptEcb(((PdfString)dictionary["Perms"]!).Bytes, PaddingMode.None);
        Assert.Equal((byte)'F', perms[8]);
        Assert.Equal(PdfBoolean.False, dictionary["EncryptMetadata"]);
    }

    [Fact]
    public void The_reader_opens_what_is_written_with_either_password_and_reads_the_permissions_back()
    {
        var data = Protected(new PdfEncryption("user", "owner") { Permissions = PdfPermissions.Print | PdfPermissions.Copy });

        var asUser = PdfDocument.Open(data, "user");
        var asOwner = PdfDocument.Open(data, "owner");

        Assert.All(new[] { asUser, asOwner }, document =>
        {
            Assert.True(document.IsEncrypted);
            Assert.Equal("2.0", document.Version);
            Assert.Equal("Secret report", document.Information.Title);
            Assert.Contains("Confidential figures", document.GetPage(1).Text, StringComparison.Ordinal);
            Assert.Equal(PdfPermissions.Print | PdfPermissions.Copy, document.Permissions);
        });
        Assert.Equal((false, true), (asUser.OpenedAsOwner, asOwner.OpenedAsOwner));
        Assert.DoesNotContain("Secret report", Encoding.Latin1.GetString(data), StringComparison.Ordinal);
        Assert.NotEqual(data, Protected(new PdfEncryption("user", "owner")));
    }

    [Fact]
    public void A_wrong_or_missing_password_is_refused()
    {
        var data = Protected(new PdfEncryption("user", "owner"));

        Assert.Throws<PdfPasswordException>(() => PdfDocument.Open(data, "User"));
        Assert.Throws<PdfPasswordException>(() => PdfDocument.Open(data));
    }

    [Fact]
    public void An_empty_user_password_opens_without_asking_with_the_restricted_rights()
    {
        var document = PdfDocument.Open(Protected(new PdfEncryption(string.Empty, "owner") { Permissions = PdfPermissions.None }));

        Assert.Equal((PdfPermissions.None, false), (document.Permissions, document.OpenedAsOwner));
        Assert.Equal(PdfPermissions.All, PdfDocument.Open(Protected(new PdfEncryption(string.Empty, "owner"))).Permissions);
    }

    [Fact]
    public void Altered_permissions_are_detected_through_perms()
    {
        var text = Encoding.Latin1.GetString(Protected(new PdfEncryption("user", "owner") { Permissions = PdfPermissions.Print | PdfPermissions.Copy }));
        Assert.Contains("/P -3372", text, StringComparison.Ordinal);

        var altered = Encoding.Latin1.GetBytes(text.Replace("/P -3372", "/P -3368", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => PdfDocument.Open(altered, "user"));
    }

    [Theory]
    [InlineData("I\u00ADX", "IX")]
    [InlineData("user", "user")]
    [InlineData("USER", "USER")]
    [InlineData("\u00AA", "a")]
    [InlineData("\u2168", "IX")]
    [InlineData("pass\u00A0word", "pass word")]
    [InlineData("\u05D0\u05D1", "\u05D0\u05D1")]
    public void SaslPrep_maps_and_normalizes_as_rfc_4013_shows(string input, string expected) => Assert.Equal(expected, SaslPrep.Prepare(input));

    [Theory]
    [InlineData("\u0007")]
    [InlineData("\u0627\u0031")]
    [InlineData("a\uE000")]
    [InlineData("a\uFDD0")]
    [InlineData("a\uD800")]
    [InlineData("\u05D0a\u05D1")]
    public void SaslPrep_refuses_prohibited_characters_and_broken_bidirectional_text(string input) => Assert.Throws<ArgumentException>(() => SaslPrep.Prepare(input));

    [Fact]
    public void Passwords_are_prepared_and_cut_to_127_bytes()
    {
        var data = Protected(new PdfEncryption("pass\u00A0word", "\u2168" + new string('x', 200)));

        Assert.False(PdfDocument.Open(data, "pass word").OpenedAsOwner);
        Assert.True(PdfDocument.Open(data, "IX" + new string('x', 125)).OpenedAsOwner);
        Assert.Throws<ArgumentException>(() => Protected(new PdfEncryption("bad\u0007", "owner")));
        Assert.Throws<ArgumentException>(() => Protected(new PdfEncryption("user", string.Empty)));
    }

    [Fact]
    public void A_file_whose_writer_skipped_saslprep_still_opens()
    {
        var data = EncryptedPdf.Create(PdfCipher.Aes256, "pass\u00A0word", "\u0007owner");

        Assert.Equal("Secret", PdfDocument.Open(data, "pass\u00A0word").GetPage(1).Text);
        Assert.True(PdfDocument.Open(data, "\u0007owner").OpenedAsOwner);
    }

    [Fact]
    public void An_existing_document_is_encrypted_by_the_editor()
    {
        var source = PdfDocument.Open(new PdfDocumentBuilder { Title = "Plain" }.Also(b => b.AddPage().DrawText("Page text", 50, 700, PdfFont.Sans, 12)).ToArray());

        var encrypted = PdfDocument.Open(PdfEditor.Encrypt(source, new PdfEncryption("u", "o") { Permissions = PdfPermissions.Print }), "u");

        Assert.Equal(("Plain", PdfPermissions.Print), (encrypted.Information.Title, encrypted.Permissions));
        Assert.Contains("Page text", encrypted.GetPage(1).Text, StringComparison.Ordinal);
    }
}

internal static class BuilderExtensions
{
    public static PdfDocumentBuilder Also(this PdfDocumentBuilder builder, Action<PdfDocumentBuilder> action)
    {
        action(builder);
        return builder;
    }
}