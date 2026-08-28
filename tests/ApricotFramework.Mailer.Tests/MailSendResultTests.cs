using System.Text.Json;
using ApricotFramework.Mailer;

namespace ApricotFramework.Mailer.Tests;

public class MailSendResultTests
{
    private const string ServerResponse = "535 5.7.8 Authentication failed for someone@example.com";

    private static MailSendResult CreateFailure()
    {
        return MailSendResult.Failure(MailErrorCode.Authentication, "The server refused the credentials.", "default", "smtp", new InvalidOperationException(ServerResponse));
    }

    [Fact]
    public void ToString_Failure_DoesNotQuoteTheServerResponse()
    {
        // a mail server's refusal often echoes the account it refused, and a result is a tempting
        // thing to log whole
        var text = CreateFailure().ToString();

        Assert.DoesNotContain(ServerResponse, text, StringComparison.Ordinal);
        Assert.Contains(nameof(MailErrorCode.Authentication), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_Failure_LeavesTheExceptionOut()
    {
        var json = JsonSerializer.Serialize(CreateFailure());

        Assert.DoesNotContain(ServerResponse, json, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MailErrorCode.Connection, true)]
    [InlineData(MailErrorCode.Timeout, true)]
    [InlineData(MailErrorCode.Throttled, true)]
    [InlineData(MailErrorCode.Rejected, false)]
    [InlineData(MailErrorCode.Authentication, false)]
    [InlineData(MailErrorCode.InvalidMessage, false)]
    [InlineData(MailErrorCode.AccountNotFound, false)]
    public void IsTransient_ByErrorCode_SaysWhetherARetryIsWorthwhile(MailErrorCode errorCode, bool expected)
    {
        var result = MailSendResult.Failure(errorCode, "failed", "default");

        Assert.Equal(expected, result.IsTransient());
    }

    [Fact]
    public void IsTransient_Success_IsFalse()
    {
        Assert.False(MailSendResult.Success("default", "smtp").IsTransient());
    }

    [Fact]
    public void Failure_WithoutAReason_Throws()
    {
        Assert.Throws<ArgumentException>(() => MailSendResult.Failure(MailErrorCode.None, "failed", "default"));
    }

    [Fact]
    public void Success_Always_CarriesNoError()
    {
        var result = MailSendResult.Success("default", "smtp", "id-1");

        Assert.Equal(MailErrorCode.None, result.ErrorCode);
        Assert.Null(result.Error);
        Assert.Equal("id-1", result.MessageId);
    }
}
