# ApricotFramework.Mailer

[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Mailer.svg?label=ApricotFramework.Mailer)](https://www.nuget.org/packages/ApricotFramework.Mailer/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Mailer.Smtp.svg?label=ApricotFramework.Mailer.Smtp)](https://www.nuget.org/packages/ApricotFramework.Mailer.Smtp/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Mailer.DependencyInjection.svg?label=ApricotFramework.Mailer.DependencyInjection)](https://www.nuget.org/packages/ApricotFramework.Mailer.DependencyInjection/)
[![CI](https://github.com/project-apricot/mailer/actions/workflows/ci.yml/badge.svg)](https://github.com/project-apricot/mailer/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](https://github.com/project-apricot/mailer/blob/main/LICENSE)

Sending mail from .NET, configured the way a connection string is: named accounts in one settings
section, each naming the transport that carries it. SMTP is here today and a transport is a package,
so adding a vendor changes nothing about the accounts you already have.

`ApricotFramework.Mailer` is the **zero-dependency** core, and nothing here needs ASP.NET Core — the
integration package depends only on `Microsoft.Extensions.*`, so a worker host can use it too.

## Install

```bash
dotnet add package ApricotFramework.Mailer
dotnet add package ApricotFramework.Mailer.Smtp
dotnet add package ApricotFramework.Mailer.DependencyInjection
```

## Usage

```csharp
builder.Services.AddMailer(builder.Configuration);
builder.Services.AddSmtpMailTransport();
```

```jsonc
"Mailer": {
  "DefaultAccount": "default",
  "Accounts": {
    "default": {
      "Transport": "smtp",
      "DefaultFrom": "Example <no-reply@example.com>",
      "Settings": {
        "Host": "smtp.example.com",
        "Port": 587,
        "Security": "StartTls",
        "Username": "apikey"
        // Password comes from the environment:
        // Mailer__Accounts__default__Settings__Password
      }
    }
  }
}
```

```csharp
var result = await mailer.SendAsync(new EmailMessage
{
    Subject = "Welcome",
    Body = EmailBody.FromHtml("<b>Hello</b>"),
    To = [EmailAddress.Parse("someone@example.com")],
});

if (!result.Succeeded && result.IsTransient())
{
    // a connection problem, a timeout or a rate limit: worth trying again
}
```

A refused or undeliverable message is a **result, not an exception** — only cancellation throws. The
one exception to "everything is a result" is startup: an account naming a transport you did not
register fails the host, rather than waiting for the first mail to find out.

Full documentation: <https://projectapricot.dev/docs/mailer>
