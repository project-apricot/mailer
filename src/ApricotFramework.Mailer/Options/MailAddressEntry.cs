using System.ComponentModel;

namespace ApricotFramework.Mailer.Options;

/// <summary>
/// A mailbox as it is declared in configuration.
/// </summary>
/// <remarks>
/// Declarable either as an object with <c>Address</c> and <c>Name</c>, or as a single string —
/// <c>"no-reply@example.com"</c> or <c>"Example &lt;no-reply@example.com&gt;"</c>.
/// </remarks>
[TypeConverter(typeof(MailAddressEntryConverter))]
public class MailAddressEntry
{
    /// <summary>
    /// Gets or sets the mailbox address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the display name, if any.
    /// </summary>
    public string? Name { get; set; }
}
