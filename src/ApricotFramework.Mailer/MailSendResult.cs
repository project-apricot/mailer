using System.Text.Json.Serialization;

namespace ApricotFramework.Mailer;

/// <summary>
/// What came of a sending.
/// </summary>
/// <remarks>
/// A refused or undeliverable message is a result, not an exception: transient failure is the normal
/// condition of talking to a mail server, and a caller that wants to retry or record it should not
/// need a <c>catch</c>. Cancellation is the exception to that and still throws.
/// </remarks>
public sealed record MailSendResult
{
    /// <summary>
    /// Gets whether the transport accepted the message for at least one recipient.
    /// </summary>
    /// <remarks>
    /// Acceptance is not delivery. A server that queues a message reports success here and may still
    /// bounce it later, out of a band. Transport that accepts some recipients and refuses others
    /// reports success because the message did go out.
    /// </remarks>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the account the sending used.
    /// </summary>
    public required string Account { get; init; }

    /// <summary>
    /// Gets the transport that carried it, or null when resolving one failed.
    /// </summary>
    public string? Transport { get; init; }

    /// <summary>
    /// Gets the identifier the transport assigned, or null when it reported none.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Gets why the sending failed, or <see cref="MailErrorCode.None"/> when it did not.
    /// </summary>
    public MailErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Gets the detail behind <see cref="ErrorCode"/>, or null on success.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the underlying failure when there was one to keep.
    /// </summary>
    /// <remarks>
    /// For a log, not for a response. It is excluded from JSON and from <see cref="ToString"/>
    /// because a mail server's refusal often quotes the credentials it refused, and a result is a
    /// tempting thing to return straight from an endpoint.
    /// </remarks>
    [JsonIgnore]
    public Exception? Exception { get; init; }

    /// <summary>
    /// Decides whether the same sending is worth trying again.
    /// </summary>
    /// <returns>
    /// True when the failure was a condition that typically clears: a connection problem, a timeout,
    /// or a rate limit. False on success and on every failure that will recur.
    /// </returns>
    /// <remarks>
    /// <see cref="MailErrorCode.Rejected"/> is deliberately not transient — a refusal names the
    /// message or the recipient, and retrying it produces the same refusal.
    /// </remarks>
    public bool IsTransient()
    {
        return this.ErrorCode is MailErrorCode.Connection or MailErrorCode.Timeout or MailErrorCode.Throttled;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return this.Succeeded
            ? $"Sent as '{this.Account}' via '{this.Transport}'."
            : $"Send as '{this.Account}' via '{this.Transport ?? "(unresolved)"}' failed with {this.ErrorCode}.";
    }

    /// <summary>
    /// Builds the result of a sending the transport accepted.
    /// </summary>
    /// <param name="account">The account used.</param>
    /// <param name="transport">The transport that carried it.</param>
    /// <param name="messageId">The identifier the transport assigned, if any.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="account"/> or <paramref name="transport"/> is null or blank.
    /// </exception>
    public static MailSendResult Success(string account, string transport, string? messageId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);

        return new MailSendResult
        {
            Succeeded = true,
            Account = account,
            Transport = transport,
            MessageId = messageId,
        };
    }

    /// <summary>
    /// Builds the result of a sending that did not happen.
    /// </summary>
    /// <param name="errorCode">Why it failed.</param>
    /// <param name="error">The detail for a log.</param>
    /// <param name="account">The account the sending was for.</param>
    /// <param name="transport">The transport, when one had been resolved.</param>
    /// <param name="exception">The underlying failure, when there was one.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="error"/> or <paramref name="account"/> is null or blank, or
    /// <paramref name="errorCode"/> is <see cref="MailErrorCode.None"/>.
    /// </exception>
    public static MailSendResult Failure(MailErrorCode errorCode, string error, string account, string? transport = null, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(account);

        if (errorCode == MailErrorCode.None)
        {
            throw new ArgumentException("A failed send needs a reason other than None.", nameof(errorCode));
        }

        return new MailSendResult
        {
            Succeeded = false,
            Account = account,
            Transport = transport,
            ErrorCode = errorCode,
            Error = error,
            Exception = exception,
        };
    }
}
