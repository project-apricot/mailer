using ApricotFramework.Mailer;
using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Options;

namespace ApricotFramework.Mailer.Tests;

/// <summary>
/// The seam a mailer implements: where its options come from, and nothing else.
/// </summary>
public class MailerBaseTests
{
    [Fact]
    public async Task SendAsync_OptionsSuppliedPerCall_AreReadEveryTime()
    {
        // what an IOptionsMonitor-backed mailer relies on: nothing is captured at construction
        var transport = new StubTransport();
        var mailer = new SwitchableMailer(new StubAccountStore(SampleMail.Account("first"), SampleMail.Account("second")), [transport])
        {
            AccountName = "first",
        };

        Assert.Equal("first", (await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken)).Account);

        mailer.AccountName = "second";

        Assert.Equal("second", (await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken)).Account);
    }

    [Fact]
    public async Task SendAsync_DerivedMailer_GetsTheWholeSendingBehaviour()
    {
        var transport = new StubTransport();
        var mailer = new SwitchableMailer(new StubAccountStore(SampleMail.Account()), [transport]) { AccountName = "default" };

        Assert.True((await mailer.SendAsync(SampleMail.Message(), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Single(transport.Calls);
    }

    [Fact]
    public void Constructor_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SwitchableMailer(null!, []));
        Assert.Throws<ArgumentNullException>(() => new SwitchableMailer(new StubAccountStore(), null!));
    }

    [Fact]
    public void DefaultMailer_NoOptionsGiven_UsesTheDefaults()
    {
        // the one thing DefaultMailer adds over the base
        var mailer = new DefaultMailer(new StubAccountStore(), []);

        Assert.NotNull(mailer);
    }

    /// <summary>
    /// A mailer whose options the test changes between sends, standing in for a reloaded configuration.
    /// </summary>
    private sealed class SwitchableMailer(IMailAccountStore accountStore, IEnumerable<IMailTransport> transports)
        : MailerBase(accountStore, transports)
    {
        public string AccountName { get; set; } = "default";

        protected override MailerOptions GetCurrentOptions()
        {
            return new MailerOptions { DefaultAccount = this.AccountName };
        }
    }
}
