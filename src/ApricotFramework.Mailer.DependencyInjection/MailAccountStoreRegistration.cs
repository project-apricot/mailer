namespace ApricotFramework.Mailer.DependencyInjection;

/// <summary>
/// Records that a host supplied its own account store.
/// </summary>
/// <remarks>
/// Startup validation cannot ask a store what it knows, so this is how it learns not to complain that
/// nothing is configured. Registered by <c>AddMailAccountStore</c>.
/// </remarks>
/// <param name="StoreType">The store type that was registered.</param>
public sealed record MailAccountStoreRegistration(Type StoreType);
