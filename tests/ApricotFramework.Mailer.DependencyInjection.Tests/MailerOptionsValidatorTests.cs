using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.DependencyInjection.Validation;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

public class MailerOptionsValidatorTests
{
    private static MailerOptionsValidator CreateValidator(bool withTransport = true, bool withSource = false, bool withStore = false)
    {
        return new MailerOptionsValidator(
            withTransport ? [new StubTransport()] : [],
            withSource ? [new StubAccountSource()] : [],
            withStore ? [new MailAccountStoreRegistration(typeof(CountingAccountStore))] : []);
    }

    private static MailerOptions OneAccount(string name = "default", string transport = "stub")
    {
        var options = new MailerOptions();

        options.Accounts[name] = new MailAccountEntry { Transport = transport };

        return options;
    }

    private static string Failure(ValidateOptionsResult result)
    {
        Assert.True(result.Failed);

        return string.Join(" | ", result.Failures ?? []);
    }

    [Fact]
    public void Validate_OneUsableAccount_Succeeds()
    {
        Assert.True(CreateValidator().Validate(null, OneAccount()).Succeeded);
    }

    [Fact]
    public void Validate_NoTransportRegistered_Fails()
    {
        var message = Failure(CreateValidator(withTransport: false).Validate(null, OneAccount()));

        Assert.Contains("No mail transport is registered", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoAccountsAndNoSources_Fails()
    {
        var message = Failure(CreateValidator().Validate(null, new MailerOptions()));

        Assert.Contains("No mail account is configured", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoAccountsButASourceIsRegistered_Succeeds()
    {
        // a source may hold every account, and cannot be asked while the host is starting
        Assert.True(CreateValidator(withSource: true).Validate(null, new MailerOptions()).Succeeded);
    }

    [Fact]
    public void Validate_NoAccountsButAHostsOwnStoreIsRegistered_Succeeds()
    {
        // a store replaces resolution outright, so configuration having nothing is expected
        Assert.True(CreateValidator(withStore: true).Validate(null, new MailerOptions()).Succeeded);
    }

    [Fact]
    public void Validate_NoDefaultAccountButAHostsOwnStoreIsRegistered_Succeeds()
    {
        Assert.True(CreateValidator(withStore: true).Validate(null, OneAccount("primary")).Succeeded);
    }

    [Fact]
    public void Validate_AccountWithNoTransport_Fails()
    {
        var options = new MailerOptions();
        options.Accounts["default"] = new MailAccountEntry();

        Assert.Contains("does not name a transport", Failure(CreateValidator().Validate(null, options)), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AccountNamingAnUnregisteredTransport_FailsListingWhatExists()
    {
        var message = Failure(CreateValidator().Validate(null, OneAccount(transport: "sendgrid")));

        Assert.Contains("sendgrid", message, StringComparison.Ordinal);
        Assert.Contains("stub", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AccountWithAnUnusableDefaultFrom_Fails()
    {
        var options = new MailerOptions();
        options.Accounts["default"] = new MailAccountEntry
        {
            Transport = "stub",
            DefaultFrom = new MailAddressEntry { Address = "not-an-email" },
        };

        Assert.Contains("DefaultFrom", Failure(CreateValidator().Validate(null, options)), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DefaultAccountNamingNothing_Fails()
    {
        var options = OneAccount();
        options.DefaultAccount = "billing";

        Assert.Contains("billing", Failure(CreateValidator().Validate(null, options)), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoDefaultAccountAndNoneCalledDefault_Fails()
    {
        // the send-time fallback is the literal name "default", so this would fail on the first mail
        var message = Failure(CreateValidator().Validate(null, OneAccount("primary")));

        Assert.Contains("primary", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoDefaultAccountButOneCalledDefault_Succeeds()
    {
        Assert.True(CreateValidator().Validate(null, OneAccount()).Succeeded);
    }

    [Fact]
    public void Validate_NegativeAttachmentCeiling_Fails()
    {
        var options = OneAccount();
        options.MaxAttachmentBytes = -1;

        Assert.Contains(nameof(MailerOptions.MaxAttachmentBytes), Failure(CreateValidator().Validate(null, options)), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoCacheLifetime_Succeeds()
    {
        Assert.True(CreateValidator().Validate(null, OneAccount()).Succeeded);
    }

    [Fact]
    public void Validate_ZeroCacheLifetime_Succeeds()
    {
        var options = OneAccount();
        options.AccountCacheLifetime = TimeSpan.Zero;

        Assert.True(CreateValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Validate_ReasonableCacheLifetime_Succeeds()
    {
        var options = OneAccount();
        options.AccountCacheLifetime = TimeSpan.FromMinutes(5);

        Assert.True(CreateValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Validate_NegativeCacheLifetime_Fails()
    {
        var options = OneAccount();
        options.AccountCacheLifetime = TimeSpan.FromSeconds(-1);

        Assert.Contains("cannot be negative", Failure(CreateValidator().Validate(null, options)), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CacheLifetimeWrittenAsABareNumber_Fails()
    {
        // "30" binds through TimeSpan as thirty days, which is the mistake this ceiling exists to catch
        var options = OneAccount();
        options.AccountCacheLifetime = TimeSpan.FromDays(30);

        Assert.Contains("hh:mm:ss", Failure(CreateValidator().Validate(null, options)), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CacheLifetimeAtTheCeiling_Succeeds()
    {
        var options = OneAccount();
        options.AccountCacheLifetime = MailerOptions.MaximumAccountCacheLifetime;

        Assert.True(CreateValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Validate_SeveralProblems_ReportsThemAllAtOnce()
    {
        var options = OneAccount(transport: "sendgrid");
        options.DefaultAccount = "billing";

        Assert.Equal(2, CreateValidator().Validate(null, options).Failures!.Count());
    }

    [Fact]
    public void Validate_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateValidator().Validate(null, null!));
    }

    [Fact]
    public void Options_ResolvedWithAMisconfiguredAccount_ThrowsOnAccess()
    {
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.From(("Mailer:Accounts:default:Transport", "sendgrid")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MailerOptions>>().Value);

        Assert.Contains("sendgrid", string.Join(" ", exception.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void Options_AccountDeclaredAsABareValue_IsReportedRatherThanSilentlyDropped()
    {
        // the configuration binder discards this with no error, leaving the account simply absent
        var services = new ServiceCollection();

        services.AddMailTransport<StubTransport>();
        services.AddMailer(Config.Json("""
            { "Mailer": { "Accounts": { "default": "stub" } } }
            """));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MailerOptions>>().Value);

        Assert.Contains("declared as a single value", string.Join(" ", exception.Failures), StringComparison.Ordinal);
    }
}
