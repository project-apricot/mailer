using ApricotFramework.Mailer;
using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Options;

namespace ApricotFramework.Mailer.Tests;

public class DefaultMailerTests
{
    private static DefaultMailer CreateMailer(IMailTransport transport, MailAccount? account = null, MailerOptions? options = null)
    {
        var store = new StubAccountStore(account ?? SampleMail.Account());

        return new DefaultMailer(store, [transport], options);
    }

    [Fact]
    public async Task SendAsync_UsableMessage_ReachesTheTransport()
    {
        var transport = new StubTransport();

        var result = await CreateMailer(transport).SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("stub-1", result.MessageId);
        Assert.Equal("default", result.Account);
        Assert.Equal("stub", result.Transport);
        Assert.Single(transport.Calls);
    }

    [Fact]
    public async Task SendAsync_NoAccountOfThatName_ReportsAccountNotFound()
    {
        var result = await CreateMailer(new StubTransport()).SendAsync("billing", SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(MailErrorCode.AccountNotFound, result.ErrorCode);
        Assert.False(result.IsTransient());
    }

    [Fact]
    public async Task SendAsync_AccountNamingAnUnregisteredTransport_ListsWhatIsRegistered()
    {
        var account = SampleMail.Account(transport: "sendgrid");

        var result = await CreateMailer(new StubTransport(), account).SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.TransportNotFound, result.ErrorCode);
        Assert.Contains("stub", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_NoTransportRegisteredAtAll_SaysSo()
    {
        var mailer = new DefaultMailer(new StubAccountStore(SampleMail.Account()), []);

        var result = await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.TransportNotFound, result.ErrorCode);
        Assert.Contains("no transport is registered at all", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_MessageWithNoRecipient_ReportsInvalidMessage()
    {
        var message = SampleMail.Message() with { To = [] };

        var result = await CreateMailer(new StubTransport()).SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.InvalidMessage, result.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_MessageWithNoSenderAndAnAccountWithNoDefault_ReportsInvalidMessage()
    {
        var account = SampleMail.Account() with { DefaultFrom = null };

        var result = await CreateMailer(new StubTransport(), account).SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.InvalidMessage, result.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_SubjectCarryingCrLf_ReportsInvalidMessageRatherThanSendingItMangled()
    {
        var message = SampleMail.Message() with { Subject = "Hi\r\nBcc: attacker@evil.com" };
        var transport = new StubTransport();

        var result = await CreateMailer(transport).SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.InvalidMessage, result.ErrorCode);
        Assert.Empty(transport.Calls);
    }

    [Fact]
    public async Task SendAsync_ReservedHeader_ReportsInvalidMessage()
    {
        // MimeKit merges a Bcc header into the real recipient list, so this is an extra RCPT TO
        var message = SampleMail.Message() with
        {
            Headers = new Dictionary<string, string> { ["Bcc"] = "attacker@evil.com" },
        };

        var result = await CreateMailer(new StubTransport()).SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.InvalidMessage, result.ErrorCode);
        Assert.Contains("Bcc", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_CustomHeader_IsAllowedThrough()
    {
        var message = SampleMail.Message() with
        {
            Headers = new Dictionary<string, string> { ["X-Campaign"] = "welcome" },
        };

        var result = await CreateMailer(new StubTransport()).SendAsync(message, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SendAsync_MessageWithoutASender_TakesTheAccountsWhole()
    {
        var transport = new StubTransport();

        await CreateMailer(transport).SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        var sent = transport.Calls[0].Message;

        Assert.Equal("no-reply@example.com", sent.From!.Address);
        Assert.Equal("Example", sent.From.Name);
    }

    [Fact]
    public async Task SendAsync_MessageWithItsOwnSender_KeepsIt()
    {
        var message = SampleMail.Message() with { From = EmailAddress.Parse("someone@example.com") };
        var transport = new StubTransport();

        await CreateMailer(transport).SendAsync(message, TestContext.Current.CancellationToken);

        var sent = transport.Calls[0].Message;

        // the account's display name must not be pinned onto a different mailbox
        Assert.Equal("someone@example.com", sent.From!.Address);
        Assert.Null(sent.From.Name);
    }

    [Fact]
    public async Task SendAsync_AccountWithADefaultReplyTo_AppliesItWhenTheMessageIsSilent()
    {
        var account = SampleMail.Account() with { DefaultReplyTo = EmailAddress.Parse("support@example.com") };
        var transport = new StubTransport();

        await CreateMailer(transport, account).SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal("support@example.com", Assert.Single(transport.Calls[0].Message.ReplyTo).Address);
    }

    [Fact]
    public async Task SendAsync_AccountWithAnEnvelopeSender_AppliesItWhenTheMessageIsSilent()
    {
        var account = SampleMail.Account() with { EnvelopeFrom = EmailAddress.Parse("bounces@example.com") };
        var transport = new StubTransport();

        await CreateMailer(transport, account).SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal("bounces@example.com", transport.Calls[0].Message.EnvelopeFrom!.Address);
    }

    [Fact]
    public async Task SendAsync_TransportThrowingAnAccountFailure_ReportsInvalidAccount()
    {
        var mailer = new DefaultMailer(new StubAccountStore(SampleMail.Account()), [new ThrowingTransport(new MailAccountException("Setting 'Host' is required."))]);

        var result = await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.InvalidAccount, result.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_TransportThrowingAnythingElse_ReportsUnknownRatherThanEscaping()
    {
        var mailer = new DefaultMailer(new StubAccountStore(SampleMail.Account()), [new ThrowingTransport(new InvalidOperationException("boom"))]);

        var result = await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.Unknown, result.ErrorCode);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task SendAsync_CancelledByTheCaller_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var mailer = new DefaultMailer(new StubAccountStore(SampleMail.Account()), [new ThrowingTransport(new OperationCanceledException())]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mailer.SendAsync(SampleMail.Message(), cancellation.Token));
    }

    [Fact]
    public async Task SendAsync_TransportGivingUpOnItsOwnDeadline_ReportsTimeout()
    {
        // MailKit raises this for its own timeout too, and a caller who never cancelled should not
        // see a cancellation
        var mailer = new DefaultMailer(new StubAccountStore(SampleMail.Account()), [new ThrowingTransport(new OperationCanceledException())]);

        var result = await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.Timeout, result.ErrorCode);
        Assert.True(result.IsTransient());
    }

    [Fact]
    public async Task SendAsync_NoAccountNamed_UsesTheConfiguredDefault()
    {
        var options = new MailerOptions { DefaultAccount = "billing" };
        var transport = new StubTransport();
        var mailer = new DefaultMailer(new StubAccountStore(SampleMail.Account("billing")), [transport], options);

        var result = await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("billing", result.Account);
    }

    [Fact]
    public async Task SendAsync_AttachmentOverTheConfiguredCeiling_ReportsInvalidMessage()
    {
        var options = new MailerOptions { MaxAttachmentBytes = 8 };
        var message = SampleMail.Message() with
        {
            Attachments = [EmailAttachment.FromBytes("big.bin", new byte[64])],
        };

        var result = await CreateMailer(new StubTransport(), options: options).SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.InvalidMessage, result.ErrorCode);
        Assert.Contains("big.bin", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_LargeAttachmentWithNoCeilingConfigured_IsAllowed()
    {
        var message = SampleMail.Message() with
        {
            Attachments = [EmailAttachment.FromBytes("big.bin", new byte[64])],
        };

        Assert.True((await CreateMailer(new StubTransport()).SendAsync(message, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task SendAsync_NullMessage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateMailer(new StubTransport()).SendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendAsync_BlankAccountName_Throws(string? accountName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateMailer(new StubTransport()).SendAsync(accountName!, SampleMail.Message(), TestContext.Current.CancellationToken));
    }
}
