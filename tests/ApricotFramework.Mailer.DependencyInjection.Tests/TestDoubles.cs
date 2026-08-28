using ApricotFramework.Mailer;
using Microsoft.Extensions.Configuration;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

/// <summary>
/// Builds configuration in memory, using the same key separator a JSON file would produce.
/// </summary>
internal static class Config
{
    public static IConfiguration From(params (string Key, string? Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
    }

    public static IConfiguration Json(string json)
    {
        return new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
    }
}

/// <summary>
/// A transport that answers to a chosen name and records nothing.
/// </summary>
internal sealed class StubTransport(string name = "stub") : IMailTransport
{
    public string Name { get; } = name;

    public Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        return Task.FromResult(MailSendResult.Success(account.Name, this.Name));
    }
}

/// <summary>
/// A second transport type answering to the same name, to prove a clash is refused.
/// </summary>
internal sealed class OtherStubTransport : IMailTransport
{
    public string Name => "stub";

    public Task<MailSendResult> SendAsync(EmailMessage message, MailAccount account, CancellationToken cancellationToken)
    {
        return Task.FromResult(MailSendResult.Success(account.Name, this.Name));
    }
}

/// <summary>
/// An account source standing in for a database or a secrets manager.
/// </summary>
internal sealed class StubAccountSource(params MailAccount[] accounts) : IMailAccountSource
{
    private readonly Dictionary<string, MailAccount> accounts =
        accounts.ToDictionary(account => account.Name, StringComparer.OrdinalIgnoreCase);

    public Task<MailAccount?> FindAsync(string name, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.accounts.GetValueOrDefault(name));
    }
}

/// <summary>
/// An account store that answers from a fixed set and counts how often it is asked.
/// </summary>
internal sealed class CountingAccountStore(params MailAccount[] accounts) : IMailAccountStore
{
    private readonly Dictionary<string, MailAccount> accounts =
        accounts.ToDictionary(account => account.Name, StringComparer.OrdinalIgnoreCase);

    public int Lookups { get; private set; }

    public Task<MailAccount?> GetAsync(string name, CancellationToken cancellationToken)
    {
        this.Lookups++;

        return Task.FromResult(this.accounts.GetValueOrDefault(name));
    }
}

/// <summary>
/// An account source that counts how often it is asked, standing in for one that costs something.
/// </summary>
internal sealed class CountingAccountSource(params MailAccount[] accounts) : IMailAccountSource
{
    private readonly Dictionary<string, MailAccount> accounts =
        accounts.ToDictionary(account => account.Name, StringComparer.OrdinalIgnoreCase);

    public int Lookups { get; private set; }

    public Task<MailAccount?> FindAsync(string name, CancellationToken cancellationToken)
    {
        this.Lookups++;

        return Task.FromResult(this.accounts.GetValueOrDefault(name));
    }
}

/// <summary>
/// An options monitor whose value the test replaces, standing in for a configuration reload.
/// </summary>
internal sealed class StaticOptionsMonitor<TOptions>(TOptions value) : Microsoft.Extensions.Options.IOptionsMonitor<TOptions>
{
    public TOptions CurrentValue { get; private set; } = value;

    public TOptions Get(string? name)
    {
        return this.CurrentValue;
    }

    public IDisposable? OnChange(Action<TOptions, string?> listener)
    {
        // nothing here reloads through the change token; Set is what a test uses instead
        return null;
    }

    public void Set(TOptions value)
    {
        this.CurrentValue = value;
    }
}

/// <summary>
/// A memory cache that records what was written, so an entry's expiry and size can be asserted
/// without waiting for a clock.
/// </summary>
internal sealed class RecordingMemoryCache : Microsoft.Extensions.Caching.Memory.IMemoryCache
{
    public Dictionary<object, Microsoft.Extensions.Caching.Memory.ICacheEntry> Entries { get; } = [];

    public Microsoft.Extensions.Caching.Memory.ICacheEntry CreateEntry(object key)
    {
        var entry = new RecordingCacheEntry(key, this.Entries);

        return entry;
    }

    public void Remove(object key)
    {
        this.Entries.Remove(key);
    }

    public bool TryGetValue(object key, out object? value)
    {
        if (this.Entries.TryGetValue(key, out var entry))
        {
            value = entry.Value;

            return true;
        }

        value = null;

        return false;
    }

    public void Dispose()
    {
        // nothing to release; the entries outlive the cache on purpose
    }

    private sealed class RecordingCacheEntry(object key, Dictionary<object, Microsoft.Extensions.Caching.Memory.ICacheEntry> entries)
        : Microsoft.Extensions.Caching.Memory.ICacheEntry
    {
        public object Key { get; } = key;

        public object? Value { get; set; }

        public DateTimeOffset? AbsoluteExpiration { get; set; }

        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        public TimeSpan? SlidingExpiration { get; set; }

        public IList<Microsoft.Extensions.Primitives.IChangeToken> ExpirationTokens { get; } = [];

        public IList<Microsoft.Extensions.Caching.Memory.PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = [];

        public Microsoft.Extensions.Caching.Memory.CacheItemPriority Priority { get; set; }

        public long? Size { get; set; }

        public void Dispose()
        {
            // the entry is committed on dispose, which is what IMemoryCache.Set does
            entries[this.Key] = this;
        }
    }
}
