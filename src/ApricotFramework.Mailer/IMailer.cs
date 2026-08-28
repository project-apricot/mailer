namespace ApricotFramework.Mailer;

/// <summary>
/// Sends mail through a named account.
/// </summary>
public interface IMailer
{
    /// <summary>
    /// Sends a message through the default account.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancels the sending.</param>
    /// <returns>What came of the sending.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled. Every other failure is a
    /// result.
    /// </exception>
    Task<MailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message through a named account.
    /// </summary>
    /// <param name="accountName">The account to send as.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancels the sending.</param>
    /// <returns>
    /// What came of the sending, including <see cref="MailErrorCode.AccountNotFound"/> when no source
    /// knows the account.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accountName"/> is null or blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled. Every other failure is a
    /// result.
    /// </exception>
    Task<MailSendResult> SendAsync(string accountName, EmailMessage message, CancellationToken cancellationToken = default);
}
