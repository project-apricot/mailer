using System.Text;
using ApricotFramework.Mailer;
using ApricotFramework.Mailer.Smtp.Impl;
using MimeKit;

namespace ApricotFramework.Mailer.Smtp.Tests;

public class MimeMessageFactoryTests
{
    private static EmailMessage Message()
    {
        return new EmailMessage
        {
            Subject = "Welcome",
            Body = EmailBody.FromHtml("<b>hello</b>"),
            From = EmailAddress.Parse("no-reply@example.com", "Example"),
            To = [EmailAddress.Parse("recipient@example.com", "Recipient")],
        };
    }

    private static string Serialize(MimeMessage mime)
    {
        using var stream = new MemoryStream();

        mime.WriteTo(stream);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void Create_Always_SetsFromAndLeavesSenderAlone()
    {
        // the old library set both, which makes the envelope and the header disagree and costs DMARC
        // alignment
        var mime = MimeMessageFactory.Create(Message());

        Assert.Equal("no-reply@example.com", Assert.Single(mime.From.Mailboxes).Address);
        Assert.Null(mime.Sender);
    }

    [Fact]
    public void Create_HtmlOnlyBody_ProducesNoEmptyAlternativePart()
    {
        var text = Serialize(MimeMessageFactory.Create(Message()));

        Assert.Contains("text/html", text, StringComparison.Ordinal);
        Assert.DoesNotContain("multipart/alternative", text, StringComparison.Ordinal);
        Assert.DoesNotContain("text/plain", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_TextOnlyBody_ProducesNoHtmlPart()
    {
        var message = Message() with { Body = EmailBody.FromText("hello") };

        var text = Serialize(MimeMessageFactory.Create(message));

        Assert.Contains("text/plain", text, StringComparison.Ordinal);
        Assert.DoesNotContain("text/html", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_BothBodies_ProducesAnAlternative()
    {
        var message = Message() with { Body = EmailBody.FromBoth("<b>hello</b>", "hello") };

        var text = Serialize(MimeMessageFactory.Create(message));

        Assert.Contains("multipart/alternative", text, StringComparison.Ordinal);
        Assert.Contains("text/html", text, StringComparison.Ordinal);
        Assert.Contains("text/plain", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_EveryRecipientKind_IsCarried()
    {
        var message = Message() with
        {
            Cc = [EmailAddress.Parse("cc@example.com")],
            Bcc = [EmailAddress.Parse("bcc@example.com")],
            ReplyTo = [EmailAddress.Parse("replies@example.com")],
        };

        var mime = MimeMessageFactory.Create(message);

        Assert.Equal("recipient@example.com", Assert.Single(mime.To.Mailboxes).Address);
        Assert.Equal("cc@example.com", Assert.Single(mime.Cc.Mailboxes).Address);
        Assert.Equal("bcc@example.com", Assert.Single(mime.Bcc.Mailboxes).Address);
        Assert.Equal("replies@example.com", Assert.Single(mime.ReplyTo.Mailboxes).Address);
    }

    [Fact]
    public void Create_Attachment_IsListedAsAnAttachment()
    {
        var message = Message() with
        {
            Attachments = [EmailAttachment.FromBytes("note.txt", "hello"u8.ToArray(), "text/plain")],
        };

        var mime = MimeMessageFactory.Create(message);

        // a text/plain attachment comes back as a TextPart, which is why the factory sets ContentId
        // through MimeEntity rather than casting to MimePart the way the old library did
        var part = Assert.IsAssignableFrom<MimePart>(Assert.Single(mime.Attachments));

        Assert.Equal("note.txt", part.FileName);
        Assert.True(part.IsAttachment);
    }

    [Fact]
    public void Create_InlineAttachment_IsReachableByContentIdAndNotListedAsAnAttachment()
    {
        var message = Message() with
        {
            Attachments = [EmailAttachment.Inline("header-logo", "logo.png", new byte[] { 1, 2, 3 }, "image/png")],
        };

        var mime = MimeMessageFactory.Create(message);
        var text = Serialize(mime);

        Assert.Empty(mime.Attachments);
        Assert.Contains("multipart/related", text, StringComparison.Ordinal);
        Assert.Contains("header-logo", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_AttachmentWithAnUnparseableContentType_Throws()
    {
        // the transport turns this into an InvalidMessage result rather than letting it escape
        var message = Message() with
        {
            Attachments = [EmailAttachment.FromBytes("note.txt", new byte[1], "not a mime type")],
        };

        Assert.Throws<ParseException>(() => MimeMessageFactory.Create(message));
    }

    [Fact]
    public void Create_CustomHeader_IsWritten()
    {
        var message = Message() with
        {
            Headers = new Dictionary<string, string> { ["X-Campaign"] = "welcome" },
        };

        Assert.Equal("welcome", MimeMessageFactory.Create(message).Headers["X-Campaign"]);
    }

    [Fact]
    public void Create_ReservedHeaderReachingTheFactory_DoesNotAddARecipient()
    {
        // validation refuses these first; this proves the second lock holds if it is ever bypassed
        var message = Message() with
        {
            Headers = new Dictionary<string, string> { ["Bcc"] = "attacker@evil.com" },
        };

        var mime = MimeMessageFactory.Create(message);

        Assert.Empty(mime.Bcc.Mailboxes);
        Assert.Single(MimeMessageFactory.GetRecipients(message));
    }

    [Theory]
    [InlineData(EmailPriority.Highest, "High")]
    [InlineData(EmailPriority.High, "High")]
    [InlineData(EmailPriority.Normal, "Normal")]
    [InlineData(EmailPriority.Low, "Low")]
    [InlineData(EmailPriority.Lowest, "Low")]
    public void Create_Priority_IsFoldedOntoImportanceAsWell(EmailPriority priority, string expectedImportance)
    {
        // X-Priority carries all five levels; Importance is what most clients actually display
        var mime = MimeMessageFactory.Create(Message() with { Priority = priority });

        Assert.Equal(expectedImportance, mime.Importance.ToString());
        Assert.Equal(priority.ToString(), mime.XPriority.ToString());
    }

    [Fact]
    public void Create_NoPriority_WritesNoPriorityHeader()
    {
        var text = Serialize(MimeMessageFactory.Create(Message()));

        Assert.DoesNotContain("X-Priority", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Importance", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_NonAsciiSubjectAndName_AreEncodedAndSurviveARoundTrip()
    {
        var message = Message() with
        {
            Subject = "Grüße, 你好",
            To = [EmailAddress.Parse("recipient@example.com", "Zoë Ünïcode")],
        };

        using var stream = new MemoryStream();
        MimeMessageFactory.Create(message).WriteTo(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var reloaded = MimeMessage.Load(stream, TestContext.Current.CancellationToken);

        Assert.Equal("Grüße, 你好", reloaded.Subject);
        Assert.Equal("Zoë Ünïcode", Assert.Single(reloaded.To.Mailboxes).Name);
    }

    [Fact]
    public void GetRecipients_Always_ReadsToCcAndBcc()
    {
        var message = Message() with
        {
            Cc = [EmailAddress.Parse("cc@example.com")],
            Bcc = [EmailAddress.Parse("bcc@example.com")],
        };

        Assert.Equal(3, MimeMessageFactory.GetRecipients(message).Count);
    }
}
