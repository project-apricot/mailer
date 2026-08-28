using ApricotFramework.Mailer.DependencyInjection.Impl;
using ApricotFramework.Mailer.DependencyInjection.Validation;
using ApricotFramework.Mailer.Impl;
using ApricotFramework.Mailer.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Mailer.DependencyInjection.Extensions;

/// <summary>
/// Registers the mailer on an <see cref="IServiceCollection"/>.
/// </summary>
public static class MailerServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section bound when no other is named.
    /// </summary>
    public const string ConfigurationSectionName = "Mailer";

    /// <summary>
    /// Adds the mailer, with accounts bound from the <c>Mailer</c> configuration section.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
    /// </exception>
    public static IServiceCollection AddMailer(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddMailer(configuration, ConfigurationSectionName);
    }

    /// <summary>
    /// Adds the mailer, with accounts bound from a named configuration section.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <param name="sectionName">The section to bind.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// No transport is registered here, because which ones a host wants is its decision — add at
    /// least one, or startup validation fails. Registration order does not matter: a transport or an
    /// account source added after this call is still picked up.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sectionName"/> is null or blank.</exception>
    public static IServiceCollection AddMailer(this IServiceCollection services, IConfiguration configuration, string sectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var section = configuration.GetSection(sectionName);

        services.AddOptions<MailerOptions>().Bind(section);

        return AddMailerCore(services, section);
    }

    /// <summary>
    /// Adds the mailer, with accounts declared in code.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <param name="configure">Declares the accounts.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
    /// </exception>
    public static IServiceCollection AddMailer(this IServiceCollection services, Action<MailerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<MailerOptions>().Configure(configure);

        return AddMailerCore(services, null);
    }

    /// <summary>
    /// Adds a source of sending accounts, asked before configuration is.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="services">The services collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Call this as many times as there are places to look. Sources accumulate: each is asked in the
    /// order it was registered, and the first one holding the name wins, with configuration asked last,
    /// so an account declared both in a source and in configuration is taken from the source. Adding
    /// the same type twice is the only thing that does nothing.
    /// <para>
    /// Registered as a singleton, because the mailer that consumes it is one — a source that needs a
    /// scoped dependency should take an <see cref="IServiceScopeFactory"/>, or a
    /// <c>DbContextFactory</c>, and open its own scope per lookup.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddMailAccountSource<TSource>(this IServiceCollection services)
        where TSource : class, IMailAccountSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMailAccountSource, TSource>());

        return services;
    }

    /// <summary>
    /// Adds a source of sending accounts that is already built.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <param name="source">The source.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Unlike the generic overload, this does not deduplicate, so one implementation can be
    /// registered more than once with different state.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="source"/> is null.
    /// </exception>
    public static IServiceCollection AddMailAccountSource(this IServiceCollection services, IMailAccountSource source)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);

        services.AddSingleton(source);

        return services;
    }

    /// <summary>
    /// Adds a transport that is already built.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <param name="transport">The transport.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// For a transport that needs constructor arguments the container cannot supply, and for the
    /// in-memory transport, whose captured messages a test reads back from the instance it registered.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="transport"/> is null.
    /// </exception>
    public static IServiceCollection AddMailTransport(this IServiceCollection services, IMailTransport transport)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transport);

        services.AddSingleton(transport);

        return services;
    }

    /// <summary>
    /// Adds a transport the container builds.
    /// </summary>
    /// <typeparam name="TTransport">The transport type.</typeparam>
    /// <param name="services">The services collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Registered as a singleton and deduplicated by type, so adding the same one twice is a no-op.
    /// Two transports answering to the same name are not: that fails when the mailer is first
    /// resolved, rather than letting registration order decide which one sends.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddMailTransport<TTransport>(this IServiceCollection services)
        where TTransport : class, IMailTransport
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMailTransport, TTransport>());

        return services;
    }

    /// <summary>
    /// Adds the in-memory transport, which accounts reach by setting <c>Transport</c> to
    /// <see cref="MemoryMailTransport.TransportName"/>.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// The one instance is registered under both <see cref="IMailTransport"/> and its own type, so a
    /// test or a diagnostic endpoint can take a <see cref="MemoryMailTransport"/> and read
    /// <see cref="MemoryMailTransport.GetSentMessages"/> without sifting it out of every registered
    /// transport. Calling this twice is harmless.
    /// <para>
    /// Nothing registers it for you. A transport that accepts every message and delivers none is the
    /// last thing that should appear in a host by default.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddMemoryMailTransport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var transport = new MemoryMailTransport();

        // registered as an instance rather than by type: TryAddEnumerable refuses a factory, and both
        // registrations have to reach the same object for the captured messages to be readable
        services.TryAddSingleton(transport);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMailTransport>(transport));

        return services;
    }

    /// <summary>
    /// Replaces how an account is resolved, keeping everything built on top of it.
    /// </summary>
    /// <typeparam name="TStore">The store type.</typeparam>
    /// <param name="services">The services collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// For a host that wants resolution to be entirely its own — a database it queries directly, with
    /// no configured accounts behind it. It replaces the whole default store, so the configured
    /// accounts and <see cref="MailerOptions.AccountCacheLifetime"/> no longer apply: a store decides
    /// its own caching, which is what lets one that already caches avoid doing it twice.
    /// <para>
    /// To <i>add</i> a place to look rather than replace the lookup, register an
    /// <see cref="IMailAccountSource"/> instead. Sources are asked ahead of configuration, and the
    /// default store caches whatever they return.
    /// </para>
    /// <para>
    /// Registered as a singleton, and order-independent with <c>AddMailer</c>. Registering
    /// <see cref="IMailAccountStore"/> directly does the same thing, except that startup validation
    /// then has no way to know a store exists and may complain that nothing is configured.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddMailAccountStore<TStore>(this IServiceCollection services)
        where TStore : class, IMailAccountStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMailAccountStore, TStore>();
        services.AddSingleton(new MailAccountStoreRegistration(typeof(TStore)));

        return services;
    }

    /// <summary>
    /// Replaces how an account is resolved with a store that is already built.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <param name="store">The store.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="store"/> is null.
    /// </exception>
    public static IServiceCollection AddMailAccountStore(this IServiceCollection services, IMailAccountStore store)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(store);

        services.AddSingleton(store);
        services.AddSingleton(new MailAccountStoreRegistration(store.GetType()));

        return services;
    }

    /// <summary>
    /// Registers everything that does not depend on where the options came from.
    /// </summary>
    /// <param name="services">The services collection.</param>
    /// <param name="section">The bound section, or null when the options were configured in code.</param>
    /// <returns>The service collection, for chaining.</returns>
    private static IServiceCollection AddMailerCore(IServiceCollection services, IConfigurationSection? section)
    {
        // the mailer logs every send; a web host has already done this, a bare container has not
        services.AddLogging();

        if (section is not null)
        {
            services.TryAddSingleton(new MailerConfigurationSection(section));
        }

        // by implementation type, not by factory: TryAddEnumerable refuses a factory descriptor
        // because it cannot tell two of them apart
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MailerOptions>, MailerOptionsValidator>());

        // a missing transport surfaces when the host starts, not on the first mail
        services.AddOptions<MailerOptions>().ValidateOnStart();

        // the default store caches into this when a lifetime is configured
        services.AddMemoryCache();

        // TryAdd, so a store the host registered first stands; a store it registers afterwards wins
        // on resolve. Either way AddMailAccountStore works on both sides of this call.
        services.TryAddSingleton<IMailAccountStore, OptionsAwareMailAccountStore>();

        services.TryAddSingleton<IMailer, OptionsAwareMailer>();

        return services;
    }
}
