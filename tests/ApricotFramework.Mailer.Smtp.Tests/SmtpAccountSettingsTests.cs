using ApricotFramework.Mailer;

namespace ApricotFramework.Mailer.Smtp.Tests;

public class SmtpAccountSettingsTests
{
    private static MailAccountSettings Settings(params (string Name, string? Value)[] values)
    {
        return new MailAccountSettings(values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.OrdinalIgnoreCase), "default");
    }

    [Fact]
    public void From_OnlyAHost_TakesTheSubmissionPortAndAutoSecurity()
    {
        var settings = SmtpAccountSettings.From(Settings(("Host", "smtp.example.com")));

        Assert.Equal("smtp.example.com", settings.Host);
        Assert.Equal(SmtpAccountSettings.DefaultPort, settings.Port);
        Assert.Equal(SmtpAccountSettings.DefaultTimeout, settings.Timeout);

        // never None: a missing setting must not leave the session in the clear
        Assert.Equal(SmtpSecurity.Auto, settings.Security);
    }

    [Fact]
    public void From_NoHost_Throws()
    {
        Assert.Throws<MailAccountException>(() => SmtpAccountSettings.From(Settings(("Port", "587"))));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("587", 587)]
    [InlineData("65535", 65535)]
    public void From_PortWithinRange_IsAccepted(string stored, int expected)
    {
        // the old library rejected 65535 with a >= comparison
        Assert.Equal(expected, SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Port", stored))).Port);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("99999999")]
    public void From_PortOutOfRange_Throws(string stored)
    {
        Assert.Throws<MailAccountException>(() => SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Port", stored))));
    }

    [Fact]
    public void From_SecurityInAnyCase_Parses()
    {
        Assert.Equal(SmtpSecurity.StartTls, SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Security", "starttls"))).Security);
    }

    [Fact]
    public void From_MisspelledSecurity_ThrowsRatherThanFallingBackToNone()
    {
        // a typo must not silently downgrade the connection
        Assert.Throws<MailAccountException>(() => SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Security", "STARTTLS_ALWAYS"))));
    }

    [Fact]
    public void TryGetCredentials_NoUsername_ReportsNoCredentials()
    {
        // an unauthenticated relay is a real configuration
        Assert.False(SmtpAccountSettings.From(Settings(("Host", "h.example.com"))).TryGetCredentials(out _, out _));
    }

    [Fact]
    public void TryGetCredentials_UsernameAndPassword_ReportsThem()
    {
        var settings = SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Username", "u"), ("Password", "p")));

        Assert.True(settings.TryGetCredentials(out var username, out var password));
        Assert.Equal("u", username);
        Assert.Equal("p", password);
    }

    [Fact]
    public void From_UsernameWithoutAPassword_Throws()
    {
        Assert.Throws<MailAccountException>(() => SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Username", "u"))));
    }

    [Fact]
    public void From_BareNumberTimeout_ThrowsRatherThanMeaningDays()
    {
        Assert.Throws<MailAccountException>(() => SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Timeout", "30"))));
    }

    [Fact]
    public void From_NegativeTimeout_Throws()
    {
        Assert.Throws<MailAccountException>(() => SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Timeout", "-00:00:30"))));
    }

    [Fact]
    public void From_ThrowingOnABadValue_DoesNotEchoTheValue()
    {
        const string Secret = "hunter2-do-not-log";

        var exception = Assert.Throws<MailAccountException>(() => SmtpAccountSettings.From(Settings(("Host", "h.example.com"), ("Port", Secret))));

        Assert.DoesNotContain(Secret, exception.Message, StringComparison.Ordinal);
    }
}
