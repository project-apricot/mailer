namespace ApricotFramework.Mailer;

/// <summary>
/// Checks a message is sendable before transport opens a connection for it.
/// </summary>
/// <remarks>
/// Addresses validate themselves when they are created, so what is left is the message as a whole
/// and the one place a caller can still put a raw string: <see cref="EmailMessage.Headers"/>.
/// </remarks>
public static class EmailMessageValidation
{
    /// <summary>
    /// The headers a caller may not set, because transport composes them from the message.
    /// </summary>
    /// <remarks>
    /// Chiefly a security boundary, not tidiness. A <c>Bcc</c> header is merged into the real
    /// recipient list — it becomes an extra <c>RCPT TO</c> — so a host that forwards a user-supplied
    /// header bag would otherwise be handing out blind copies of its own mail. <c>From</c> behaves
    /// the same way and produces a second sender.
    /// </remarks>
    private static readonly HashSet<string> ReservedHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bcc",
        "Cc",
        "Content-Transfer-Encoding",
        "Content-Type",
        "Date",
        "DKIM-Signature",
        "From",
        "Message-Id",
        "MIME-Version",
        "Received",
        "Reply-To",
        "Return-Path",
        "Sender",
        "Subject",
        "To"
    };

    /// <summary>
    /// Decides whether a header is one a caller may set.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>True when a transport should write it.</returns>
    /// <remarks>
    /// Any <c>Resent-</c> header is reserved as well, since those re-address a message just as the
    /// originals do.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static bool IsWritableHeader(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return !ReservedHeaderNames.Contains(name) && !name.StartsWith("Resent-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Names the first reason a message cannot be sent.
    /// </summary>
    /// <param name="message">The message to check.</param>
    /// <param name="resolvedFrom">
    /// The sender the sending resolved, which is the message's own or the account's default.
    /// </param>
    /// <returns>The reason it is unsendable or null when it is sendable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public static string? Explain(EmailMessage message, EmailAddress? resolvedFrom)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (resolvedFrom is null)
        {
            return "The message has no sender and the account does not set a default one.";
        }

        if (!message.GetAllRecipients().Any())
        {
            return "The message has no recipient in To, Cc or Bcc.";
        }

        // MimeKit defends itself against header injection by silently stripping CR and LF, which
        // would deliver a mangled subject instead of telling anyone
        if (message.Subject.Any(char.IsControl))
        {
            return "The subject contains a control character.";
        }

        if (message.Body.IsEmpty())
        {
            return "The message has neither an HTML nor a plain-text body.";
        }

        return ExplainHeaders(message.Headers);
    }

    /// <summary>
    /// Names the first reason the extra headers cannot be written.
    /// </summary>
    /// <param name="headers">The headers the caller supplied.</param>
    /// <returns>The reason they are unusable or null when they are usable.</returns>
    private static string? ExplainHeaders(IReadOnlyDictionary<string, string> headers)
    {
        foreach (var (name, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Any(static c => char.IsControl(c) || char.IsWhiteSpace(c) || c == ':'))
            {
                return "A header name is empty or contains white space, a colon or a control character.";
            }

            if (!IsWritableHeader(name))
            {
                return $"Header '{name}' is composed from the message and may not be set directly. Setting it as a header would change who receives the message.";
            }

            if (value is null || value.Any(char.IsControl))
            {
                return $"The value of header '{name}' is null or contains a control character.";
            }
        }

        return null;
    }
}
