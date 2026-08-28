namespace ApricotFramework.Mailer.Smtp;

/// <summary>
/// How the connection to an SMTP server is secured.
/// </summary>
/// <remarks>
/// <see cref="Auto"/> is the zero value on purpose, so a setting that is missing or unreadable can
/// never leave a session unencrypted by default.
/// </remarks>
public enum SmtpSecurity
{
    /// <summary>
    /// Let the port decide: implicit TLS on 465, STARTTLS elsewhere when the server offers it.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// No encryption at all.
    /// </summary>
    /// <remarks>
    /// The credentials and the message cross the network in the clear. Only for a relay reached over
    /// a loopback interface or an already-encrypted link.
    /// </remarks>
    None = 1,

    /// <summary>
    /// TLS from the first byte, as on port 465.
    /// </summary>
    SslOnConnect = 2,

    /// <summary>
    /// Connect in the clear and require an upgrade to TLS, failing if the server will not.
    /// </summary>
    StartTls = 3,

    /// <summary>
    /// Upgrade to TLS when the server offers it, and continue in the clear when it does not.
    /// </summary>
    /// <remarks>
    /// A server that simply stops advertising STARTTLS silently downgrades the session, so prefer
    /// <see cref="StartTls"/> wherever the server is known to support it.
    /// </remarks>
    StartTlsWhenAvailable = 4,
}
