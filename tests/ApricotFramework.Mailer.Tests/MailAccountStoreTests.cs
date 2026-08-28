using ApricotFramework.Mailer;
using ApricotFramework.Mailer.Impl;

namespace ApricotFramework.Mailer.Tests;

public class MailAccountStoreTests
{
    [Fact]
    public async Task GetAsync_NoSources_ReturnsNull()
    {
        Assert.Null(await new DefaultMailAccountStore().GetAsync("default", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_TwoSourcesHoldingTheName_TakesTheFirst()
    {
        var first = new StubAccountSource(SampleMail.Account() with { Transport = "first" });
        var second = new StubAccountSource(SampleMail.Account() with { Transport = "second" });

        var store = new DefaultMailAccountStore([first, second]);

        var account = await store.GetAsync("default", TestContext.Current.CancellationToken);

        Assert.Equal("first", account!.Transport);
    }

    [Fact]
    public async Task GetAsync_FirstSourceDoesNotHaveIt_FallsThroughToTheNext()
    {
        var store = new DefaultMailAccountStore([new StubAccountSource(), new StubAccountSource(SampleMail.Account("billing"))]);

        Assert.NotNull(await store.GetAsync("billing", TestContext.Current.CancellationToken));
    }
}
