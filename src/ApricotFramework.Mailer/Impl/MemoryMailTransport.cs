using System.Collections.Concurrent;

namespace ApricotFramework.Mailer.Impl;

/// <summary>
/// Keeps every message instead of delivering it, so a test or a development host can assert on what
/// would have been sent.
/// </summary>
/// <remarks>
/// Register one instance and read <see cref="GetSentMessages"/>. Messages accumulate for the lifetime
/// of the instance, so a long-running host should either <see cref="Clear"/> periodically or use the
/// pickup-directory transport instead.
/// </remarks>
public class MemoryMailTransport : IMailTransport
{
    /// <summary>
    /// The name an account sets to send through this transport.
    /// </summary>
    public const string TransportName = "memory";

    private readonly ConcurrentQueue<SentMail> sent = new();

    /// <inheritdoc />
    public string Name => TransportName;

    /// <inheritdoc />
    public virtual Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(account);

        cancellationToken.ThrowIfCancellationRequested();

        var messageId = $"memory-{Guid.NewGuid():n}";

        this.sent.Enqueue(new SentMail(account.Name, messageId, message));

        return Task.FromResult(MailSendResult.Success(account.Name, this.Name, messageId));
    }

    /// <summary>
    /// Gets what has been sent so far, oldest first.
    /// </summary>
    /// <returns>The captured messages.</returns>
    public IReadOnlyList<SentMail> GetSentMessages()
    {
        return [.. this.sent];
    }

    /// <summary>
    /// Discards everything captured so far.
    /// </summary>
    public void Clear()
    {
        while (this.sent.TryDequeue(out _))
        {
            // draining is the point; a concurrent queue has no bulk clear
        }
    }

    /// <summary>
    /// One message this transport was asked to send.
    /// </summary>
    /// <param name="Account">The account it was sent as.</param>
    /// <param name="MessageId">The identifier this transport assigned it.</param>
    /// <param name="Message">The message itself.</param>
    public sealed record SentMail(string Account, string MessageId, EmailMessage Message);
}
