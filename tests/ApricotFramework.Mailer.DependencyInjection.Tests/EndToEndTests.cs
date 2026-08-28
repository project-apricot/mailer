using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.Impl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApricotFramework.Mailer.DependencyInjection.Tests;

public class EndToEndTests
{
    private static EmailMessage Message()
    {
        return new EmailMessage
        {
            Subject = "Welcome",
            Body = EmailBody.FromHtml("<b>hello</b>"),
            To = [EmailAddress.Parse("recipient@example.com")],
        };
    }

    [Fact]
    public async Task SendAsync_ThroughTheContainer_ReachesTheRegisteredTransport()
    {
        var transport = new MemoryMailTransport();
        var services = new ServiceCollection();

        services.AddMailTransport(transport);
        services.AddMailer(Config.From(
            ("Mailer:Accounts:default:Transport", MemoryMailTransport.TransportName),
            ("Mailer:Accounts:default:DefaultFrom", "Example <no-reply@example.com>")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var result = await provider.GetRequiredService<IMailer>().SendAsync(Message(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        var sent = Assert.Single(transport.GetSentMessages());

        Assert.Equal("no-reply@example.com", sent.Message.From!.Address);
    }

    [Fact]
    public async Task SendAsync_Succeeding_IsLogged()
    {
        var log = new CapturingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddProvider(log).SetMinimumLevel(LogLevel.Trace));
        services.AddMailTransport(new MemoryMailTransport());
        services.AddMailer(Config.From(
            ("Mailer:Accounts:default:Transport", MemoryMailTransport.TransportName),
            ("Mailer:Accounts:default:DefaultFrom", "no-reply@example.com")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        await provider.GetRequiredService<IMailer>().SendAsync(Message(), TestContext.Current.CancellationToken);

        var entry = Assert.Single(log.Entries);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("default", entry.Message, StringComparison.Ordinal);
        Assert.Contains(MemoryMailTransport.TransportName, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_Failing_IsLoggedAsAWarning()
    {
        var log = new CapturingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddProvider(log).SetMinimumLevel(LogLevel.Trace));
        services.AddMailTransport(new MemoryMailTransport());
        services.AddMailer(Config.From(
            ("Mailer:Accounts:default:Transport", MemoryMailTransport.TransportName),
            ("Mailer:Accounts:default:DefaultFrom", "no-reply@example.com")));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        await provider.GetRequiredService<IMailer>().SendAsync("nothing", Message(), TestContext.Current.CancellationToken);

        var entry = Assert.Single(log.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(MailErrorCode.AccountNotFound), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A logger provider that keeps what was written, so a test can assert on it.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(this.Entries);
        }

        public void Dispose()
        {
            // nothing to release; the entries outlive the provider on purpose
        }

        private sealed class CapturingLogger(List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
