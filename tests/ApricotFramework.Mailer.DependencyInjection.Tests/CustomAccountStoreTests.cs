using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.DependencyInjection.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

/// <summary>
/// How a host's own account store composes with the cache and with configured accounts.
/// </summary>
public class CustomAccountStoreTests
{
    private const string CachingConfiguration = "00:05:00";

    private static MailAccount Account(string name)
    {
        return new MailAccount { Name = name, Transport = "stub" };
    }

    private static ServiceProvider Build(Action<IServiceCollection> register, params (string Key, string? Value)[] configuration)
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        register(services);
        services.AddMailer(Config.From(configuration));

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddMailAccountStore_Always_ReplacesTheDefaultStore()
    {
        var store = new CountingAccountStore(Account("db"));

        using var provider = Build(services => services.AddMailAccountStore(store));

        Assert.Same(store, provider.GetRequiredService<IMailAccountStore>());
    }

    [Fact]
    public async Task AddMailAccountStore_WithACacheLifetimeConfigured_StillDoesNotCache()
    {
        // a store owns its own caching; ours is part of the store it replaced, not a wrapper round it
        var store = new CountingAccountStore(Account("db"));

        using var provider = Build(
            services => services.AddMailAccountStore(store),
            ("Mailer:AccountCacheLifetime", CachingConfiguration));

        var resolved = provider.GetRequiredService<IMailAccountStore>();

        await resolved.GetAsync("db", TestContext.Current.CancellationToken);
        await resolved.GetAsync("db", TestContext.Current.CancellationToken);

        Assert.Equal(2, store.Lookups);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddMailAccountStore_EitherRegistrationOrder_IsUsed(bool storeFirst)
    {
        var store = new CountingAccountStore(Account("db"));
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();

        if (storeFirst)
        {
            services.AddMailAccountStore(store);
            services.AddMailer(Config.From());
        }
        else
        {
            services.AddMailer(Config.From());
            services.AddMailAccountStore(store);
        }

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(await provider.GetRequiredService<IMailAccountStore>().GetAsync("db", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddMailAccountStore_Always_TakesOverResolutionFromConfiguration()
    {
        // replacing the store gives up the configured accounts; a source would have kept them
        using var provider = Build(
            services => services.AddMailAccountStore(new CountingAccountStore(Account("db"))),
            ("Mailer:Accounts:configured:Transport", "stub"));

        var resolved = provider.GetRequiredService<IMailAccountStore>();

        Assert.NotNull(await resolved.GetAsync("db", TestContext.Current.CancellationToken));
        Assert.Null(await resolved.GetAsync("configured", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AddMailAccountStore_OfT_IsResolvedFromTheContainer()
    {
        using var provider = Build(services =>
        {
            services.AddSingleton(new CountingAccountStore(Account("db")));
            services.AddMailAccountStore<DelegatingAccountStore>();
        });

        Assert.IsType<DelegatingAccountStore>(provider.GetRequiredService<IMailAccountStore>());
    }

    [Fact]
    public async Task SendAsync_WithACustomStore_ResolvesThroughIt()
    {
        var transport = new Mailer.Impl.MemoryMailTransport();
        var account = new MailAccount
        {
            Name = "db",
            Transport = Mailer.Impl.MemoryMailTransport.TransportName,
            DefaultFrom = EmailAddress.Parse("no-reply@example.com"),
        };

        var services = new ServiceCollection();

        services.AddMailTransport(transport);
        services.AddMailAccountStore(new CountingAccountStore(account));
        services.AddMailer(Config.From(("Mailer:DefaultAccount", "db")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var result = await provider.GetRequiredService<IMailer>().SendAsync(
            new EmailMessage
            {
                Subject = "Welcome",
                Body = EmailBody.FromText("hello"),
                To = [EmailAddress.Parse("recipient@example.com")],
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("db", result.Account);
    }

    [Fact]
    public void RegisteringTheStoreDirectly_WorksTheSameWay()
    {
        // AddMailAccountStore is the discoverable spelling, not a different mechanism
        var store = new CountingAccountStore(Account("db"));
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddSingleton<IMailAccountStore>(store);
        services.AddMailer(Config.From(("Mailer:Accounts:configured:Transport", "stub"), ("Mailer:DefaultAccount", "configured")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Same(store, provider.GetRequiredService<IMailAccountStore>());
    }

    [Fact]
    public void AddMailAccountStore_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddMailAccountStore(null!));
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddMailAccountStore<DelegatingAccountStore>());
    }

    /// <summary>
    /// A store the container builds from another registration, to prove the generic overload resolves.
    /// </summary>
    private sealed class DelegatingAccountStore(CountingAccountStore inner) : IMailAccountStore
    {
        public Task<MailAccount?> GetAsync(string name, CancellationToken cancellationToken)
        {
            return inner.GetAsync(name, cancellationToken);
        }
    }
}
