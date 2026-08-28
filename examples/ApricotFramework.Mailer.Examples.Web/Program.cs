using ApricotFramework.Mailer;
using ApricotFramework.Mailer.DependencyInjection.Extensions;
using ApricotFramework.Mailer.Examples.Web;
using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Smtp.Extensions;
using ApricotFramework.RazorEngine.AspNetCore;
using ApricotFramework.RazorEngine.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// the pattern worth copying: one AddMailer plus the transports this host is willing to use. No
// transport is registered for you, so a send can never quietly go nowhere.
builder.Services.AddMailer(builder.Configuration);
builder.Services.AddSmtpMailTransport();
builder.Services.AddPickupDirectoryMailTransport();

// registers one instance under both IMailTransport and its own type, so the endpoint below can
// simply take a MemoryMailTransport
builder.Services.AddMemoryMailTransport();

// razor-engine renders the body; the mailer does not know templates exist
builder.Services.AddRazorEngine();

var app = builder.Build();

// The default account writes .eml files to ./mail-drop, so this runs with no SMTP server at all.
// Open one in a mail client to see exactly what a recipient would.
app.MapPost("/welcome", async (IMailer mailer, IRazorEngine razor, string name, string? account, CancellationToken cancellationToken) =>
{
    // TryParse rather than Parse: the name is request input, and a control character in it is a
    // header-injection attempt the model refuses. Parse would throw and become a 500.
    if (!EmailAddress.TryParse("recipient@example.com", name, out var recipient))
    {
        return Results.BadRequest(new { ErrorCode = nameof(MailErrorCode.InvalidMessage), Error = "That name cannot be used as a display name." });
    }

    var html = await razor.RenderAsync("~/Templates/Welcome.cshtml", new WelcomeModel(name, "Apricot"), cancellationToken);

    var message = new EmailMessage
    {
        Subject = $"Welcome, {name}",
        Body = EmailBody.FromBoth(html, $"Welcome, {name}. Your account on Apricot is ready."),
        To = [recipient],
        Attachments = [EmailAttachment.Inline("header-logo", "logo.png", Logo.Bytes, "image/png")],
    };

    var result = account is null
        ? await mailer.SendAsync(message, cancellationToken)
        : await mailer.SendAsync(account, message, cancellationToken);

    // the whole result is safe to show here only because this is an example; Error can quote a
    // server's reply, so a real endpoint returns the code and logs the rest
    return result.Succeeded
        ? Results.Ok(new { result.Account, result.Transport, result.MessageId })
        : Results.BadRequest(new { result.Account, ErrorCode = result.ErrorCode.ToString(), result.Error, Retryable = result.IsTransient() });
});

// what the in-memory transport captured, for the "assert on what would have been sent" case
app.MapGet("/captured", (MemoryMailTransport memory) =>
{
    return memory.GetSentMessages().Select(sent => new
    {
        sent.Account,
        sent.MessageId,
        sent.Message.Subject,
        To = sent.Message.To.Select(address => address.ToString()),
    });
});

app.Run();
