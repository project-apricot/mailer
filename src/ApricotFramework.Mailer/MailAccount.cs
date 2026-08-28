namespace ApricotFramework.Mailer;

/// <summary>
/// A named identity to send as: which transport carries the mail, what that transport needs, and the
/// sender to fall back to.
/// </summary>
/// <remarks>
/// One account per set of credentials. Transport is stateless and receives the account on every
/// sending, so one registered transport serves any number of accounts, and an account's settings may
/// change while the process runs.
/// </remarks>
public sealed record MailAccount
{
    /// <summary>
    /// Gets the name callers to address this account by.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the name of the transport that carries the mail, such as <c>smtp</c>.
    /// </summary>
    public required string Transport { get; init; }

    /// <summary>
    /// Gets the sender used when a message does not set one, or null to require the message to.
    /// </summary>
    public EmailAddress? DefaultFrom { get; init; }

    /// <summary>
    /// Gets where replies go when a message does not say, or null for none.
    /// </summary>
    public EmailAddress? DefaultReplyTo { get; init; }

    /// <summary>
    /// Gets the address bounces go to when a message does not say, or null to use
    /// <see cref="DefaultFrom"/>.
    /// </summary>
    /// <remarks>
    /// The envelope sender. Set it to a mailbox you actually read, or to one your bounce processor
    /// consumes; it is not shown to the recipient.
    /// </remarks>
    public EmailAddress? EnvelopeFrom { get; init; }

    /// <summary>
    /// Gets the settings the transport reads, such as a host and credentials.
    /// </summary>
    public MailAccountSettings Settings { get; init; } = MailAccountSettings.Empty;
}
