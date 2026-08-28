using ApricotFramework.Mailer;
using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Options;

namespace ApricotFramework.Mailer.Tests;

/// <summary>
/// Input a caller should not be sending, each of which must produce a clean answer rather than an
/// exception or a hang.
/// </summary>
public class AdversarialInputTests
{
    private static async Task<MailSendResult> Send(EmailMessage message, MailerOptions? options = null)
    {
        var transport = new MemoryMailTransport();
        var account = SampleMail.Account(transport: MemoryMailTransport.TransportName);
        var mailer = new DefaultMailer(new StubAccountStore(account), [transport], options);

        return await mailer.SendAsync(message, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_ZeroByteAttachment_IsAccepted()
    {
        var message = SampleMail.Message() with { Attachments = [EmailAttachment.FromBytes("empty.bin", ReadOnlyMemory<byte>.Empty)] };

        Assert.True((await Send(message)).Succeeded);
    }

    [Fact]
    public async Task SendAsync_ZeroByteAttachmentUnderACeiling_IsStillAccepted()
    {
        var message = SampleMail.Message() with { Attachments = [EmailAttachment.FromBytes("empty.bin", ReadOnlyMemory<byte>.Empty)] };

        Assert.True((await Send(message, new MailerOptions { MaxAttachmentBytes = 1 })).Succeeded);
    }

    [Fact]
    public async Task SendAsync_AbsurdlyLongSubject_IsAccepted()
    {
        // long is not the same as malformed; MIME folds it
        var message = SampleMail.Message() with { Subject = new string('a', 100_000) };

        Assert.True((await Send(message)).Succeeded);
    }

    [Fact]
    public async Task SendAsync_ManyRecipients_IsAccepted()
    {
        var message = SampleMail.Message() with
        {
            To = [.. Enumerable.Range(0, 500).Select(index => EmailAddress.Parse($"person{index}@example.com"))],
        };

        Assert.True((await Send(message)).Succeeded);
    }

    [Fact]
    public async Task SendAsync_HeaderNameCarryingAColon_ReportsInvalidMessage()
    {
        var message = SampleMail.Message() with
        {
            Headers = new Dictionary<string, string> { ["X-Bad: Injected"] = "value" },
        };

        Assert.Equal(MailErrorCode.InvalidMessage, (await Send(message)).ErrorCode);
    }

    [Fact]
    public async Task SendAsync_BlankHeaderName_ReportsInvalidMessage()
    {
        var message = SampleMail.Message() with
        {
            Headers = new Dictionary<string, string> { ["  "] = "value" },
        };

        Assert.Equal(MailErrorCode.InvalidMessage, (await Send(message)).ErrorCode);
    }

    [Fact]
    public async Task SendAsync_HeaderValueCarryingCrLf_ReportsInvalidMessage()
    {
        var message = SampleMail.Message() with
        {
            Headers = new Dictionary<string, string> { ["X-Campaign"] = "welcome\r\nBcc: attacker@evil.com" },
        };

        Assert.Equal(MailErrorCode.InvalidMessage, (await Send(message)).ErrorCode);
    }

    [Fact]
    public async Task SendAsync_NonAsciiSubjectAndDisplayName_AreAccepted()
    {
        var message = SampleMail.Message() with
        {
            Subject = "Grüße, 你好, مرحبا",
            To = [EmailAddress.Parse("recipient@example.com", "Zoë 你好")],
        };

        Assert.True((await Send(message)).Succeeded);
    }

    [Fact]
    public void Settings_ValueCarryingTheKeySeparator_IsReadBackWhole()
    {
        // settings are a dictionary, not a delimited string, so nothing here can be split apart
        var settings = new MailAccountSettings(new Dictionary<string, string?> { ["Password"] = "a:b=c;d\"e'f" }, "default");

        Assert.Equal("a:b=c;d\"e'f", settings.GetString("Password"));
    }

    [Fact]
    public void ToAccount_AccountNameCarryingUnusualCharacters_IsCarriedThrough()
    {
        var entry = new MailAccountEntry { Transport = "memory" };

        Assert.Equal("tenant/42 (eu-west)", entry.ToAccount("tenant/42 (eu-west)").Name);
    }

    [Fact]
    public void ToAccount_EntryWithABlankTransport_Throws()
    {
        Assert.Throws<MailAccountException>(() => new MailAccountEntry { Transport = "   " }.ToAccount("default"));
    }
}
