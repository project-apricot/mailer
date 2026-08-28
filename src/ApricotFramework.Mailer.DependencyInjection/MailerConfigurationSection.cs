using Microsoft.Extensions.Configuration;

namespace ApricotFramework.Mailer.DependencyInjection;

/// <summary>
/// The configuration section the mail options were bound from.
/// </summary>
/// <remarks>
/// Registered only when the options came from configuration, so validation can report a declaration
/// the binder discarded — which it does silently. Absent when the options were configured in code.
/// </remarks>
/// <param name="Value">The section.</param>
public sealed record MailerConfigurationSection(IConfigurationSection Value);
