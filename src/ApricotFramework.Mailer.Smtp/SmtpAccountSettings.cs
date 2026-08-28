using System.Diagnostics.CodeAnalysis;

namespace ApricotFramework.Mailer.Smtp;

/// <summary>
/// The settings <see cref="SmtpMailTransport"/> reads from a sending account.
/// </summary>
/// <remarks>
/// A class rather than a record, and with no <c>ToString</c>, because it holds a password.
/// </remarks>
public sealed class SmtpAccountSettings
{
    /// <summary>
    /// The setting naming the server to connect to.
    /// </summary>
    public const string HostSetting = "Host";

    /// <summary>
    /// The setting naming the port to connect on.
    /// </summary>
    public const string PortSetting = "Port";

    /// <summary>
    /// The setting naming how the connection is secured.
    /// </summary>
    public const string SecuritySetting = "Security";

    /// <summary>
    /// The setting naming the user to authenticate as.
    /// </summary>
    public const string UsernameSetting = "Username";

    /// <summary>
    /// The setting naming the password or token to authenticate with.
    /// </summary>
    public const string PasswordSetting = "Password";

    /// <summary>
    /// The setting naming how long to wait on the server.
    /// </summary>
    public const string TimeoutSetting = "Timeout";

    /// <summary>
    /// The port used when the account does not name one, which is the submission port.
    /// </summary>
    public const int DefaultPort = 587;

    /// <summary>
    /// The lowest port number a server can listen on.
    /// </summary>
    private const int MinimumPort = 1;

    /// <summary>
    /// The highest port number a server can listen on.
    /// </summary>
    private const int MaximumPort = 65535;

    private SmtpAccountSettings(string host, int port, SmtpSecurity security, string? username, string? password, TimeSpan timeout)
    {
        this.Host = host;
        this.Port = port;
        this.Security = security;
        this.Username = username;
        this.Password = password;
        this.Timeout = timeout;
    }

    /// <summary>
    /// The time allowed on the server when the account does not say.
    /// </summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets the server to connect to.
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// Gets the port to connect on.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets how the connection is secured.
    /// </summary>
    public SmtpSecurity Security { get; }

    /// <summary>
    /// Gets the user to authenticate as, or null to skip authentication.
    /// </summary>
    public string? Username { get; }

    /// <summary>
    /// Gets the password or token to authenticate with, or null when there is no user.
    /// </summary>
    public string? Password { get; }

    /// <summary>
    /// Gets how long to wait on the server.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the credentials to authenticate with, when the account has any.
    /// </summary>
    /// <param name="username">The user to authenticate as.</param>
    /// <param name="password">The password or token to authenticate with.</param>
    /// <returns>False when the account authenticates with nothing, as an open relay would.</returns>
    /// <remarks>
    /// The two travel together because one without the other is rejected when the settings are read;
    /// this is what makes that invariant visible to a caller.
    /// </remarks>
    public bool TryGetCredentials([NotNullWhen(true)] out string? username, [NotNullWhen(true)] out string? password)
    {
        username = this.Username;
        password = this.Password;

        return username is not null && password is not null;
    }

    /// <summary>
    /// Reads the SMTP settings out of an account.
    /// </summary>
    /// <param name="settings">The account's settings.</param>
    /// <returns>The settings, validated.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    /// <exception cref="MailAccountException">
    /// Thrown when a setting is missing, unreadable or out of range. No message quotes a value.
    /// </exception>
    public static SmtpAccountSettings From(MailAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var host = settings.GetRequiredString(HostSetting);
        var port = settings.GetInt32(PortSetting, DefaultPort);

        if (port is < MinimumPort or > MaximumPort)
        {
            throw new MailAccountException($"Setting '{PortSetting}' is expected to be between {MinimumPort} and {MaximumPort}.");
        }

        var username = settings.GetString(UsernameSetting);
        var password = settings.GetString(PasswordSetting);

        if (username is not null && password is null)
        {
            throw new MailAccountException($"Setting '{UsernameSetting}' is set, so '{PasswordSetting}' is required as well.");
        }

        var timeout = settings.GetTimeSpan(TimeoutSetting, DefaultTimeout);

        if (timeout <= TimeSpan.Zero)
        {
            throw new MailAccountException($"Setting '{TimeoutSetting}' is expected to be a positive duration.");
        }

        return new SmtpAccountSettings(host, port, settings.GetEnum(SecuritySetting, SmtpSecurity.Auto), username, password, timeout);
    }
}
