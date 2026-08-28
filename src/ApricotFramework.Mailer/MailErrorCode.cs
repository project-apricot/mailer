namespace ApricotFramework.Mailer;

/// <summary>
/// Why a sending did not deliver.
/// </summary>
/// <remarks>
/// The code is the part a caller should branch on; <see cref="MailSendResult.Error"/> carries the
/// detail for a log. Codes are only ever added, so a switch that handles the ones it knows and
/// treats the rest as a failure keeps working.
/// </remarks>
public enum MailErrorCode
{
    /// <summary>
    /// No error: the sending succeeded.
    /// </summary>
    None = 0,

    /// <summary>
    /// No account source knows the requested account.
    /// </summary>
    AccountNotFound = 1,

    /// <summary>
    /// The account names transport that is not registered.
    /// </summary>
    TransportNotFound = 2,

    /// <summary>
    /// The account is known, but its settings are unusable, such as a missing host.
    /// </summary>
    InvalidAccount = 3,

    /// <summary>
    /// The message itself is unsendable, such as having no recipient or no resolvable sender.
    /// </summary>
    InvalidMessage = 4,

    /// <summary>
    /// The transport rejected the credentials the account supplied.
    /// </summary>
    Authentication = 5,

    /// <summary>
    /// The transport could not be reached, or the connection failed or timed out.
    /// </summary>
    Connection = 6,

    /// <summary>
    /// The transport accepted the conversation and refused the message.
    /// </summary>
    Rejected = 7,

    /// <summary>
    /// The transport did not answer in time.
    /// </summary>
    Timeout = 8,

    /// <summary>
    /// The transport refused the message for now because a rate or quota limit was reached.
    /// </summary>
    Throttled = 9,

    /// <summary>
    /// The sending failed for a reason the transport did not classify.
    /// </summary>
    Unknown = 10
}
