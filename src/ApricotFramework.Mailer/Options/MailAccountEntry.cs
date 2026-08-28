namespace ApricotFramework.Mailer.Options;

/// <summary>
/// One sending an account as it is declared in configuration.
/// </summary>
/// <remarks>
/// Shaped for the configuration binder — mutable, everything optional — and mapped onto the immutable
/// <see cref="MailAccount"/> by <see cref="ToAccount"/>. Kept separate from it so the domain type is
/// not held to what a binder can populate.
/// </remarks>
public class MailAccountEntry
{
    /// <summary>
    /// Gets or sets the name of the transport that carries the mail, such as <c>smtp</c>.
    /// </summary>
    public string? Transport { get; set; }

    /// <summary>
    /// Gets or sets the sender used when a message does not set one.
    /// </summary>
    public MailAddressEntry? DefaultFrom { get; set; }

    /// <summary>
    /// Gets or sets where replies go when a message does not say.
    /// </summary>
    public MailAddressEntry? DefaultReplyTo { get; set; }

    /// <summary>
    /// Gets the settings the transport reads, such as a host and credentials.
    /// </summary>
    /// <remarks>
    /// Get-only, which is the shape the configuration binder populates. Values are read as text
    /// whatever they look like in the file, so a JSON number arrives as <c>"587"</c>. The section
    /// must be flat: the binder drops a nested object under it without complaint.
    /// </remarks>
    public IDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps this entry onto an account.
    /// </summary>
    /// <param name="name">The name of the entry is keyed by.</param>
    /// <returns>The account.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or blank.</exception>
    /// <exception cref="MailAccountException">
    /// Thrown when the entry names no transport, or either default address is unusable.
    /// </exception>
    public virtual MailAccount ToAccount(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (string.IsNullOrWhiteSpace(this.Transport))
        {
            throw new MailAccountException($"Mail account '{name}' does not name a transport.");
        }

        return new MailAccount
        {
            Name = name,
            Transport = this.Transport.Trim(),
            DefaultFrom = ToAddress(this.DefaultFrom, name, nameof(this.DefaultFrom)),
            DefaultReplyTo = ToAddress(this.DefaultReplyTo, name, nameof(this.DefaultReplyTo)),
            Settings = new MailAccountSettings(new Dictionary<string, string?>(this.Settings, StringComparer.OrdinalIgnoreCase), name),
        };
    }

    /// <summary>
    /// Maps a declared address onto a validated one.
    /// </summary>
    /// <param name="entry">The declared address, or null when none was declared.</param>
    /// <param name="accountName">The owning account, for the failure message.</param>
    /// <param name="propertyName">Which address this is, for the failure message?</param>
    /// <returns>The address, or null when none was declared.</returns>
    private static EmailAddress? ToAddress(MailAddressEntry? entry, string accountName, string propertyName)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Address))
        {
            return null;
        }

        if (!EmailAddress.TryParse(entry.Address, entry.Name, out var address))
        {
            throw new MailAccountException($"The {propertyName} address configured for mail account '{accountName}' is not a usable mailbox.");
        }

        return address;
    }
}
