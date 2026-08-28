namespace ApricotFramework.Mailer.Options;

/// <summary>
/// The sending accounts a host declares, and which one to use when a caller names none.
/// </summary>
public class MailerOptions
{
    /// <summary>
    /// The account name a sending falls back to when nothing names one.
    /// </summary>
    public const string FallbackAccountName = "default";

    /// <summary>
    /// The longest account cache lifetime that will be accepted.
    /// </summary>
    /// <remarks>
    /// A cached account holds a decrypted credential, so a long lifetime is a rotation problem rather
    /// than a performance win. The ceiling also catches the commonest way of writing this setting
    /// wrongly: <c>"30"</c> parses as thirty <i>days</i>, not thirty seconds.
    /// </remarks>
    public static TimeSpan MaximumAccountCacheLifetime { get; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the account used when a caller does not name one.
    /// </summary>
    /// <remarks>
    /// Left unset, a sending looks for an account called <see cref="FallbackAccountName"/>.
    /// </remarks>
    public string? DefaultAccount { get; set; }

    /// <summary>
    /// Gets or sets how long a resolved account stays usable before it is looked up again. Null or
    /// zero, which is the default, means no caching.
    /// </summary>
    /// <remarks>
    /// Worth setting only where accounts come from an <see cref="IMailAccountSource"/> that costs
    /// something to ask; configuration on its own needs no cache. The value is how long a rotated
    /// credential keeps being used, so keep it short. Written <c>hh:mm:ss</c>.
    /// <para>
    /// Honored by the default store the ASP.NET Core integration registers. A host that supplies its
    /// own <see cref="IMailAccountStore"/> replaces that store, and with it this setting — a store
    /// decides its own caching.
    /// </para>
    /// </remarks>
    public TimeSpan? AccountCacheLifetime { get; set; }

    /// <summary>
    /// Gets or sets the largest attachment a sending will accept, in bytes, or 0 for no limit.
    /// </summary>
    /// <remarks>
    /// Checked before transport reads the payload, and only for attachments whose length is known
    /// without reading them. Base64 encoding adds about a third on the wire, so a limit here is not
    /// the limit the receiving server applies.
    /// </remarks>
    public long MaxAttachmentBytes { get; set; }

    /// <summary>
    /// Gets the accounts, keyed by the name callers address them with.
    /// </summary>
    /// <remarks>
    /// Get-only, which is the shape the configuration binder populates.
    /// </remarks>
    public IDictionary<string, MailAccountEntry> Accounts { get; } = new Dictionary<string, MailAccountEntry>(StringComparer.OrdinalIgnoreCase);
}
