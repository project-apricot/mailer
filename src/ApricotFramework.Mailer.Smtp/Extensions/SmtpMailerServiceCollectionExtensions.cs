using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApricotFramework.Mailer.Smtp.Extensions;

/// <summary>
/// Registers this package's transports on an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// A transport needs no configuration of its own: everything it reads arrives on the sending account,
/// so these calls take nothing and can be made before or after <c>AddMailer</c>.
/// </remarks>
public static class SmtpMailerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SMTP transport, which accounts reach by setting <c>Transport</c> to
    /// <see cref="SmtpMailTransport.TransportName"/>.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Registered as a singleton, because the mailer that consumes it is one and a transport holds no
    /// per-send state. Calling this twice is harmless; registering a second transport that answers to
    /// <c>smtp</c> is not, and fails when the mailer is first resolved. To substitute a subclass,
    /// register it instead of calling this.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddSmtpMailTransport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMailTransport, SmtpMailTransport>());

        return services;
    }

    /// <summary>
    /// Adds the pickup-directory transport, which accounts reach by setting <c>Transport</c> to
    /// <see cref="PickupDirectoryMailTransport.TransportName"/>.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Registered as a singleton. The directory is read from each account's
    /// <c>Directory</c> setting, so one registration serves any number of drop points.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddPickupDirectoryMailTransport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMailTransport, PickupDirectoryMailTransport>());

        return services;
    }
}
