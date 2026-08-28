namespace ApricotFramework.Mailer.Examples.Web;

/// <summary>
/// What the welcome template binds to.
/// </summary>
/// <param name="Name">Who the mail is addressed to.</param>
/// <param name="Product">What they signed up for.</param>
public sealed record WelcomeModel(string Name, string Product);
