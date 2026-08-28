using ApricotFramework.Mailer;
using ApricotFramework.Mailer.Impl;

namespace ApricotFramework.Mailer.Tests;

public class MemoryMailTransportTests
{
    [Fact]
    public async Task SendAsync_Always_KeepsTheMessageAndReportsSuccess()
    {
        var transport = new MemoryMailTransport();
        var message = SampleMail.Message();

        var result = await transport.SendAsync(message, SampleMail.Account(transport: MemoryMailTransport.TransportName), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.MessageId);
        Assert.Same(message, Assert.Single(transport.GetSentMessages()).Message);
    }

    [Fact]
    public async Task GetSentMessages_SeveralSends_ReadsOldestFirst()
    {
        var transport = new MemoryMailTransport();
        var account = SampleMail.Account(transport: MemoryMailTransport.TransportName);

        await transport.SendAsync(SampleMail.Message() with { Subject = "one" }, account, TestContext.Current.CancellationToken);
        await transport.SendAsync(SampleMail.Message() with { Subject = "two" }, account, TestContext.Current.CancellationToken);

        Assert.Equal(["one", "two"], transport.GetSentMessages().Select(sent => sent.Message.Subject));
    }

    [Fact]
    public async Task Clear_AfterASend_DiscardsWhatWasKept()
    {
        var transport = new MemoryMailTransport();

        await transport.SendAsync(SampleMail.Message(), SampleMail.Account(transport: MemoryMailTransport.TransportName), TestContext.Current.CancellationToken);
        transport.Clear();

        Assert.Empty(transport.GetSentMessages());
    }

    [Fact]
    public async Task SendAsync_AlreadyCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new MemoryMailTransport().SendAsync(SampleMail.Message(), SampleMail.Account(), cancellation.Token));
    }
}
