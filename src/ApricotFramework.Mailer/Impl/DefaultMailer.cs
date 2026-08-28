using ApricotFramework.Mailer.Options;

namespace ApricotFramework.Mailer.Impl;

/// <summary>
/// A mailer whose options are an object handed to it once.
/// </summary>
/// <remarks>
/// Usable without a container, so a console or worker host can new one up. A host whose options can
/// change while it runs derives from <see cref="MailerBase"/> instead and reads them per send.
/// </remarks>
public class DefaultMailer : MailerBase
{
    private readonly MailerOptions options;

    /// <summary>
    /// Creates a new instance of the mailer.
    /// </summary>
    /// <param name="accountStore">Where accounts are resolved from.</param>
    /// <param name="transports">The transports available to send through.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accountStore"/> or <paramref name="transports"/> is null.
    /// </exception>
    public DefaultMailer(IMailAccountStore accountStore, IEnumerable<IMailTransport> transports, MailerOptions? options = null)
        : base(accountStore, transports)
    {
        this.options = options ?? new MailerOptions();
    }

    /// <inheritdoc />
    protected override MailerOptions GetCurrentOptions()
    {
        return this.options;
    }
}
