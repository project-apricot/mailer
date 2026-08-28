namespace ApricotFramework.Mailer;

/// <summary>
/// Thrown when a sending account's settings cannot be read as the transport needs them.
/// </summary>
/// <remarks>
/// A sending turns this into a <see cref="MailErrorCode.InvalidAccount"/> result rather than letting it
/// escape. Messages name the account and the setting but never the value, so a credential cannot
/// reach a log through one.
/// </remarks>
public class MailAccountException : Exception
{
    /// <summary>
    /// Creates a new instance of the exception.
    /// </summary>
    public MailAccountException()
    {
    }

    /// <summary>
    /// Creates a new instance of the exception.
    /// </summary>
    /// <param name="message">What could not be read.</param>
    public MailAccountException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new instance of the exception.
    /// </summary>
    /// <param name="message">What could not be read.</param>
    /// <param name="innerException">The underlying failure.</param>
    public MailAccountException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
