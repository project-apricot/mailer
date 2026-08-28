using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.DependencyInjection.Impl;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

public class MailerServiceCollectionExtensionsTests
{
    private static (string Key, string? Value)[] OneSmtpAccount(string accountName = "default") =>
    [
        ($"Mailer:Accounts:{accountName}:Transport", "stub"),
        ($"Mailer:Accounts:{accountName}:DefaultFrom:Address", "no-reply@example.com"),
        ("Mailer:DefaultAccount", accountName),
    ];

    [Fact]
    public void AddMailer_WithConfiguration_RegistersASingletonMailer()
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.From(OneSmtpAccount()));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<OptionsAwareMailer>(provider.GetRequiredService<IMailer>());
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, d => d.ServiceType == typeof(IMailer)).Lifetime);
    }

    [Fact]
    public void AddMailer_WithAnAction_RegistersTheSameServices()
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(options =>
        {
            options.Accounts["default"] = new MailAccountEntry { Transport = "stub" };
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IMailer>());
    }

    [Fact]
    public void AddMailer_WithANamedSection_BindsThatSectionInstead()
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.From(("Outbound:Accounts:default:Transport", "stub")), "Outbound");

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.True(provider.GetRequiredService<IOptions<MailerOptions>>().Value.Accounts.ContainsKey("default"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddMailTransport_EitherRegistrationOrder_IsPickedUp(bool transportFirst)
    {
        var services = new ServiceCollection();

        if (transportFirst)
        {
            services.AddMailTransport<StubTransport>();
            services.AddMailer(Config.From(OneSmtpAccount()));
        }
        else
        {
            services.AddMailer(Config.From(OneSmtpAccount()));
            services.AddMailTransport<StubTransport>();
        }

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IOptions<MailerOptions>>().Value);
        Assert.NotNull(provider.GetRequiredService<IMailer>());
    }

    [Fact]
    public void AddMailTransport_SameTypeTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailTransport<StubTransport>();

        Assert.Single(services, d => d.ServiceType == typeof(IMailTransport));
    }

    [Fact]
    public void AddMailTransport_TwoTypesUnderOneName_FailsWhenTheMailerIsResolved()
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailTransport<OtherStubTransport>();
        services.AddMailer(Config.From(OneSmtpAccount()));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        // letting registration order decide which one sends would be worse than refusing
        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<IMailer>());
    }

    [Fact]
    public void AddMailAccountSource_SameTypeTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        services.AddMailAccountSource<EmptyAccountSource>();
        services.AddMailAccountSource<EmptyAccountSource>();

        Assert.Single(services, d => d.ServiceType == typeof(IMailAccountSource));
    }

    [Fact]
    public void AddMemoryMailTransport_Always_RegistersOneInstanceUnderBothServiceTypes()
    {
        var services = new ServiceCollection();

        services.AddMemoryMailTransport();
        services.AddMailer(Config.From(("Mailer:Accounts:default:Transport", "memory")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var byConcreteType = provider.GetRequiredService<Mailer.Impl.MemoryMailTransport>();
        var byInterface = Assert.Single(provider.GetServices<IMailTransport>());

        // the point of the method: what a send writes to is what a test can read back
        Assert.Same(byConcreteType, byInterface);
    }

    [Fact]
    public void AddMemoryMailTransport_CalledTwice_StillRegistersOne()
    {
        var services = new ServiceCollection();

        services.AddMemoryMailTransport();
        services.AddMemoryMailTransport();
        services.AddMailer(Config.From(("Mailer:Accounts:default:Transport", "memory")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        // two would answer to the same name and the registry would refuse to resolve at all
        Assert.Same(provider.GetRequiredService<Mailer.Impl.MemoryMailTransport>(), Assert.Single(provider.GetServices<IMailTransport>()));
        Assert.NotNull(provider.GetRequiredService<IMailer>());
    }

    [Fact]
    public async Task AddMemoryMailTransport_AfterASend_TheInjectedInstanceHasTheMessage()
    {
        var services = new ServiceCollection();

        services.AddMemoryMailTransport();
        services.AddMailer(Config.From(
            ("Mailer:Accounts:default:Transport", "memory"),
            ("Mailer:Accounts:default:DefaultFrom", "no-reply@example.com")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        await provider.GetRequiredService<IMailer>().SendAsync(
            new EmailMessage
            {
                Subject = "Welcome",
                Body = EmailBody.FromText("hello"),
                To = [EmailAddress.Parse("recipient@example.com")],
            },
            TestContext.Current.CancellationToken);

        var captured = Assert.Single(provider.GetRequiredService<Mailer.Impl.MemoryMailTransport>().GetSentMessages());

        Assert.Equal("Welcome", captured.Message.Subject);
    }

    [Fact]
    public void AddMemoryMailTransport_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddMemoryMailTransport());
    }

    [Fact]
    public void AddMailAccountSource_TwoDifferentTypes_RegistersBoth()
    {
        // TryAddEnumerable de-duplicates by implementation type, not by service type, so it adds to
        // the set rather than guarding it
        var services = new ServiceCollection();

        services.AddMailAccountSource<EmptyAccountSource>();
        services.AddMailAccountSource<SecondEmptyAccountSource>();

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IMailAccountSource)));
    }

    [Fact]
    public async Task AddMailAccountSource_SeveralSources_AreAskedInRegistrationOrder()
    {
        var first = new CountingAccountSource(new MailAccount { Name = "shared", Transport = "first" });
        var second = new CountingAccountSource(
            new MailAccount { Name = "shared", Transport = "second" },
            new MailAccount { Name = "onlyInSecond", Transport = "stub" });

        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailAccountSource(first);
        services.AddMailAccountSource(second);
        services.AddMailer(Config.From());

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<IMailAccountStore>();

        // the first source that has the name wins
        Assert.Equal("first", (await store.GetAsync("shared", TestContext.Current.CancellationToken))!.Transport);

        // and a name it does not have falls through to the next
        Assert.NotNull(await store.GetAsync("onlyInSecond", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AddMailAccountSource_Instance_IsRegisteredAsASingleton()
    {
        var services = new ServiceCollection();

        services.AddMailAccountSource(new StubAccountSource());

        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, d => d.ServiceType == typeof(IMailAccountSource)).Lifetime);
    }

    [Fact]
    public void AddMailer_HostRegistersItsOwnMailer_KeepsTheHostsOne()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IMailer, StubMailer>();
        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.From(OneSmtpAccount()));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<StubMailer>(provider.GetRequiredService<IMailer>());
    }

    [Fact]
    public void AddMailer_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddMailer(Config.From()));
    }

    [Fact]
    public void AddMailer_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddMailer((Microsoft.Extensions.Configuration.IConfiguration)null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddMailer_BlankSectionName_Throws(string sectionName)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddMailer(Config.From(), sectionName));
    }

    /// <summary>
    /// A source that has nothing, to prove de-duplication by type.
    /// </summary>
    private sealed class EmptyAccountSource : IMailAccountSource
    {
        public Task<MailAccount?> FindAsync(string name, CancellationToken cancellationToken)
        {
            return Task.FromResult<MailAccount?>(null);
        }
    }

    /// <summary>
    /// A second source type, to prove a different one is an addition rather than a duplicate.
    /// </summary>
    private sealed class SecondEmptyAccountSource : IMailAccountSource
    {
        public Task<MailAccount?> FindAsync(string name, CancellationToken cancellationToken)
        {
            return Task.FromResult<MailAccount?>(null);
        }
    }

    /// <summary>
    /// A mailer a host might register in place of this library's.
    /// </summary>
    private sealed class StubMailer : IMailer
    {
        public Task<MailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("never called; the test only checks which type was resolved");
        }

        public Task<MailSendResult> SendAsync(string accountName, EmailMessage message, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("never called; the test only checks which type was resolved");
        }
    }
}
