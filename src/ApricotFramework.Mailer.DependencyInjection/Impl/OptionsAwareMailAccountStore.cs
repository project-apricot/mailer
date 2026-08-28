using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Mailer.DependencyInjection.Impl;

/// <summary>
/// The store a host gets by default: registered sources first, configuration behind them, and the
/// answer held for as long as the options say.
/// </summary>
/// <remarks>
/// Sources are asked before configuration, so a host that moves its accounts into a database or a
/// secrets manager can rotate a credential there and have it take effect — a value left behind in
/// <c>appsettings.json</c> does not outrank it. Configuration is always consulted and cannot be
/// registered away, so a host that declares its accounts there needs no source at all.
/// <para>
/// Everything is read through <see cref="IOptionsMonitor{TOptions}"/> on each lookup, so an edited
/// configuration file takes effect without a restart — including turning caching on and off.
/// </para>
/// </remarks>
public class OptionsAwareMailAccountStore : DefaultMailAccountStore
{
    /// <summary>
    /// What every cache key this store writes begins with.
    /// </summary>
    /// <remarks>
    /// The cache is the host's, shared with everything else in the process, so the keys are namespaced.
    /// </remarks>
    public const string CacheKeyPrefix = "ApricotFramework.Mailer.Account:";

    private readonly IOptionsMonitor<MailerOptions> optionsMonitor;

    private readonly IMemoryCache cache;

    /// <summary>
    /// Creates a new instance of the store.
    /// </summary>
    /// <param name="optionsMonitor">Where the configured accounts and the cache lifetime are read from.</param>
    /// <param name="cache">Where a resolved account is held, when caching is configured.</param>
    /// <param name="sources">The sources to ask before configuration, or null for none.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="optionsMonitor"/> or <paramref name="cache"/> is null.
    /// </exception>
    public OptionsAwareMailAccountStore(
        IOptionsMonitor<MailerOptions> optionsMonitor,
        IMemoryCache cache,
        IEnumerable<IMailAccountSource>? sources = null)
        : base(sources)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(cache);

        this.optionsMonitor = optionsMonitor;
        this.cache = cache;
    }

    /// <inheritdoc />
    public override async Task<MailAccount?> GetAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var lifetime = this.GetCacheLifetime();

        if (lifetime <= TimeSpan.Zero)
        {
            return await this.ResolveAsync(name, cancellationToken).ConfigureAwait(false);
        }

        var key = ToCacheKey(name);

        if (this.cache.TryGetValue<MailAccount>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var account = await this.ResolveAsync(name, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            // caching the absence would keep an account created a moment later invisible for a whole
            // lifetime
            return null;
        }

        this.cache.Set(key, account, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime,

            // a host that set a SizeLimit rejects any entry that does not declare a size
            Size = 1,
        });

        return account;
    }

    /// <summary>
    /// Drops one account from the cache, so the next send resolves it again.
    /// </summary>
    /// <param name="name">The account name.</param>
    /// <remarks>
    /// For a host that knows a credential has just been rotated and does not want to wait out the
    /// lifetime.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or blank.</exception>
    public void Invalidate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        this.cache.Remove(ToCacheKey(name));
    }

    /// <summary>
    /// Builds the cache key an account is held under.
    /// </summary>
    /// <param name="name">The account name.</param>
    /// <returns>The key.</returns>
    /// <remarks>
    /// Account names are matched without regard to case everywhere else, and cache keys are matched
    /// exactly, so the name is folded first — otherwise two spellings of one account would occupy two
    /// entries and each would be looked up separately.
    /// </remarks>
    protected static string ToCacheKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return CacheKeyPrefix + name.ToUpperInvariant();
    }

    /// <summary>
    /// Finds an account, ignoring the cache.
    /// </summary>
    /// <param name="name">The account name.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The account, or null when nothing has it.</returns>
    protected virtual async Task<MailAccount?> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        return await base.GetAsync(name, cancellationToken).ConfigureAwait(false) ?? this.FindConfigured(name);
    }

    /// <summary>
    /// Finds an account among the configured ones.
    /// </summary>
    /// <param name="name">The account name.</param>
    /// <returns>The account, or null when configuration does not declare it.</returns>
    /// <exception cref="MailAccountException">Thrown when the entry is declared but unusable.</exception>
    protected virtual MailAccount? FindConfigured(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var (declaredName, entry) in this.optionsMonitor.CurrentValue.Accounts)
        {
            // the configured spelling is carried through, not the caller's: an account is named once
            // and that is the name that should reach a log
            if (string.Equals(declaredName, name, StringComparison.OrdinalIgnoreCase))
            {
                return entry.ToAccount(declaredName);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets how long an account may be held, as configured right now.
    /// </summary>
    /// <returns>The lifetime, or <see cref="TimeSpan.Zero"/> when caching is off.</returns>
    protected virtual TimeSpan GetCacheLifetime()
    {
        return this.optionsMonitor.CurrentValue.AccountCacheLifetime ?? TimeSpan.Zero;
    }
}
