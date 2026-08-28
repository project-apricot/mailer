namespace ApricotFramework.Mailer;

/// <summary>
/// One message to send.
/// </summary>
/// <remarks>
/// Nothing here is transport-specific: the same message can go out through any transport, and what a
/// transport cannot express it leaves out. <see cref="From"/> is optional because the sending account
/// usually supplies it.
/// <para>
/// Equality is the compiler's, so two messages built from equal parts are <b>not</b> equal: the
/// collection and delegate members compare by reference. Compare the members you care about.
/// </para>
/// </remarks>
public sealed record EmailMessage
{
    /// <summary>
    /// Gets the subject line. Required, though it may be empty.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Gets what the message says.
    /// </summary>
    public required EmailBody Body { get; init; }

    /// <summary>
    /// Gets the primary recipients.
    /// </summary>
    public required IReadOnlyList<EmailAddress> To { get; init; }

    /// <summary>
    /// Gets the sender, or null to use the sending account's default.
    /// </summary>
    public EmailAddress? From { get; init; }

    /// <summary>
    /// Gets the address bounces go to, or null to use the sending account's default.
    /// </summary>
    /// <remarks>
    /// The envelope sender, which is what SMTP carries in <c>MAIL FROM</c> and is separate from
    /// <see cref="From"/>. Set it when the two must-differ: sending as a customer's domain while
    /// keeping bounces, and the SPF record they align with, on your own.
    /// </remarks>
    public EmailAddress? EnvelopeFrom { get; init; }

    /// <summary>
    /// Gets the carbon-copy recipients.
    /// </summary>
    public IReadOnlyList<EmailAddress> Cc { get; init; } = [];

    /// <summary>
    /// Gets the blind carbon-copy recipients.
    /// </summary>
    public IReadOnlyList<EmailAddress> Bcc { get; init; } = [];

    /// <summary>
    /// Gets where replies should go, or empty to use the sending account's default.
    /// </summary>
    public IReadOnlyList<EmailAddress> ReplyTo { get; init; } = [];

    /// <summary>
    /// Gets the files traveling with the message, attached or inline.
    /// </summary>
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// Gets extra headers to write, keyed by the header name.
    /// </summary>
    /// <remarks>
    /// For headers transport does not already own. Setting one it does own — <c>From</c>,
    /// <c>To</c>, <c>Subject</c> — is ignored rather than allowed to contradict the message.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the importance the message claims, or null to claim none.
    /// </summary>
    public EmailPriority? Priority { get; init; }

    /// <summary>
    /// Gets per-send values that mean something to one transport only, keyed by name.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="MailAccount.Settings"/> for things that vary per message rather
    /// than per account — a provider template id, a tag, a scheduled time, an idempotency key.
    /// Transport reads the names it knows and ignores the rest, so a value meant for one transport is
    /// harmless when the message goes through another.
    /// </remarks>
    public IReadOnlyDictionary<string, string?> Properties { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets every recipient across <see cref="To"/>, <see cref="Cc"/> and <see cref="Bcc"/>.
    /// </summary>
    /// <returns>The recipients, in that order.</returns>
    public IEnumerable<EmailAddress> GetAllRecipients()
    {
        return this.To.Concat(this.Cc).Concat(this.Bcc);
    }
}
