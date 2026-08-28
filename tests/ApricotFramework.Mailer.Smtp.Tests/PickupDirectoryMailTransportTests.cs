using ApricotFramework.Mailer;
using MimeKit;

namespace ApricotFramework.Mailer.Smtp.Tests;

public class PickupDirectoryMailTransportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"mailer-pickup-{Guid.NewGuid():n}");

    private static EmailMessage Message()
    {
        return new EmailMessage
        {
            Subject = "Welcome",
            Body = EmailBody.FromHtml("<b>hello</b>"),
            From = EmailAddress.Parse("no-reply@example.com", "Example"),
            To = [EmailAddress.Parse("recipient@example.com")],
        };
    }

    private MailAccount Account(string? path = null)
    {
        return new MailAccount
        {
            Name = "local",
            Transport = PickupDirectoryMailTransport.TransportName,
            Settings = new MailAccountSettings(new Dictionary<string, string?> { ["Directory"] = path ?? this.directory }, "local"),
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SendAsync_Always_WritesAnEmlMimeKitCanReadBack()
    {
        var result = await new PickupDirectoryMailTransport().SendAsync(Message(), this.Account(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        var file = Assert.Single(Directory.GetFiles(this.directory, "*.eml"));
        var reloaded = await MimeMessage.LoadAsync(file, TestContext.Current.CancellationToken);

        Assert.Equal("Welcome", reloaded.Subject);
        Assert.Equal("no-reply@example.com", Assert.Single(reloaded.From.Mailboxes).Address);
        Assert.Equal("recipient@example.com", Assert.Single(reloaded.To.Mailboxes).Address);
    }

    [Fact]
    public async Task SendAsync_Always_LeavesNoPartialFileBehind()
    {
        // an agent watching the directory must never read half a message
        await new PickupDirectoryMailTransport().SendAsync(Message(), this.Account(), TestContext.Current.CancellationToken);

        Assert.Empty(Directory.GetFiles(this.directory, "*.tmp"));
    }

    [Fact]
    public async Task SendAsync_SubjectCarryingAPathSeparator_StillWritesInsideTheDirectory()
    {
        // the file name is generated, never derived from message content
        var message = Message() with { Subject = "../../escaped" };

        await new PickupDirectoryMailTransport().SendAsync(message, this.Account(), TestContext.Current.CancellationToken);

        Assert.Single(Directory.GetFiles(this.directory, "*.eml"));
    }

    [Fact]
    public async Task SendAsync_DirectoryDoesNotExistYet_CreatesIt()
    {
        var nested = Path.Combine(this.directory, "deeper");

        var result = await new PickupDirectoryMailTransport().SendAsync(Message(), this.Account(nested), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(Directory.GetFiles(nested, "*.eml"));
    }

    [Fact]
    public async Task SendAsync_TwoSends_ProduceTwoFiles()
    {
        var transport = new PickupDirectoryMailTransport();

        await transport.SendAsync(Message(), this.Account(), TestContext.Current.CancellationToken);
        await transport.SendAsync(Message(), this.Account(), TestContext.Current.CancellationToken);

        Assert.Equal(2, Directory.GetFiles(this.directory, "*.eml").Length);
    }

    [Fact]
    public async Task SendAsync_AccountWithNoDirectory_Throws()
    {
        var account = new MailAccount { Name = "local", Transport = PickupDirectoryMailTransport.TransportName };

        await Assert.ThrowsAsync<MailAccountException>(() =>
            new PickupDirectoryMailTransport().SendAsync(Message(), account, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_InlineAttachment_SurvivesTheRoundTrip()
    {
        var message = Message() with
        {
            Attachments = [EmailAttachment.Inline("header-logo", "logo.png", new byte[] { 1, 2, 3 }, "image/png")],
        };

        await new PickupDirectoryMailTransport().SendAsync(message, this.Account(), TestContext.Current.CancellationToken);

        var file = Assert.Single(Directory.GetFiles(this.directory, "*.eml"));
        var reloaded = await MimeMessage.LoadAsync(file, TestContext.Current.CancellationToken);

        Assert.Contains(reloaded.BodyParts, part => part.ContentId == "header-logo");
    }
}
