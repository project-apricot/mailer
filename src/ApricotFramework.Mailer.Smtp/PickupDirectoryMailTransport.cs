using ApricotFramework.Mailer.Smtp.Impl;
using MimeKit;

namespace ApricotFramework.Mailer.Smtp;

/// <summary>
/// Writes each message into a directory as an <c>.eml</c> file instead of delivering it.
/// </summary>
/// <remarks>
/// For seeing what a host actually sends, without a server and without a real recipient: the files
/// open in any mail client, so an HTML body renders as the recipient would see it. Also the shape a
/// local relay agent consumes, which is where the name comes from.
/// <para>
/// The file is the whole message in the clear. Point it somewhere that is not backed up or shipped
/// to a log collector.
/// </para>
/// </remarks>
public class PickupDirectoryMailTransport : IMailTransport
{
    /// <summary>
    /// The name an account sets to send through this transport.
    /// </summary>
    public const string TransportName = "pickup";

    /// <summary>
    /// The setting naming the directory to write into.
    /// </summary>
    public const string DirectorySetting = "Directory";

    /// <inheritdoc />
    public string Name => TransportName;

    /// <inheritdoc />
    public virtual async Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(account);

        var directory = account.Settings.GetRequiredString(DirectorySetting);

        MimeMessage mime;

        try
        {
            mime = this.CreateMimeMessage(message);
        }
        catch (Exception e) when (e is ParseException or ArgumentException or FormatException)
        {
            return MailSendResult.Failure(MailErrorCode.InvalidMessage, $"The message could not be composed: {e.Message}", account.Name, this.Name, e);
        }

        try
        {
            var path = await WriteAsync(mime, directory, cancellationToken).ConfigureAwait(false);

            return MailSendResult.Success(account.Name, this.Name, Path.GetFileNameWithoutExtension(path));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return MailSendResult.Failure(MailErrorCode.Connection, $"The message could not be written to '{directory}': {e.Message}", account.Name, this.Name, e);
        }
    }

    /// <summary>
    /// Builds the MIME document for a message.
    /// </summary>
    /// <param name="message">The message to convert.</param>
    /// <returns>The MIME document.</returns>
    protected virtual MimeMessage CreateMimeMessage(EmailMessage message)
    {
        return MimeMessageFactory.Create(message);
    }

    /// <summary>
    /// Writes one document into the directory.
    /// </summary>
    /// <param name="mime">The document to write.</param>
    /// <param name="directory">Where to write it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The path written.</returns>
    private static async Task<string> WriteAsync(MimeMessage mime, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);

        // the name is generated, never derived from the subject or an address: those are attacker
        // input, and a path separator in one would decide where the file lands
        var name = $"{DateTime.UtcNow:yyyyMMdd'T'HHmmssfff}-{Guid.NewGuid():n}";
        var path = Path.Combine(directory, $"{name}.eml");
        var partial = Path.Combine(directory, $"{name}.tmp");

        await using (var stream = File.Create(partial))
        {
            await mime.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        // written aside and moved, so an agent watching the directory never reads half a message
        File.Move(partial, path);

        return path;
    }
}
