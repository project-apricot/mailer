using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using ApricotFramework.Mailer.Smtp.Impl;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ApricotFramework.Mailer.Smtp;

/// <summary>
/// Delivers a message by handing it to an SMTP server.
/// </summary>
/// <remarks>
/// A connection, a handshake and an authentication per message. That is the honest cost of sending
/// one mail and it keeps the transport stateless, which is what lets a single registration serve
/// every account that names it.
/// </remarks>
public class SmtpMailTransport : IMailTransport
{
    /// <summary>
    /// The name an account sets to send through this transport.
    /// </summary>
    public const string TransportName = "smtp";

    /// <inheritdoc />
    public string Name => TransportName;

    /// <summary>
    /// Decides which failure an exception from the SMTP conversation represents.
    /// </summary>
    /// <param name="exception">The exception the send produced.</param>
    /// <returns>The code to report.</returns>
    /// <remarks>
    /// A 4xx reply is the server asking for the message later — a greylist, a busy mailbox, a rate
    /// limit — so it reports as throttled and is worth retrying. A 5xx reply is a refusal of this
    /// message and is not.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static MailErrorCode Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            AuthenticationException or ServiceNotAuthenticatedException => MailErrorCode.Authentication,
            SmtpCommandException command => (int)command.StatusCode >= 400 && (int)command.StatusCode < 500
                ? MailErrorCode.Throttled
                : MailErrorCode.Rejected,
            TimeoutException => MailErrorCode.Timeout,
            SslHandshakeException or SmtpProtocolException or SocketException or IOException => MailErrorCode.Connection,

            // MailKit raises this when StartTls was required and the server did not offer it
            NotSupportedException => MailErrorCode.Connection,
            _ => MailErrorCode.Unknown,
        };
    }

    /// <inheritdoc />
    public virtual async Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(account);

        var settings = SmtpAccountSettings.From(account.Settings);

        MimeMessage mime;

        try
        {
            mime = this.CreateMimeMessage(message);
        }
        catch (Exception e) when (e is ParseException or ArgumentException or FormatException)
        {
            // an unparseable content type is the message's problem, not the server's
            return MailSendResult.Failure(MailErrorCode.InvalidMessage, $"The message could not be composed: {e.Message}", account.Name, this.Name, e);
        }

        try
        {
            return await this.DeliverAsync(mime, message, account, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (Classify(e) != MailErrorCode.Unknown)
        {
            return MailSendResult.Failure(Classify(e), Describe(e), account.Name, this.Name, e);
        }
    }

    /// <summary>
    /// Creates the client a send runs on.
    /// </summary>
    /// <param name="settings">The account's SMTP settings.</param>
    /// <returns>The client, not yet connected. The caller disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    protected virtual SmtpClient CreateClient(SmtpAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var client = new SmtpClient
        {
            Timeout = (int)Math.Min(settings.Timeout.TotalMilliseconds, int.MaxValue),
        };

        client.ServerCertificateValidationCallback = this.ValidateServerCertificate;

        return client;
    }

    /// <summary>
    /// Decides whether to trust the certificate the server presented.
    /// </summary>
    /// <param name="sender">The client that made the connection.</param>
    /// <param name="certificate">The certificate presented, or null when none was.</param>
    /// <param name="chain">The chain built for it, or null when none was.</param>
    /// <param name="errors">What was wrong with it, if anything.</param>
    /// <returns>True to continue the handshake.</returns>
    /// <remarks>
    /// A security boundary. The default accepts only a certificate with nothing wrong with it, and an
    /// override that returns true unconditionally disables certificate validation for every account
    /// this transport serves — no analyzer will warn about it. For a development server, prefer
    /// pinning the expected certificate here, or configuring the account with
    /// <see cref="SmtpSecurity.None"/> over a loopback interface.
    /// </remarks>
    protected virtual bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        return errors == SslPolicyErrors.None;
    }

    /// <summary>
    /// Builds the MIME document for a message.
    /// </summary>
    /// <param name="message">The message to convert.</param>
    /// <returns>The MIME document.</returns>
    /// <remarks>
    /// The seam for changing what goes on the wire — adding a signature, rewriting a part — without
    /// reimplementing the send.
    /// </remarks>
    protected virtual MimeMessage CreateMimeMessage(EmailMessage message)
    {
        return MimeMessageFactory.Create(message);
    }

    /// <summary>
    /// Maps the library's security mode onto MailKit's.
    /// </summary>
    /// <param name="security">The mode the account asked for.</param>
    /// <returns>The MailKit equivalent.</returns>
    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security)
    {
        return security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurity.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
            _ => SecureSocketOptions.Auto,
        };
    }

    /// <summary>
    /// Describes a failure without quoting anything the account supplied.
    /// </summary>
    /// <param name="exception">The exception to describe.</param>
    /// <returns>The description for the result.</returns>
    private static string Describe(Exception exception)
    {
        return $"The SMTP server reported {exception.GetType().Name}: {exception.Message}";
    }

    /// <summary>
    /// Runs the SMTP conversation.
    /// </summary>
    /// <param name="mime">The document to send.</param>
    /// <param name="message">The message it came from, for the envelope sender.</param>
    /// <param name="account">The account being sent as.</param>
    /// <param name="settings">The account's SMTP settings.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>What came of the send.</returns>
    private async Task<MailSendResult> DeliverAsync(MimeMessage mime, EmailMessage message, MailAccount account, SmtpAccountSettings settings, CancellationToken cancellationToken)
    {
        using var client = this.CreateClient(settings);

        await client.ConnectAsync(settings.Host, settings.Port, ToSocketOptions(settings.Security), cancellationToken).ConfigureAwait(false);

        // a relay that accepts unauthenticated submission is a real configuration, so an account
        // without credentials simply does not authenticate
        if (settings.TryGetCredentials(out var username, out var password))
        {
            await client.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);
        }

        if (message.EnvelopeFrom is null)
        {
            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await client.SendAsync(mime, MimeMessageFactory.ToMailbox(message.EnvelopeFrom), MimeMessageFactory.GetRecipients(message), cancellationToken).ConfigureAwait(false);
        }

        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);

        return MailSendResult.Success(account.Name, this.Name, mime.MessageId);
    }
}
