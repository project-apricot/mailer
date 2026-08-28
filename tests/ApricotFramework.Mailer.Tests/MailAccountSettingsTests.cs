using ApricotFramework.Mailer;

namespace ApricotFramework.Mailer.Tests;

public class MailAccountSettingsTests
{
    private const string SecretValue = "hunter2-do-not-log";

    private static MailAccountSettings CreateSettings(params (string Name, string? Value)[] values)
    {
        return new MailAccountSettings(values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.OrdinalIgnoreCase), "billing");
    }

    [Fact]
    public void GetString_NameInAnotherCase_FindsTheValue()
    {
        Assert.Equal("smtp.example.com", CreateSettings(("Host", "smtp.example.com")).GetString("HOST"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetString_PresentButBlank_ReturnsTheFallback(string? stored)
    {
        Assert.Equal("fallback", CreateSettings(("Host", stored)).GetString("Host", "fallback"));
    }

    [Fact]
    public void GetRequiredString_Absent_ThrowsNamingTheAccountAndSetting()
    {
        var exception = Assert.Throws<MailAccountException>(() => CreateSettings().GetRequiredString("Host"));

        Assert.Contains("Host", exception.Message, StringComparison.Ordinal);
        Assert.Contains("billing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequiredString_PresentButNull_IsTreatedAsAbsent()
    {
        Assert.Throws<MailAccountException>(() => CreateSettings(("Host", null)).GetRequiredString("Host"));
    }

    [Fact]
    public void GetInt32_ValueTheJsonBinderProduced_Parses()
    {
        // a JSON number reaches configuration as text
        Assert.Equal(587, CreateSettings(("Port", "587")).GetInt32("Port", 25));
    }

    [Fact]
    public void GetInt32_Absent_ReturnsTheFallback()
    {
        Assert.Equal(25, CreateSettings().GetInt32("Port", 25));
    }

    [Fact]
    public void GetInt32_NotANumber_ThrowsWithoutEchoingTheValue()
    {
        var exception = Assert.Throws<MailAccountException>(() => CreateSettings(("Port", SecretValue)).GetInt32("Port", 25));

        Assert.DoesNotContain(SecretValue, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("False", false)]
    public void GetBoolean_ValueTheJsonBinderProduced_Parses(string stored, bool expected)
    {
        // the JSON provider renders a bool as "True"/"False", so parsing has to be case-insensitive
        Assert.Equal(expected, CreateSettings(("UseSsl", stored)).GetBoolean("UseSsl", false));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    public void GetBoolean_ShellStyleTruth_Throws(string stored)
    {
        Assert.Throws<MailAccountException>(() => CreateSettings(("UseSsl", stored)).GetBoolean("UseSsl", false));
    }

    [Fact]
    public void GetTimeSpan_ColonSeparated_Parses()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), CreateSettings(("Timeout", "00:00:30")).GetTimeSpan("Timeout", TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void GetTimeSpan_BareNumber_ThrowsRatherThanMeaningDays()
    {
        // TimeSpan.Parse reads "30" as thirty days, which is never what was meant by a timeout
        var exception = Assert.Throws<MailAccountException>(() => CreateSettings(("Timeout", "30")).GetTimeSpan("Timeout", TimeSpan.Zero));

        Assert.Contains("hh:mm:ss", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetEnum_MemberNameInAnyCase_Parses()
    {
        Assert.Equal(SampleChoice.Second, CreateSettings(("Choice", "second")).GetEnum("Choice", SampleChoice.First));
    }

    [Fact]
    public void GetEnum_Absent_ReturnsTheFallback()
    {
        Assert.Equal(SampleChoice.Second, CreateSettings().GetEnum("Choice", SampleChoice.Second));
    }

    [Fact]
    public void GetEnum_NumericValue_Throws()
    {
        // a stored ordinal would change meaning if the enumeration were ever reordered
        Assert.Throws<MailAccountException>(() => CreateSettings(("Choice", "1")).GetEnum("Choice", SampleChoice.First));
    }

    [Fact]
    public void GetEnum_UndeclaredValue_ThrowsListingTheMembers()
    {
        var exception = Assert.Throws<MailAccountException>(() => CreateSettings(("Choice", "Third")).GetEnum("Choice", SampleChoice.First));

        Assert.Contains(nameof(SampleChoice.Second), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetNames_Always_ReportsWhatWasSuppliedSoAnUnknownKeyCanBeFound()
    {
        Assert.Equal(["Prot"], CreateSettings(("Prot", "587")).GetNames());
    }

    [Fact]
    public void Empty_Always_HasNothingInIt()
    {
        Assert.Empty(MailAccountSettings.Empty.GetNames());
    }

    private enum SampleChoice
    {
        First = 0,
        Second = 1,
    }
}
