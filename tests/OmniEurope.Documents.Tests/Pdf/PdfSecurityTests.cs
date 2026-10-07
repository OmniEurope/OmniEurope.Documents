// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfSecurityTests
{
    [Theory]
    [InlineData(PdfCipher.Rc4Bits40, "user")]
    [InlineData(PdfCipher.Rc4Bits40, "owner")]
    [InlineData(PdfCipher.Rc4Bits128, "user")]
    [InlineData(PdfCipher.Rc4Bits128, "owner")]
    [InlineData(PdfCipher.Aes128, "user")]
    [InlineData(PdfCipher.Aes128, "owner")]
    [InlineData(PdfCipher.Aes256, "user")]
    [InlineData(PdfCipher.Aes256, "owner")]
    [InlineData(PdfCipher.Aes256Revision5, "user")]
    [InlineData(PdfCipher.Aes256Revision5, "owner")]
    [InlineData(PdfCipher.Aes128ClearStrings, "user")]
    public void Opens_encrypted_files_with_the_user_or_the_owner_password(PdfCipher cipher, string password)
    {
        var document = PdfDocument.Open(EncryptedPdf.Create(cipher, "user", "owner"), password);

        Assert.True(document.IsEncrypted);
        Assert.Equal(EncryptedPdf.Title, document.Information.Title);
        Assert.Equal("Secret", document.GetPage(1).Text);
    }

    [Theory]
    [InlineData(PdfCipher.Rc4Bits40)]
    [InlineData(PdfCipher.Rc4Bits128)]
    [InlineData(PdfCipher.Aes128)]
    [InlineData(PdfCipher.Aes256)]
    public void A_wrong_password_is_refused(PdfCipher cipher)
    {
        var data = EncryptedPdf.Create(cipher, "user", "owner");

        Assert.Throws<PdfPasswordException>(() => PdfDocument.Open(data, "wrong"));
        Assert.Throws<PdfPasswordException>(() => PdfDocument.Open(data));
    }

    [Theory]
    [InlineData(PdfCipher.Rc4Bits128)]
    [InlineData(PdfCipher.Aes256)]
    public void An_empty_user_password_opens_without_asking(PdfCipher cipher)
    {
        var document = PdfDocument.Open(EncryptedPdf.Create(cipher, string.Empty, "owner"));

        Assert.Equal("Secret", document.GetPage(1).Text);
    }

    [Fact]
    public void Other_security_handlers_are_reported_as_unsupported()
    {
        var data = EncryptedPdf.Create(PdfCipher.Rc4Bits128, "user", "owner");
        var text = Encoding.Latin1.GetString(data).Replace("/Filter /Standard", "/Filter /Adobe.PubSec", StringComparison.Ordinal);

        var error = Assert.Throws<NotSupportedException>(() => PdfDocument.Open(Encoding.Latin1.GetBytes(text)));
        Assert.Contains("Adobe.PubSec", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("00112233445566778899AABBCCDDEEFF")]
    [InlineData("00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF")]
    public void Aes_strings_too_short_or_badly_padded_do_not_stop_reading(string title)
    {
        // 16 bytes hold no block after the initialisation vector; 32 bytes of noise decrypt without valid padding.
        var data = Encoding.Latin1.GetString(EncryptedPdf.Create(PdfCipher.Aes128, "user", "owner"));
        var start = data.IndexOf("/Title <", StringComparison.Ordinal) + 8;
        var end = data.IndexOf('>', start);
        var damaged = data[..start] + title.PadRight(end - start, ' ') + data[end..];

        var document = PdfDocument.Open(Encoding.Latin1.GetBytes(damaged), "user");

        Assert.NotEqual(EncryptedPdf.Title, document.Information.Title);
        Assert.Equal("Secret", document.GetPage(1).Text);
    }
}
