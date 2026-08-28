using System.Diagnostics.CodeAnalysis;

namespace ApricotFramework.Mailer;

/// <summary>
/// One mailbox: an address, and optionally the name to display beside it.
/// </summary>
/// <remarks>
/// Created only through <see cref="Parse(string, string?)"/> or <see cref="TryParse(string?, string?, out EmailAddress?)"/>,
/// so an instance is always safe to write into a header. Equality is ordinal on both members, which
/// means two spellings of the same mailbox differing in case are not equal.
/// </remarks>
public sealed record EmailAddress
{
    /// <summary>
    /// The longest address this accepts, which is the limit RFC 5321 puts on a reverse-path.
    /// </summary>
    public const int MaximumAddressLength = 254;

    /// <summary>
    /// The longest local part this accepts, which is the limit RFC 5321 puts on one.
    /// </summary>
    public const int MaximumLocalPartLength = 64;

    private EmailAddress(string address, string? name)
    {
        this.Address = address;
        this.Name = name;
    }

    /// <summary>
    /// Gets the mailbox address, in its original spelling.
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// Gets the display name or null when the mailbox has none.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Creates a mailbox from an address and an optional display name.
    /// </summary>
    /// <param name="address">
    /// The bare mailbox address, such as <c>someone@example.com</c>. The
    /// <c>Name &lt;someone@example.com&gt;</c> display form is not accepted; pass the name separately.
    /// </param>
    /// <param name="name">The display name, or null for none.</param>
    /// <returns>The mailbox.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the address or the name is unusable. The message says which rule failed and never
    /// echoes the value.
    /// </exception>
    public static EmailAddress Parse(string address, string? name = null)
    {
        if (!TryParse(address, name, out var result))
        {
            throw new FormatException($"'{nameof(address)}' is not a usable mailbox address: {Explain(address, name)}");
        }

        return result;
    }

    /// <summary>
    /// Tries to create a mailbox from an address.
    /// </summary>
    /// <param name="address">The bare mailbox address.</param>
    /// <param name="result">The mailbox, or null when the address is unusable.</param>
    /// <returns>True when the address is usable.</returns>
    public static bool TryParse(string? address, [NotNullWhen(true)] out EmailAddress? result)
    {
        return TryParse(address, null, out result);
    }

    /// <summary>
    /// Tries to create a mailbox from an address and a display name.
    /// </summary>
    /// <param name="address">The bare mailbox address.</param>
    /// <param name="name">The display name, or null for none.</param>
    /// <param name="result">The mailbox, or null when the address or the name is unusable.</param>
    /// <returns>True when both are usable.</returns>
    /// <remarks>
    /// The checks are structural rather than a full RFC 5322 grammar: one <c>@</c> with a non-empty
    /// local part and a dotted domain, within the length limits, and no white space or control
    /// character anywhere. Rejecting control characters is what stops a crafted name or address from
    /// injecting a header.
    /// </remarks>
    public static bool TryParse(string? address, string? name, [NotNullWhen(true)] out EmailAddress? result)
    {
        result = null;

        if (Explain(address, name) is not null)
        {
            return false;
        }

        result = new EmailAddress(address!.Trim(), string.IsNullOrWhiteSpace(name) ? null : name.Trim());

        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return this.Name is null ? this.Address : $"{this.Name} <{this.Address}>";
    }

    /// <summary>
    /// Names the first rule an address and name pair breaks.
    /// </summary>
    /// <param name="address">The candidate address.</param>
    /// <param name="name">The candidate display name.</param>
    /// <returns>The reason it is unusable, or null when it is usable.</returns>
    private static string? Explain(string? address, string? name)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return "it is empty.";
        }

        if (name is not null && ContainsControlCharacter(name))
        {
            return "the display name contains a control character.";
        }

        var candidate = address.Trim();

        if (candidate.Length > MaximumAddressLength)
        {
            return $"it is longer than {MaximumAddressLength} characters.";
        }

        if (ContainsControlCharacter(candidate) || candidate.Any(char.IsWhiteSpace))
        {
            return "it contains white space or a control character.";
        }

        var at = candidate.IndexOf('@');

        if (at < 0 || at != candidate.LastIndexOf('@'))
        {
            return "it does not have exactly one '@'.";
        }

        var local = candidate[..at];
        var domain = candidate[(at + 1)..];

        if (local.Length == 0)
        {
            return "it has no local part before the '@'.";
        }

        if (local.Length > MaximumLocalPartLength)
        {
            return $"its local part is longer than {MaximumLocalPartLength} characters.";
        }

        if (domain.Length == 0)
        {
            return "it has no domain after the '@'.";
        }

        // a domain of bare labels is legal in SMTP but never routable from the public internet, and
        // accepting one hides a truncated address
        if (!domain.Contains('.', StringComparison.Ordinal) || domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains("..", StringComparison.Ordinal))
        {
            return "its domain is not a dotted name.";
        }

        return null;
    }

    /// <summary>
    /// Decides whether a value carries a character that must never reach a header.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>True when the value contains a control character.</returns>
    private static bool ContainsControlCharacter(string value)
    {
        return value.Any(char.IsControl);
    }
}
