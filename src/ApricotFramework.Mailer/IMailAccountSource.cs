namespace ApricotFramework.Mailer;

/// <summary>
/// Supplies sending accounts from somewhere other than configuration.
/// </summary>
/// <remarks>
/// This is the seam for accounts held in a database, a secrets manager, or another service. It says
/// nothing about storage, schema, or how a credential is protected, all of which stay the
/// implementation's business — including decrypting anything it stores encrypted.
/// <para>
/// Asked once per sending, so an implementation that hits the network should be put behind a cache.
/// </para>
/// </remarks>
public interface IMailAccountSource
{
    /// <summary>
    /// Finds an account by name.
    /// </summary>
    /// <param name="name">The account name to look for.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The account, or null when this source does not have it.</returns>
    Task<MailAccount?> FindAsync(string name, CancellationToken cancellationToken);
}
