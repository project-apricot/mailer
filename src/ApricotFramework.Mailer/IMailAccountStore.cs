namespace ApricotFramework.Mailer;

/// <summary>
/// Resolves sending accounts by name across every place accounts can come from.
/// </summary>
/// <remarks>
/// What a mailer depends on. A host adds places to look by registering an
/// <see cref="IMailAccountSource"/>; replacing the store itself changes how they are combined.
/// </remarks>
public interface IMailAccountStore
{
    /// <summary>
    /// Gets an account by name.
    /// </summary>
    /// <param name="name">The account name.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The account, or null when nothing has it.</returns>
    Task<MailAccount?> GetAsync(string name, CancellationToken cancellationToken);
}
