using ApricotFramework.Mailer;

namespace ApricotFramework.Mailer.Tests;

public class EmailModelTests
{
    [Fact]
    public void FromHtml_Always_LeavesThePlainTextPartAbsent()
    {
        var body = EmailBody.FromHtml("<b>hi</b>");

        Assert.Equal("<b>hi</b>", body.Html);
        Assert.Null(body.Text);
        Assert.False(body.IsEmpty());
    }

    [Fact]
    public void FromBoth_Always_KeepsBothParts()
    {
        var body = EmailBody.FromBoth("<b>hi</b>", "hi");

        Assert.Equal("<b>hi</b>", body.Html);
        Assert.Equal("hi", body.Text);
    }

    [Fact]
    public void FromText_EmptyString_IsStillAPart()
    {
        // an empty part is a part; absent is what null means
        Assert.False(EmailBody.FromText(string.Empty).IsEmpty());
    }

    [Fact]
    public void FromBytes_CalledTwice_ProducesAFreshReadableStreamEachTime()
    {
        // a send may open an attachment more than once: once to measure it, once to write it
        var attachment = EmailAttachment.FromBytes("note.txt", "hello"u8.ToArray(), "text/plain");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var payload = attachment.OpenRead();
            using var reader = new StreamReader(payload);

            Assert.Equal("hello", reader.ReadToEnd());
        }
    }

    [Fact]
    public void FromBytes_CallerMutatesTheBufferAfterwards_DoesNotChangeThePayload()
    {
        var buffer = new byte[] { 1, 2, 3 };
        var attachment = EmailAttachment.FromBytes("note.bin", buffer);

        buffer[0] = 99;

        using var payload = attachment.OpenRead();

        Assert.Equal(1, payload.ReadByte());
    }

    [Fact]
    public void FromBytes_NoContentType_UsesTheDefault()
    {
        Assert.Equal(EmailAttachment.DefaultContentType, EmailAttachment.FromBytes("note.bin", new byte[1]).ContentType);
    }

    [Fact]
    public void Inline_Always_MarksThePartAsBelongingToTheBody()
    {
        var attachment = EmailAttachment.Inline("header-logo", "logo.png", new byte[1], "image/png");

        Assert.True(attachment.IsInline);
        Assert.Equal("header-logo", attachment.ContentId);
    }

    [Theory]
    [InlineData("note\r\n.txt")]
    [InlineData("note\0.txt")]
    public void FromBytes_FileNameCarryingAControlCharacter_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => EmailAttachment.FromBytes(fileName, new byte[1]));
    }

    [Fact]
    public void Inline_ContentIdCarryingWhiteSpace_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmailAttachment.Inline("header logo", "logo.png", new byte[1]));
    }

    [Fact]
    public void FromFile_MissingFile_FailsOnlyWhenThePayloadIsOpened()
    {
        // the path is read at send time, so a file that appears later still works
        var attachment = EmailAttachment.FromFile("/no/such/file.txt");

        Assert.Equal("file.txt", attachment.FileName);
        Assert.Throws<DirectoryNotFoundException>(() => attachment.OpenRead());
    }

    [Fact]
    public void GetAllRecipients_Always_ReadsToThenCcThenBcc()
    {
        var message = SampleMail.Message() with
        {
            Cc = [EmailAddress.Parse("cc@example.com")],
            Bcc = [EmailAddress.Parse("bcc@example.com")],
        };

        Assert.Equal(
            ["recipient@example.com", "cc@example.com", "bcc@example.com"],
            message.GetAllRecipients().Select(address => address.Address));
    }

    [Fact]
    public void IsWritableHeader_ReservedName_IsRefusedWhateverTheCase()
    {
        Assert.False(EmailMessageValidation.IsWritableHeader("bCc"));
        Assert.False(EmailMessageValidation.IsWritableHeader("Return-Path"));
        Assert.False(EmailMessageValidation.IsWritableHeader("Resent-To"));
    }

    [Fact]
    public void IsWritableHeader_CustomName_IsAllowed()
    {
        Assert.True(EmailMessageValidation.IsWritableHeader("X-Campaign"));
    }
}
