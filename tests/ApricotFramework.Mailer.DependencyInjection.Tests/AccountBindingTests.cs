using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

public class AccountBindingTests
{
    private static MailerOptions Bind(string json)
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.Json(json));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        return provider.GetRequiredService<IOptionsMonitor<MailerOptions>>().CurrentValue;
    }

    [Fact]
    public void Bind_SettingsWithMixedJsonTypes_ArriveAsText()
    {
        var options = Bind("""
            {
              "Mailer": {
                "Accounts": {
                  "default": {
                    "Transport": "stub",
                    "Settings": { "Host": "smtp.example.com", "Port": 587, "UseSsl": true }
                  }
                }
              }
            }
            """);

        var settings = options.Accounts["default"].ToAccount("default").Settings;

        Assert.Equal("smtp.example.com", settings.GetString("Host"));
        Assert.Equal(587, settings.GetInt32("Port", 0));
        Assert.True(settings.GetBoolean("UseSsl", false));
    }

    [Fact]
    public void Bind_SettingNameInAnotherCase_IsStillFound()
    {
        var options = Bind("""
            {
              "Mailer": { "Accounts": { "default": { "Transport": "stub", "Settings": { "Host": "h.example.com" } } } }
            }
            """);

        Assert.Equal("h.example.com", options.Accounts["default"].ToAccount("default").Settings.GetString("HOST"));
    }

    [Fact]
    public void Bind_AccountNameInAnotherCase_IsStillFound()
    {
        var options = Bind("""
            { "Mailer": { "Accounts": { "Billing": { "Transport": "stub" } }, "DefaultAccount": "billing" } }
            """);

        Assert.True(options.Accounts.ContainsKey("BILLING"));
    }

    [Fact]
    public void Bind_AddressAsAnObject_IsRead()
    {
        var options = Bind("""
            {
              "Mailer": { "Accounts": { "default": {
                "Transport": "stub",
                "DefaultFrom": { "Address": "no-reply@example.com", "Name": "Example" }
              } } }
            }
            """);

        var account = options.Accounts["default"].ToAccount("default");

        Assert.Equal("no-reply@example.com", account.DefaultFrom!.Address);
        Assert.Equal("Example", account.DefaultFrom.Name);
    }

    [Fact]
    public void Bind_AddressAsABareString_IsRead()
    {
        // without a type converter the binder discards this and reports nothing
        var options = Bind("""
            { "Mailer": { "Accounts": { "default": { "Transport": "stub", "DefaultFrom": "no-reply@example.com" } } } }
            """);

        var account = options.Accounts["default"].ToAccount("default");

        Assert.Equal("no-reply@example.com", account.DefaultFrom!.Address);
        Assert.Null(account.DefaultFrom.Name);
    }

    [Fact]
    public void Bind_AddressInDisplayForm_IsSplitIntoNameAndAddress()
    {
        var options = Bind("""
            { "Mailer": { "Accounts": { "default": { "Transport": "stub", "DefaultFrom": "Example <no-reply@example.com>" } } } }
            """);

        var account = options.Accounts["default"].ToAccount("default");

        Assert.Equal("no-reply@example.com", account.DefaultFrom!.Address);
        Assert.Equal("Example", account.DefaultFrom.Name);
    }

    [Fact]
    public void Bind_EnvironmentStyleKeys_MergeIntoTheSettings()
    {
        // this is how a secret stays out of appsettings.json
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.From(
            ("Mailer:Accounts:default:Transport", "stub"),
            ("Mailer:Accounts:default:Settings:Host", "smtp.example.com"),
            ("Mailer:Accounts:default:Settings:Password", "from-the-environment")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var settings = provider.GetRequiredService<IOptionsMonitor<MailerOptions>>().CurrentValue.Accounts["default"].ToAccount("default").Settings;

        Assert.Equal("from-the-environment", settings.GetString("Password"));
    }
}
