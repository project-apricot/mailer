namespace ApricotFramework.Mailer;

/// <summary>
/// Carries a message to somewhere that will deliver it.
/// </summary>
/// <remarks>
/// An implementation must be stateless and safe to use from several threads: one instance is shared
/// by every account naming it, and everything it needs arrives per sending on the account. It should
/// classify the failures it understands into a <see cref="MailSendResult"/> rather than throwing.
/// </remarks>
public interface IMailTransport
{
    /// <summary>
    /// Gets the name an account's <see cref="MailAccount.Transport"/> matches, such as <c>smtp</c>.
    /// </summary>
    /// <remarks>
    /// Matched case-insensitively, and no two registered transports may share one.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Sends a message.
    /// </summary>
    /// <param name="message">The message to send, already validated.</param>
    /// <param name="account">The account to send as, including the settings this transport reads.</param>
    /// <param name="cancellationToken">Cancels the sending.</param>
    /// <returns>What came of the sending.</returns>
    /// <exception cref="MailAccountException">
    /// Thrown when the account's settings are unusable. The sending reports it as
    /// <see cref="MailErrorCode.InvalidAccount"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled.
    /// </exception>
    Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken);
}
