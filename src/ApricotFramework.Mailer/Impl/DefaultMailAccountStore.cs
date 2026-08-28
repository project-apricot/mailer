namespace ApricotFramework.Mailer.Impl;

/// <summary>
/// Resolves an account by asking each registered source in turn.
/// </summary>
/// <remarks>
/// The first source that has the name wins, so the registration order decides precedence between two
/// sources that both hold it.
/// </remarks>
public class DefaultMailAccountStore : IMailAccountStore
{
    /// <summary>
    /// Gets the sources to ask, in the order they are asked.
    /// </summary>
    protected IReadOnlyList<IMailAccountSource> Sources { get; }

    /// <summary>
    /// Creates a new instance of the store.
    /// </summary>
    /// <param name="sources">The sources to ask, or null for none.</param>
    public DefaultMailAccountStore(IEnumerable<IMailAccountSource>? sources = null)
    {
        this.Sources = [.. sources ?? []];
    }

    /// <inheritdoc />
    public virtual async Task<MailAccount?> GetAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var source in this.Sources)
        {
            var account = await source.FindAsync(name, cancellationToken).ConfigureAwait(false);

            if (account is not null)
            {
                return account;
            }
        }

        return null;
    }
}
