using ApricotFramework.Mailer.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using ApricotFramework.Mailer.DependencyInjection.Impl;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

public class OptionsAwareMailAccountStoreTests
{
    private static MailAccount FromSource(string name = "default")
    {
        return new MailAccount
        {
            Name = name,
            Transport = "stub",
            Settings = new MailAccountSettings(new Dictionary<string, string?> { ["Origin"] = "source" }, name),
        };
    }

    private static ServiceProvider Build(bool withSource, params (string Key, string? Value)[] configuration)
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();

        if (withSource)
        {
            services.AddMailAccountSource(new StubAccountSource(FromSource()));
        }

        services.AddMailer(Config.From(configuration));

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task GetAsync_ConfiguredOnly_ReadsConfiguration()
    {
        using var provider = Build(
            withSource: false,
            ("Mailer:Accounts:default:Transport", "stub"),
            ("Mailer:Accounts:default:Settings:Origin", "configuration"));

        var account = await provider.GetRequiredService<IMailAccountStore>().GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal("configuration", account!.Settings.GetString("Origin"));
    }

    [Fact]
    public async Task GetAsync_DeclaredInBoth_TakesTheSource()
    {
        // a credential rotated in a database or a vault must not be outranked by a stale value left
        // behind in appsettings.json
        using var provider = Build(
            withSource: true,
            ("Mailer:Accounts:default:Transport", "stub"),
            ("Mailer:Accounts:default:Settings:Origin", "configuration"));

        var account = await provider.GetRequiredService<IMailAccountStore>().GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal("source", account!.Settings.GetString("Origin"));
    }

    [Fact]
    public async Task GetAsync_OnlyInConfigurationWhileASourceIsRegistered_StillFallsBackToConfiguration()
    {
        // registering a source must not make configuration unreachable
        using var provider = Build(
            withSource: true,
            ("Mailer:Accounts:billing:Transport", "stub"),
            ("Mailer:Accounts:billing:Settings:Origin", "configuration"));

        var account = await provider.GetRequiredService<IMailAccountStore>().GetAsync("billing", TestContext.Current.CancellationToken);

        Assert.Equal("configuration", account!.Settings.GetString("Origin"));
    }

    [Fact]
    public async Task GetAsync_UnknownName_ReturnsNull()
    {
        using var provider = Build(withSource: false, ("Mailer:Accounts:default:Transport", "stub"));

        Assert.Null(await provider.GetRequiredService<IMailAccountStore>().GetAsync("nothing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_NameInAnotherCase_KeepsTheConfiguredSpelling()
    {
        // an account is named once, in configuration, and that is the name that should reach a log
        using var provider = Build(withSource: false, ("Mailer:Accounts:Billing:Transport", "stub"), ("Mailer:DefaultAccount", "Billing"));

        var account = await provider.GetRequiredService<IMailAccountStore>().GetAsync("BILLING", TestContext.Current.CancellationToken);

        Assert.Equal("Billing", account!.Name);
    }

    [Fact]
    public async Task GetAsync_ConfigurationChangesAfterStartup_IsPickedUp()
    {
        var source = new ConfigurationManager();

        ((IConfigurationBuilder)source).AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mailer:Accounts:default:Transport"] = "stub",
            ["Mailer:Accounts:default:Settings:Origin"] = "before",
        });

        var services = new ServiceCollection();
        services.AddMailTransport<StubTransport>();
        services.AddMailer(source);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<IMailAccountStore>();

        Assert.Equal("before", (await store.GetAsync("default", TestContext.Current.CancellationToken))!.Settings.GetString("Origin"));

        source["Mailer:Accounts:default:Settings:Origin"] = "after";

        // setting a value does not raise a change token on its own; a reload is what a file watcher
        // would have triggered
        ((IConfigurationRoot)source).Reload();

        Assert.Equal("after", (await store.GetAsync("default", TestContext.Current.CancellationToken))!.Settings.GetString("Origin"));
    }
}
