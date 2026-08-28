using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.DependencyInjection.Impl;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

/// <summary>
/// Caching is part of the default store and is turned on by configuration alone.
/// </summary>
public class AccountCachingTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static MailAccount Account(string name = "default")
    {
        return new MailAccount { Name = name, Transport = "stub" };
    }

    private static OptionsAwareMailAccountStore CreateStore(IMailAccountSource source, IMemoryCache cache, TimeSpan? lifetime)
    {
        var options = new StaticOptionsMonitor<MailerOptions>(new MailerOptions { AccountCacheLifetime = lifetime });

        return new OptionsAwareMailAccountStore(options, cache, [source]);
    }

    [Fact]
    public async Task GetAsync_NoLifetimeConfigured_AsksEveryTimeAndCachesNothing()
    {
        var source = new CountingAccountSource(Account());
        var cache = new RecordingMemoryCache();

        var store = CreateStore(source, cache, null);

        await store.GetAsync("default", TestContext.Current.CancellationToken);
        await store.GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal(2, source.Lookups);
        Assert.Empty(cache.Entries);
    }

    [Fact]
    public async Task GetAsync_ZeroLifetime_IsTheSameAsOff()
    {
        var source = new CountingAccountSource(Account());
        var cache = new RecordingMemoryCache();

        var store = CreateStore(source, cache, TimeSpan.Zero);

        await store.GetAsync("default", TestContext.Current.CancellationToken);
        await store.GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal(2, source.Lookups);
        Assert.Empty(cache.Entries);
    }

    [Fact]
    public async Task GetAsync_SecondCall_ComesFromTheCache()
    {
        var source = new CountingAccountSource(Account());

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = CreateStore(source, cache, Lifetime);

        await store.GetAsync("default", TestContext.Current.CancellationToken);
        await store.GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal(1, source.Lookups);
    }

    [Fact]
    public async Task GetAsync_Always_WritesTheConfiguredLifetimeAndASizeOntoTheEntry()
    {
        var cache = new RecordingMemoryCache();

        await CreateStore(new CountingAccountSource(Account()), cache, Lifetime).GetAsync("default", TestContext.Current.CancellationToken);

        var entry = Assert.Single(cache.Entries);

        Assert.Equal(Lifetime, entry.Value.AbsoluteExpirationRelativeToNow);

        // a host that set a SizeLimit rejects an entry that does not declare a size
        Assert.Equal(1, entry.Value.Size);
    }

    [Fact]
    public async Task GetAsync_Always_NamespacesTheCacheKey()
    {
        var cache = new RecordingMemoryCache();

        await CreateStore(new CountingAccountSource(Account()), cache, Lifetime).GetAsync("default", TestContext.Current.CancellationToken);

        Assert.StartsWith(OptionsAwareMailAccountStore.CacheKeyPrefix, Assert.Single(cache.Entries).Key.ToString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_NameInAnotherCase_HitsTheSameEntry()
    {
        // account names are matched without regard to case; cache keys are matched exactly
        var source = new CountingAccountSource(Account());

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = CreateStore(source, cache, Lifetime);

        await store.GetAsync("default", TestContext.Current.CancellationToken);
        await store.GetAsync("DEFAULT", TestContext.Current.CancellationToken);

        Assert.Equal(1, source.Lookups);
    }

    [Fact]
    public async Task GetAsync_AccountThatDoesNotExistYet_IsNotCached()
    {
        var source = new CountingAccountSource();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = CreateStore(source, cache, Lifetime);

        await store.GetAsync("billing", TestContext.Current.CancellationToken);
        await store.GetAsync("billing", TestContext.Current.CancellationToken);

        Assert.Equal(2, source.Lookups);
    }

    [Fact]
    public async Task Invalidate_AfterAHit_MakesTheNextCallAskAgain()
    {
        var source = new CountingAccountSource(Account());

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = CreateStore(source, cache, Lifetime);

        await store.GetAsync("default", TestContext.Current.CancellationToken);
        store.Invalidate("DEFAULT");
        await store.GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal(2, source.Lookups);
    }

    [Fact]
    public async Task GetAsync_LifetimeRemovedWhileRunning_StopsServingFromTheCache()
    {
        // the point of reading the lifetime per lookup: caching can be switched off without a restart
        var source = new CountingAccountSource(Account());
        var options = new StaticOptionsMonitor<MailerOptions>(new MailerOptions { AccountCacheLifetime = Lifetime });

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new OptionsAwareMailAccountStore(options, cache, [source]);

        await store.GetAsync("default", TestContext.Current.CancellationToken);

        options.Set(new MailerOptions { AccountCacheLifetime = null });

        await store.GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal(2, source.Lookups);
    }

    [Fact]
    public void Constructor_NullArgument_Throws()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new StaticOptionsMonitor<MailerOptions>(new MailerOptions());

        Assert.Throws<ArgumentNullException>(() => new OptionsAwareMailAccountStore(null!, cache));
        Assert.Throws<ArgumentNullException>(() => new OptionsAwareMailAccountStore(options, null!));
    }

    [Fact]
    public async Task AddMailer_LifetimeFromConfiguration_IsHonouredWithNoOtherRegistration()
    {
        var source = new CountingAccountSource(Account("billing"));
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailAccountSource(source);
        services.AddMailer(Config.From(("Mailer:AccountCacheLifetime", "00:05:00")));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<IMailAccountStore>();

        await store.GetAsync("billing", TestContext.Current.CancellationToken);
        await store.GetAsync("billing", TestContext.Current.CancellationToken);

        Assert.Equal(1, source.Lookups);
    }

    [Fact]
    public async Task AddMailer_NoLifetimeConfigured_DoesNotCache()
    {
        var source = new CountingAccountSource(Account("billing"));
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailAccountSource(source);
        services.AddMailer(Config.From());

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<IMailAccountStore>();

        await store.GetAsync("billing", TestContext.Current.CancellationToken);
        await store.GetAsync("billing", TestContext.Current.CancellationToken);

        Assert.Equal(2, source.Lookups);
    }

    [Fact]
    public void AddMailer_Always_RegistersTheDefaultStore()
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.From(("Mailer:Accounts:default:Transport", "stub")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<OptionsAwareMailAccountStore>(provider.GetRequiredService<IMailAccountStore>());
    }
}
