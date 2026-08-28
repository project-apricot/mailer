using ApricotFramework.Mailer.Options;

namespace ApricotFramework.Mailer.Impl;

/// <summary>
/// Everything a mailer does, except deciding where its options come from.
/// </summary>
/// <remarks>
/// Resolves the account, picks its transport, checks the message, and hands it over. Everything except
/// cancellation comes back as a <see cref="MailSendResult"/>.
/// <para>
/// Derive from this when options reach you from somewhere of your own; derive from
/// <see cref="DefaultMailer"/> when they are an object you already hold.
/// </para>
/// </remarks>
public abstract class MailerBase : IMailer
{
    private readonly IMailAccountStore accountStore;

    /// <summary>
    /// Creates a new instance of the mailer.
    /// </summary>
    /// <param name="accountStore">Where accounts are resolved from.</param>
    /// <param name="transports">The transports available to send through.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accountStore"/> or <paramref name="transports"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when two transports answer to the same name.</exception>
    protected MailerBase(IMailAccountStore accountStore, IEnumerable<IMailTransport> transports)
    {
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(transports);

        this.accountStore = accountStore;
        this.Transports = new MailTransportRegistry(transports);
    }

    /// <summary>
    /// Gets the transports available to send through.
    /// </summary>
    protected MailTransportRegistry Transports { get; }

    /// <inheritdoc />
    public virtual Task<MailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        return this.SendAsync(this.GetDefaultAccountName(), message, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<MailSendResult> SendAsync(string accountName, EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentNullException.ThrowIfNull(message);

        MailAccount? account;

        try
        {
            account = await this.accountStore.GetAsync(accountName, cancellationToken).ConfigureAwait(false);
        }
        catch (MailAccountException e)
        {
            // a declared account that cannot be read at all, such as one naming no transport
            return MailSendResult.Failure(MailErrorCode.InvalidAccount, e.Message, accountName, exception: e);
        }

        if (account is null)
        {
            return MailSendResult.Failure(
                MailErrorCode.AccountNotFound,
                $"No mail account named '{accountName}' was found. Declare it in configuration or supply it from an account source.",
                accountName);
        }

        var transport = this.Transports.Find(account.Transport);

        if (transport is null)
        {
            var registered = this.Transports.GetNames();

            return MailSendResult.Failure(
                MailErrorCode.TransportNotFound,
                registered.Count == 0
                    ? $"Mail account '{account.Name}' uses transport '{account.Transport}', and no transport is registered at all."
                    : $"Mail account '{account.Name}' uses transport '{account.Transport}', which is not registered. Registered transports: {string.Join(", ", registered)}.",
                account.Name,
                account.Transport);
        }

        var prepared = Prepare(message, account);
        var problem = EmailMessageValidation.Explain(prepared, prepared.From)
            ?? ExplainSize(prepared, this.GetCurrentOptions().MaxAttachmentBytes);

        if (problem is not null)
        {
            return MailSendResult.Failure(MailErrorCode.InvalidMessage, problem, account.Name, account.Transport);
        }

        return await this.DispatchAsync(transport, prepared, account, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the options in force for this sending.
    /// </summary>
    /// <returns>The options. Never null.</returns>
    /// <remarks>
    /// The one thing a mailer has to supply, and a method rather than a property because supplying it
    /// is expected to do real work — reading from a monitor that may have reloaded, for instance.
    /// </remarks>
    protected abstract MailerOptions GetCurrentOptions();

    /// <summary>
    /// Gets the account name a sending uses when the caller names none.
    /// </summary>
    /// <returns>The account name. Never blank.</returns>
    protected virtual string GetDefaultAccountName()
    {
        var configured = this.GetCurrentOptions().DefaultAccount;

        return string.IsNullOrWhiteSpace(configured) ? MailerOptions.FallbackAccountName : configured.Trim();
    }

    /// <summary>
    /// Hands the message to the transport and turns anything that escapes into a result.
    /// </summary>
    /// <param name="transport">The transport to send through.</param>
    /// <param name="message">The prepared message.</param>
    /// <param name="account">The account being sent as.</param>
    /// <param name="cancellationToken">Cancels the sending.</param>
    /// <returns>What came of the sending.</returns>
    protected virtual async Task<MailSendResult> DispatchAsync(IMailTransport transport, EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(account);

        try
        {
            return await transport.SendAsync(message, account, cancellationToken).ConfigureAwait(false);
        }
        catch (MailAccountException e)
        {
            return MailSendResult.Failure(MailErrorCode.InvalidAccount, e.Message, account.Name, account.Transport, e);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // cancellation the caller asked for is their decision, not a delivery failure
            throw;
        }
        catch (OperationCanceledException e)
        {
            // a transport's own deadline also surfaces as this, and that is a timeout
            return MailSendResult.Failure(
                MailErrorCode.Timeout,
                $"Transport '{transport.Name}' gave up before the send completed.",
                account.Name,
                account.Transport,
                e);
        }
        catch (Exception e)
        {
            // transport is asked to classify its own failures; this is the backstop for the ones it
            // does not, so that one misbehaving transport cannot take a request down
            return MailSendResult.Failure(
                MailErrorCode.Unknown,
                $"Transport '{transport.Name}' failed with {e.GetType().Name}: {e.Message}",
                account.Name,
                account.Transport,
                e);
        }
    }

    /// <summary>
    /// Fills in what the account supplies and the message left out.
    /// </summary>
    /// <param name="message">The message as the caller wrote it.</param>
    /// <param name="account">The account being sent as.</param>
    /// <returns>The message transport receives.</returns>
    private static EmailMessage Prepare(EmailMessage message, MailAccount account)
    {
        return message with
        {
            // the account's sender is taken whole: mixing the message's address with the account's
            // display name produces a mailbox neither of them asked for
            From = message.From ?? account.DefaultFrom,
            EnvelopeFrom = message.EnvelopeFrom ?? account.EnvelopeFrom,
            ReplyTo = message.ReplyTo.Count > 0 || account.DefaultReplyTo is null ? message.ReplyTo : [account.DefaultReplyTo],
        };
    }

    /// <summary>
    /// Names the first attachment that is larger than the configured ceiling.
    /// </summary>
    /// <param name="message">The message to measure.</param>
    /// <param name="maxBytes">The ceiling in bytes, or 0 for no limit.</param>
    /// <returns>The reason the message is too large or null when it is within the limit.</returns>
    private static string? ExplainSize(EmailMessage message, long maxBytes)
    {
        if (maxBytes <= 0)
        {
            return null;
        }

        foreach (var attachment in message.Attachments)
        {
            using var payload = attachment.OpenRead();

            // an attachment that cannot report its length would have to be buffered to measure,
            // which is the cost the limit exists to avoid
            if (payload.CanSeek && payload.Length > maxBytes)
            {
                return $"Attachment '{attachment.FileName}' is larger than the configured limit of {maxBytes} bytes.";
            }
        }

        return null;
    }
}
