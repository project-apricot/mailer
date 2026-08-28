using MimeKit;

namespace ApricotFramework.Mailer.Smtp.Impl;

/// <summary>
/// Turns an <see cref="EmailMessage"/> into the MIME document a mail server is given.
/// </summary>
/// <remarks>
/// Shared by every transport in this package, so what SMTP puts on the wire and what the
/// pickup directory writes to disk are the same document.
/// </remarks>
public static class MimeMessageFactory
{
    /// <summary>
    /// Builds the MIME document for a message.
    /// </summary>
    /// <param name="message">The message to convert, already validated.</param>
    /// <returns>The MIME document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    /// <exception cref="MimeKit.ParseException">
    /// Thrown when an attachment's content type is not a MIME type.
    /// </exception>
    public static MimeMessage Create(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mime = new MimeMessage();

        // only From is set, never Sender: setting both makes the envelope and the header disagree,
        // which is what breaks DMARC alignment
        if (message.From is not null)
        {
            mime.From.Add(ToMailbox(message.From));
        }

        mime.To.AddRange(message.To.Select(ToMailbox));
        mime.Cc.AddRange(message.Cc.Select(ToMailbox));
        mime.Bcc.AddRange(message.Bcc.Select(ToMailbox));
        mime.ReplyTo.AddRange(message.ReplyTo.Select(ToMailbox));
        mime.Subject = message.Subject;

        ApplyBody(mime, message);
        ApplyPriority(mime, message.Priority);

        foreach (var (name, value) in message.Headers)
        {
            // validation already refused the reserved ones; this is the second lock on the door
            if (EmailMessageValidation.IsWritableHeader(name))
            {
                mime.Headers.Add(name, value);
            }
        }

        return mime;
    }

    /// <summary>
    /// Gets the mailboxes a message is actually delivered to.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>Every recipient, as mailboxes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public static IReadOnlyList<MailboxAddress> GetRecipients(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return [.. message.GetAllRecipients().Select(ToMailbox)];
    }

    /// <summary>
    /// Converts one address.
    /// </summary>
    /// <param name="address">The address to convert.</param>
    /// <returns>The mailbox.</returns>
    public static MailboxAddress ToMailbox(EmailAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return new MailboxAddress(address.Name, address.Address);
    }

    /// <summary>
    /// Attaches the body and every file travelling with it.
    /// </summary>
    /// <param name="mime">The document being built.</param>
    /// <param name="message">The message being converted.</param>
    private static void ApplyBody(MimeMessage mime, EmailMessage message)
    {
        var builder = new BodyBuilder();

        // assigned only when present: feeding an empty string instead of null produces an
        // alternative part with nothing in it on every message
        if (message.Body.Html is not null)
        {
            builder.HtmlBody = message.Body.Html;
        }

        if (message.Body.Text is not null)
        {
            builder.TextBody = message.Body.Text;
        }

        foreach (var attachment in message.Attachments)
        {
            var collection = attachment.IsInline ? builder.LinkedResources : builder.Attachments;

            using var payload = attachment.OpenRead();

            var entity = collection.Add(attachment.FileName, payload, ContentType.Parse(attachment.ContentType));

            if (attachment.ContentId is not null)
            {
                entity.ContentId = attachment.ContentId;
            }
        }

        mime.Body = builder.ToMessageBody();
    }

    /// <summary>
    /// Writes the priority out as the headers clients actually read.
    /// </summary>
    /// <param name="mime">The document being built.</param>
    /// <param name="priority">The priority claimed, or null to claim none.</param>
    /// <remarks>
    /// Five levels are written to <c>X-Priority</c>, which has them, and folded onto the three of
    /// <c>Importance</c>, which is what most clients display.
    /// </remarks>
    private static void ApplyPriority(MimeMessage mime, EmailPriority? priority)
    {
        if (priority is null)
        {
            return;
        }

        mime.XPriority = priority switch
        {
            EmailPriority.Highest => XMessagePriority.Highest,
            EmailPriority.High => XMessagePriority.High,
            EmailPriority.Low => XMessagePriority.Low,
            EmailPriority.Lowest => XMessagePriority.Lowest,
            _ => XMessagePriority.Normal,
        };

        mime.Importance = priority switch
        {
            EmailPriority.Highest or EmailPriority.High => MessageImportance.High,
            EmailPriority.Low or EmailPriority.Lowest => MessageImportance.Low,
            _ => MessageImportance.Normal,
        };
    }
}
