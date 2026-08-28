using ApricotFramework.Mailer;

namespace ApricotFramework.Mailer.Tests;

public class EmailAddressTests
{
    [Theory]
    [InlineData("someone@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    [InlineData("  padded@example.com  ")]
    [InlineData("UPPER@EXAMPLE.COM")]
    [InlineData("ünïcode@example.com")]
    public void TryParse_UsableAddress_Succeeds(string candidate)
    {
        Assert.True(EmailAddress.TryParse(candidate, out var address));
        Assert.Equal(candidate.Trim(), address.Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("a@b@c.com")]
    [InlineData("@example.com")]
    [InlineData("local@")]
    [InlineData("local@nodot")]
    [InlineData("local@.example.com")]
    [InlineData("local@example.com.")]
    [InlineData("local@exa..mple.com")]
    [InlineData("has space@example.com")]
    public void TryParse_UnusableAddress_Fails(string? candidate)
    {
        Assert.False(EmailAddress.TryParse(candidate, out var address));
        Assert.Null(address);
    }

    [Theory]
    [InlineData("victim@example.com\r\nBcc: attacker@evil.com")]
    [InlineData("victim@example.com\nBcc: attacker@evil.com")]
    [InlineData("victim@example.com\0")]
    public void TryParse_AddressCarryingAHeaderInjection_Fails(string candidate)
    {
        // MimeKit strips or throws on these depending on where they land, so refusing them here is
        // what makes the outcome the same for every transport
        Assert.False(EmailAddress.TryParse(candidate, out _));
    }

    [Fact]
    public void TryParse_DisplayNameCarryingCrLf_Fails()
    {
        Assert.False(EmailAddress.TryParse("someone@example.com", "Bob\r\nBcc: attacker@evil.com", out _));
    }

    [Fact]
    public void TryParse_AddressOverTheLengthLimit_Fails()
    {
        var local = new string('a', 60);
        var domain = new string('b', 200);

        Assert.False(EmailAddress.TryParse($"{local}@{domain}.com", out _));
    }

    [Fact]
    public void TryParse_LocalPartOverTheLengthLimit_Fails()
    {
        Assert.False(EmailAddress.TryParse($"{new string('a', 65)}@example.com", out _));
    }

    [Fact]
    public void TryParse_LocalPartAtTheLengthLimit_Succeeds()
    {
        Assert.True(EmailAddress.TryParse($"{new string('a', 64)}@example.com", out _));
    }

    [Fact]
    public void Parse_BlankDisplayName_KeepsNoName()
    {
        Assert.Null(EmailAddress.Parse("someone@example.com", "   ").Name);
    }

    [Fact]
    public void Parse_UnusableAddress_ThrowsWithoutEchoingTheValue()
    {
        var exception = Assert.Throws<FormatException>(() => EmailAddress.Parse("nope"));

        Assert.DoesNotContain("nope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_WithAName_ProducesTheDisplayForm()
    {
        Assert.Equal("Bob <bob@example.com>", EmailAddress.Parse("bob@example.com", "Bob").ToString());
    }

    [Fact]
    public void ToString_WithoutAName_ProducesTheBareAddress()
    {
        Assert.Equal("bob@example.com", EmailAddress.Parse("bob@example.com").ToString());
    }

    [Fact]
    public void Equals_SameAddressDifferentCase_AreNotEqual()
    {
        // ordinal, so a mailbox is whatever it was written as; nothing here canonicalises a domain
        Assert.NotEqual(EmailAddress.Parse("bob@example.com"), EmailAddress.Parse("BOB@example.com"));
    }
}
