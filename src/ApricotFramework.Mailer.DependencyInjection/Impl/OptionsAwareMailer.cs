using System.Diagnostics;
using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Mailer.DependencyInjection.Impl;

/// <summary>
/// A mailer that reads its options from configuration as it goes and logs the outcome of each send.
/// </summary>
public partial class OptionsAwareMailer : MailerBase
{
    private readonly IOptionsMonitor<MailerOptions> optionsMonitor;

    private readonly ILogger<OptionsAwareMailer> logger;

    /// <summary>
    /// Creates a new instance of the mailer.
    /// </summary>
    /// <param name="accountStore">Where accounts are resolved from.</param>
    /// <param name="transports">The transports available to send through.</param>
    /// <param name="optionsMonitor">Where the options are read from.</param>
    /// <param name="logger">Where the outcome of a send is reported.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="optionsMonitor"/> or <paramref name="logger"/> is null.
    /// </exception>
    public OptionsAwareMailer(
        IMailAccountStore accountStore,
        IEnumerable<IMailTransport> transports,
        IOptionsMonitor<MailerOptions> optionsMonitor,
        ILogger<OptionsAwareMailer> logger)
        : base(accountStore, transports)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(logger);

        this.optionsMonitor = optionsMonitor;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override async Task<MailSendResult> SendAsync(string accountName, EmailMessage message, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();

        var result = await base.SendAsync(accountName, message, cancellationToken).ConfigureAwait(false);

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (result.Succeeded)
        {
            this.LogSent(result.Account, result.Transport, elapsed);
        }
        else
        {
            // the reason can quote a server's reply, so it goes to the log and not into ToString
            this.LogNotSent(result.Account, result.Transport, result.ErrorCode, result.Error, result.Exception);
        }

        return result;
    }

    /// <inheritdoc />
    protected override MailerOptions GetCurrentOptions()
    {
        return this.optionsMonitor.CurrentValue;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Mail sent as account {Account} through transport {Transport} in {ElapsedMilliseconds}ms.")]
    private partial void LogSent(string account, string? transport, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail was not sent as account {Account} through transport {Transport}: {ErrorCode}. {Reason}")]
    private partial void LogNotSent(string account, string? transport, MailErrorCode errorCode, string? reason, Exception? exception);
}
