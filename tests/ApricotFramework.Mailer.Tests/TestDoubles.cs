using ApricotFramework.Mailer;

namespace ApricotFramework.Mailer.Tests;

/// <summary>
/// A message that passes validation, so a test can change one thing about it.
/// </summary>
internal static class SampleMail
{
    public static EmailMessage Message()
    {
        return new EmailMessage
        {
            Subject = "Welcome",
            Body = EmailBody.FromHtml("<b>hello</b>"),
            To = [EmailAddress.Parse("recipient@example.com")],
        };
    }

    public static MailAccount Account(string name = "default", string transport = "stub")
    {
        return new MailAccount
        {
            Name = name,
            Transport = transport,
            DefaultFrom = EmailAddress.Parse("no-reply@example.com", "Example"),
        };
    }
}

/// <summary>
/// A transport that records what it was given and answers however the test asks it to.
/// </summary>
internal sealed class StubTransport : IMailTransport
{
    private readonly Func<EmailMessage, MailAccount, MailSendResult>? responder;

    public StubTransport(string name = "stub", Func<EmailMessage, MailAccount, MailSendResult>? responder = null)
    {
        this.Name = name;
        this.responder = responder;
    }

    public string Name { get; }

    public List<(EmailMessage Message, MailAccount Account)> Calls { get; } = [];

    public Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        this.Calls.Add((message, account));

        return Task.FromResult(this.responder?.Invoke(message, account) ?? MailSendResult.Success(account.Name, this.Name, "stub-1"));
    }
}

/// <summary>
/// A transport that throws, to prove what escapes a transport becomes a result.
/// </summary>
internal sealed class ThrowingTransport(Exception failure, string name = "stub") : IMailTransport
{
    public string Name { get; } = name;

    public Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        throw failure;
    }
}

/// <summary>
/// An account source holding a fixed set of accounts, counting how often it is asked.
/// </summary>
internal sealed class StubAccountSource(params MailAccount[] accounts) : IMailAccountSource
{
    private readonly Dictionary<string, MailAccount> accounts =
        accounts.ToDictionary(account => account.Name, StringComparer.OrdinalIgnoreCase);

    public int Lookups { get; private set; }

    public Task<MailAccount?> FindAsync(string name, CancellationToken cancellationToken)
    {
        this.Lookups++;

        return Task.FromResult(this.accounts.GetValueOrDefault(name));
    }
}

/// <summary>
/// A store answering from a fixed set, so a decorator can be tested without a real source.
/// </summary>
internal sealed class StubAccountStore(params MailAccount[] accounts) : IMailAccountStore
{
    private readonly Dictionary<string, MailAccount> accounts =
        accounts.ToDictionary(account => account.Name, StringComparer.OrdinalIgnoreCase);

    public int Lookups { get; private set; }

    public Task<MailAccount?> GetAsync(string name, CancellationToken cancellationToken)
    {
        this.Lookups++;

        return Task.FromResult(this.accounts.GetValueOrDefault(name));
    }
}

