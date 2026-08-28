using ApricotFramework.Mailer;
using ApricotFramework.Mailer.Impl;

namespace ApricotFramework.Mailer.Tests;

public class MailTransportRegistryTests
{
    [Fact]
    public void Find_NameInAnotherCase_ResolvesTheTransport()
    {
        var registry = new MailTransportRegistry([new StubTransport("smtp")]);

        Assert.NotNull(registry.Find("SMTP"));
    }

    [Fact]
    public void Find_UnknownName_ReturnsNull()
    {
        Assert.Null(new MailTransportRegistry([new StubTransport("smtp")]).Find("sendgrid"));
    }

    [Fact]
    public void Constructor_TwoTransportsUnderOneName_ThrowsRatherThanPickingByOrder()
    {
        var exception = Assert.Throws<ArgumentException>(() => new MailTransportRegistry([new StubTransport("smtp"), new StubTransport("smtp")]));

        Assert.Contains("smtp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NamesDifferingOnlyInCase_AreTheSameName()
    {
        Assert.Throws<ArgumentException>(() => new MailTransportRegistry([new StubTransport("smtp"), new StubTransport("SMTP")]));
    }

    [Fact]
    public void Constructor_BlankTransportName_ThrowsNamingTheType()
    {
        var exception = Assert.Throws<ArgumentException>(() => new MailTransportRegistry([new StubTransport("  ")]));

        Assert.Contains(nameof(StubTransport), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetNames_Always_ReportsEveryRegisteredName()
    {
        var registry = new MailTransportRegistry([new StubTransport("smtp"), new StubTransport("memory")]);

        Assert.Equal(["memory", "smtp"], registry.GetNames().Order(StringComparer.Ordinal));
    }
}
