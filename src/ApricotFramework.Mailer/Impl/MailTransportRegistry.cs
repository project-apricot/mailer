namespace ApricotFramework.Mailer.Impl;

/// <summary>
/// The transports available to send through, addressed by name.
/// </summary>
/// <remarks>
/// There are no built-in transports: a host registers the ones it wants, so a sending through an
/// unconfigured account fails loudly instead of being swallowed by a default that goes nowhere.
/// </remarks>
public class MailTransportRegistry
{
    /// <summary>
    /// Gets the transports, keyed by the name they answer to.
    /// </summary>
    protected Dictionary<string, IMailTransport> Transports { get; }

    /// <summary>
    /// Creates a new instance of the registry.
    /// </summary>
    /// <param name="transports">Every registered transport.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transports"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when transport has a blank name, or two answers to the same one.
    /// </exception>
    public MailTransportRegistry(IEnumerable<IMailTransport> transports)
    {
        ArgumentNullException.ThrowIfNull(transports);

        // names are labels, so they are matched the way configuration keys are
        this.Transports = new Dictionary<string, IMailTransport>(StringComparer.OrdinalIgnoreCase);

        foreach (var transport in transports)
        {
            ArgumentNullException.ThrowIfNull(transport, nameof(transports));

            if (string.IsNullOrWhiteSpace(transport.Name))
            {
                throw new ArgumentException($"The transport of type '{transport.GetType().Name}' has a blank name.", nameof(transports));
            }

            // silently keeping the first would make which one sends depend on registration order
            if (!this.Transports.TryAdd(transport.Name, transport))
            {
                throw new ArgumentException($"Two mail transports are named '{transport.Name}'.", nameof(transports));
            }
        }
    }

    /// <summary>
    /// Finds the transport a name refers to.
    /// </summary>
    /// <param name="name">The transport name.</param>
    /// <returns>The transport, or null when none answers to that name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public virtual IMailTransport? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return this.Transports.GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets the names that can be sent through.
    /// </summary>
    /// <returns>The registered transport names.</returns>
    public virtual IReadOnlyCollection<string> GetNames()
    {
        return this.Transports.Keys;
    }
}
