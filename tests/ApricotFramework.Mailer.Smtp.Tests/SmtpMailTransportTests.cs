using System.Net.Sockets;
using ApricotFramework.Mailer;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace ApricotFramework.Mailer.Smtp.Tests;

public class SmtpMailTransportTests
{
    [Fact]
    public void Name_Always_IsWhatAnAccountNames()
    {
        Assert.Equal("smtp", new SmtpMailTransport().Name);
    }

    [Theory]
    [InlineData(SmtpStatusCode.ServiceNotAvailable, MailErrorCode.Throttled)]
    [InlineData(SmtpStatusCode.MailboxBusy, MailErrorCode.Throttled)]
    [InlineData(SmtpStatusCode.InsufficientStorage, MailErrorCode.Throttled)]
    [InlineData(SmtpStatusCode.MailboxUnavailable, MailErrorCode.Rejected)]
    [InlineData(SmtpStatusCode.TransactionFailed, MailErrorCode.Rejected)]
    public void Classify_CommandFailure_SeparatesComeBackLaterFromRefused(SmtpStatusCode status, MailErrorCode expected)
    {
        var exception = new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, status, "refused");

        Assert.Equal(expected, SmtpMailTransport.Classify(exception));
    }

    [Fact]
    public void Classify_AuthenticationFailure_IsAuthentication()
    {
        Assert.Equal(MailErrorCode.Authentication, SmtpMailTransport.Classify(new AuthenticationException("no")));
    }

    [Fact]
    public void Classify_NotAuthenticated_IsAuthentication()
    {
        Assert.Equal(MailErrorCode.Authentication, SmtpMailTransport.Classify(new ServiceNotAuthenticatedException("no")));
    }

    [Fact]
    public void Classify_Timeout_IsTimeout()
    {
        Assert.Equal(MailErrorCode.Timeout, SmtpMailTransport.Classify(new TimeoutException()));
    }

    [Theory]
    [InlineData(typeof(SocketException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(SmtpProtocolException))]
    public void Classify_NetworkFailure_IsConnection(Type failure)
    {
        Assert.Equal(MailErrorCode.Connection, SmtpMailTransport.Classify((Exception)Activator.CreateInstance(failure)!));
    }

    [Fact]
    public void Classify_StartTlsUnavailable_IsConnection()
    {
        // MailKit raises NotSupportedException when StartTls was required and not offered
        Assert.Equal(MailErrorCode.Connection, SmtpMailTransport.Classify(new NotSupportedException()));
    }

    [Fact]
    public void Classify_SomethingElse_IsUnknown()
    {
        Assert.Equal(MailErrorCode.Unknown, SmtpMailTransport.Classify(new InvalidOperationException()));
    }

    [Fact]
    public async Task SendAsync_AccountWithNoHost_ThrowsSoTheMailerReportsAnInvalidAccount()
    {
        var account = new MailAccount { Name = "default", Transport = "smtp" };
        var message = new EmailMessage
        {
            Subject = "s",
            Body = EmailBody.FromText("t"),
            From = EmailAddress.Parse("a@example.com"),
            To = [EmailAddress.Parse("b@example.com")],
        };

        await Assert.ThrowsAsync<MailAccountException>(() =>
            new SmtpMailTransport().SendAsync(message, account, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_UnparseableContentType_ReportsInvalidMessageWithoutConnecting()
    {
        var account = new MailAccount
        {
            Name = "default",
            Transport = "smtp",
            Settings = new MailAccountSettings(new Dictionary<string, string?> { ["Host"] = "127.0.0.1", ["Port"] = "1" }, "default"),
        };

        var message = new EmailMessage
        {
            Subject = "s",
            Body = EmailBody.FromText("t"),
            From = EmailAddress.Parse("a@example.com"),
            To = [EmailAddress.Parse("b@example.com")],
            Attachments = [EmailAttachment.FromBytes("note.txt", new byte[1], "not a mime type")],
        };

        var result = await new SmtpMailTransport().SendAsync(message, account, TestContext.Current.CancellationToken);

        Assert.Equal(MailErrorCode.InvalidMessage, result.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_NothingListeningOnThePort_ReportsConnectionRatherThanThrowing()
    {
        var account = new MailAccount
        {
            Name = "default",
            Transport = "smtp",
            Settings = new MailAccountSettings(
                new Dictionary<string, string?> { ["Host"] = "127.0.0.1", ["Port"] = "1", ["Security"] = "None", ["Timeout"] = "00:00:02" },
                "default"),
        };

        var message = new EmailMessage
        {
            Subject = "s",
            Body = EmailBody.FromText("t"),
            From = EmailAddress.Parse("a@example.com"),
            To = [EmailAddress.Parse("b@example.com")],
        };

        var result = await new SmtpMailTransport().SendAsync(message, account, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(MailErrorCode.Connection, result.ErrorCode);
        Assert.True(result.IsTransient());
    }
}
