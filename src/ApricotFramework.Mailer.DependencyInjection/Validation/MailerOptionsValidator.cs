using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Mailer.DependencyInjection.Validation;

/// <summary>
/// Refuses a mail configuration that cannot send, while the host is starting.
/// </summary>
/// <remarks>
/// Accounts supplied by an <see cref="IMailAccountSource"/>, or by a host's own
/// <see cref="IMailAccountStore"/>, are not visible here. Where either is registered the checks relax:
/// a name this cannot find may still be resolvable at send time.
/// </remarks>
public class MailerOptionsValidator : IValidateOptions<MailerOptions>
{
    private readonly MailTransportRegistry transports;

    private readonly bool hasOwnResolution;

    private readonly MailerConfigurationSection? section;

    /// <summary>
    /// Creates a new instance of the validator.
    /// </summary>
    /// <param name="transports">Every registered transport.</param>
    /// <param name="sources">Every registered account source.</param>
    /// <param name="storeRegistrations">
    /// A record of any account store the host supplied through <c>AddMailAccountStore</c>. Empty
    /// otherwise.
    /// </param>
    /// <param name="section">
    /// The configuration section the options were bound from, or null when they were configured in
    /// code. Supplied so a declaration the binder silently discarded can still be reported.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="transports"/> or <paramref name="sources"/> is null.
    /// </exception>
    public MailerOptionsValidator(
        IEnumerable<IMailTransport> transports,
        IEnumerable<IMailAccountSource> sources,
        IEnumerable<MailAccountStoreRegistration> storeRegistrations,
        MailerConfigurationSection? section = null)
    {
        ArgumentNullException.ThrowIfNull(transports);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(storeRegistrations);

        this.transports = new MailTransportRegistry(transports);

        // both can resolve an account that configuration knows nothing about, and neither can be
        // asked while the host is starting
        this.hasOwnResolution = sources.Any() || storeRegistrations.Any();
        this.section = section;
    }

    /// <inheritdoc />
    public virtual ValidateOptionsResult Validate(string? name, MailerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        var registered = this.transports.GetNames();

        if (registered.Count == 0)
        {
            failures.Add("No mail transport is registered, so nothing can be sent. Add one, for example with AddSmtpMailTransport().");
        }

        if (options.Accounts.Count == 0 && !this.hasOwnResolution)
        {
            failures.Add($"No mail account is configured under '{MailerServiceCollectionExtensions.ConfigurationSectionName}:Accounts' and no account source or account store is registered, so every send would fail.");
        }

        ValidateAccountCacheLifetime(options, failures);

        if (options.MaxAttachmentBytes < 0)
        {
            failures.Add($"{nameof(MailerOptions.MaxAttachmentBytes)} cannot be negative. Use 0 for no limit.");
        }

        foreach (var (accountName, entry) in options.Accounts)
        {
            ValidateAccount(accountName, entry, registered, failures);
        }

        this.ValidateDefaultAccount(options, failures);
        this.ValidateNothingWasDiscarded(options, failures);

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Checks the account cache lifetime is a duration someone meant to write.
    /// </summary>
    /// <param name="options">The options being validated.</param>
    /// <param name="failures">Where a problem is recorded.</param>
    /// <remarks>
    /// The ceiling is what turns the commonest mistake into a startup failure: a bare number binds
    /// through <see cref="TimeSpan"/>, so <c>"30"</c> is thirty days rather than the thirty seconds
    /// it looks like.
    /// </remarks>
    private static void ValidateAccountCacheLifetime(MailerOptions options, List<string> failures)
    {
        if (options.AccountCacheLifetime is not { } lifetime)
        {
            return;
        }

        if (lifetime < TimeSpan.Zero)
        {
            failures.Add($"{nameof(MailerOptions.AccountCacheLifetime)} cannot be negative. Omit it, or use 00:00:00, to switch caching off.");

            return;
        }

        if (lifetime > MailerOptions.MaximumAccountCacheLifetime)
        {
            failures.Add($"{nameof(MailerOptions.AccountCacheLifetime)} is {lifetime}, which is longer than the maximum of {MailerOptions.MaximumAccountCacheLifetime}. Write it as hh:mm:ss — a bare number is read as a count of days.");
        }
    }

    /// <summary>
    /// Checks one declared account.
    /// </summary>
    /// <param name="accountName">The name it is declared under.</param>
    /// <param name="entry">The declaration.</param>
    /// <param name="registered">The transports that exist.</param>
    /// <param name="failures">Where a problem is recorded.</param>
    private static void ValidateAccount(string accountName, MailAccountEntry? entry, IReadOnlyCollection<string> registered, List<string> failures)
    {
        if (entry is null)
        {
            failures.Add($"Mail account '{accountName}' is declared with no settings at all.");

            return;
        }

        MailAccount account;

        try
        {
            // the mapping is what validates a transport name and both default addresses
            account = entry.ToAccount(accountName);
        }
        catch (MailAccountException e)
        {
            failures.Add(e.Message);

            return;
        }

        if (registered.Count > 0 && !registered.Contains(account.Transport, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add($"Mail account '{accountName}' uses transport '{account.Transport}', which is not registered. Registered transports: {string.Join(", ", registered)}.");
        }
    }

    /// <summary>
    /// Checks that a send naming no account has somewhere to go.
    /// </summary>
    /// <param name="options">The options being validated.</param>
    /// <param name="failures">Where a problem is recorded.</param>
    private void ValidateDefaultAccount(MailerOptions options, List<string> failures)
    {
        if (this.hasOwnResolution)
        {
            // an account source, or a host's own store, may well hold the default, and neither can be
            // asked from here
            return;
        }

        if (string.IsNullOrWhiteSpace(options.DefaultAccount))
        {
            if (!options.Accounts.ContainsKey(MailerOptions.FallbackAccountName) && options.Accounts.Count > 0)
            {
                failures.Add($"No {nameof(MailerOptions.DefaultAccount)} is set and no account is named '{MailerOptions.FallbackAccountName}', so a send that names no account would fail. Configured accounts: {string.Join(", ", options.Accounts.Keys)}.");
            }

            return;
        }

        if (!options.Accounts.ContainsKey(options.DefaultAccount.Trim()))
        {
            failures.Add($"{nameof(MailerOptions.DefaultAccount)} is '{options.DefaultAccount}', which is not a configured account. Configured accounts: {string.Join(", ", options.Accounts.Keys)}.");
        }
    }

    /// <summary>
    /// Reports an account the configuration binder dropped.
    /// </summary>
    /// <param name="options">The options as they were bound.</param>
    /// <param name="failures">Where a problem is recorded.</param>
    /// <remarks>
    /// An account declared as a bare value rather than an object binds to nothing, with no error from
    /// the binder — the account is simply absent, and every other check here would pass.
    /// </remarks>
    private void ValidateNothingWasDiscarded(MailerOptions options, List<string> failures)
    {
        if (this.section is null)
        {
            return;
        }

        foreach (var declared in this.section.Value.GetSection(nameof(MailerOptions.Accounts)).GetChildren())
        {
            if (!options.Accounts.ContainsKey(declared.Key))
            {
                failures.Add($"Mail account '{declared.Key}' is declared as a single value rather than an object, so nothing was read from it. It needs at least a Transport.");
            }
        }
    }
}
