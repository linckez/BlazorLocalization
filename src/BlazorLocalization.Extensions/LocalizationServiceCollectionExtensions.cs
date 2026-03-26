using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions;

public static class LocalizationServiceCollectionExtensions
{
    private const string DefaultSectionName = "Localization";

    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="ProviderBasedStringLocalizer"/> as the <see cref="IStringLocalizer"/> implementation,
        /// backed by FusionCache and <see cref="ITranslationProvider"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Reads the <c>"Localization"</c> section from the supplied <paramref name="configuration"/>.
        /// Translation providers registered on the returned builder will automatically bind
        /// their options from <c>Localization:TranslationProviders:{ProviderName}</c>.
        /// </para>
        /// <para>
        /// Use <paramref name="configure"/> to override or supplement values from configuration.
        /// Code-based values win over configuration values.
        /// </para>
        /// <inheritdoc cref="AddProviderBasedLocalization(IServiceCollection, IConfigurationSection, Action{ProviderBasedLocalizationOptions}?)" path="/remarks/para[3]"/>
        /// <inheritdoc cref="AddProviderBasedLocalization(IServiceCollection, IConfigurationSection, Action{ProviderBasedLocalizationOptions}?)" path="/remarks/para[4]"/>
        /// </remarks>
        /// <param name="configuration">
        /// The application's root <see cref="IConfiguration"/> (e.g. <c>builder.Configuration</c>).
        /// </param>
        /// <param name="configure">Optional delegate to override or supplement configuration values.</param>
        public ProviderBasedLocalizationBuilder AddProviderBasedLocalization(IConfiguration configuration,
            Action<ProviderBasedLocalizationOptions>? configure = null)
        {
            var section = configuration.GetSection(DefaultSectionName);
            return AddProviderBasedLocalizationCore(services, section, configure);
        }

        /// <summary>
        /// Registers <see cref="ProviderBasedStringLocalizer"/> as the <see cref="IStringLocalizer"/> implementation,
        /// backed by FusionCache and <see cref="ITranslationProvider"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Attempts to resolve <see cref="IConfiguration"/> from the service collection. This works
        /// reliably with the default host builder but
        /// <b>not</b> with <c>WebApplication.CreateBuilder</c>, which defers registration until
        /// <c>Build()</c>. Prefer the <see cref="AddProviderBasedLocalization(IServiceCollection, IConfiguration, Action{ProviderBasedLocalizationOptions}?)"/>
        /// overload when <c>builder.Configuration</c> is available.
        /// </para>
        /// <para>
        /// Use <paramref name="configure"/> to override or supplement values from configuration.
        /// Code-based values win over configuration values.
        /// </para>
        /// <inheritdoc cref="AddProviderBasedLocalization(IServiceCollection, IConfigurationSection, Action{ProviderBasedLocalizationOptions}?)" path="/remarks/para[3]"/>
        /// <inheritdoc cref="AddProviderBasedLocalization(IServiceCollection, IConfigurationSection, Action{ProviderBasedLocalizationOptions}?)" path="/remarks/para[4]"/>
        /// </remarks>
        public ProviderBasedLocalizationBuilder AddProviderBasedLocalization(Action<ProviderBasedLocalizationOptions>? configure = null)
        {
            var section = ResolveConfigurationSection(services, DefaultSectionName);
            return AddProviderBasedLocalizationCore(services, section, configure);
        }

        /// <summary>
        /// Registers <see cref="ProviderBasedStringLocalizer"/> as the <see cref="IStringLocalizer"/> implementation,
        /// backed by FusionCache and <see cref="ITranslationProvider"/>, bound to an explicit configuration section.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this overload when your localization settings live under a non-default section name
        /// (e.g. <c>builder.Configuration.GetSection("MyApp:Localization")</c>).
        /// </para>
        /// <para>
        /// Use <paramref name="configure"/> to override or supplement values from configuration.
        /// Code-based values win over configuration values.
        /// </para>
        /// <para>
        /// Requires one or more <see cref="ITranslationProvider"/> implementations to be registered separately.
        /// Providers are tried in registration order — the first non-null result wins.
        /// </para>
        /// <para>
        /// If an <c>IDistributedCache</c> (e.g. Redis, SQLite) is already registered in the container,
        /// FusionCache will use it automatically as an L2 backing store.
        /// </para>
        /// </remarks>
        public ProviderBasedLocalizationBuilder AddProviderBasedLocalization(IConfigurationSection section,
            Action<ProviderBasedLocalizationOptions>? configure = null)
        {
            return AddProviderBasedLocalizationCore(services, section, configure);
        }
    }

    private static ProviderBasedLocalizationBuilder AddProviderBasedLocalizationCore(
        IServiceCollection services,
        IConfigurationSection? section,
        Action<ProviderBasedLocalizationOptions>? configure)
    {
        var options = new ProviderBasedLocalizationOptions();
        section?.Bind(options);
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IStringLocalizerFactory, ProviderBasedStringLocalizerFactory>();

        // StringLocalizer<T> routes IStringLocalizer<T> resolution through IStringLocalizerFactory.
        // Equivalent to what AddLocalization() registers, without pulling in its default factory.
        services.AddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));

        var cacheBuilder = services.AddFusionCache(options.CacheName)
            .WithOptions(o =>
            {
                o.CacheKeyPrefix = options.CacheName + ":";
                o.DistributedCacheErrorsLogLevel = LogLevel.Warning;
                o.FactorySyntheticTimeoutsLogLevel = LogLevel.Debug;

                // Factory misses for parent cultures (e.g. 'es' when only 'es-MX' has translations)
                // are expected behaviour — they use skip-cache flags to prevent caching null.
                // Demoted to Debug so genuine factory errors don't flood default log output.
                o.FactoryErrorsLogLevel = LogLevel.Debug;
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = options.TranslationDuration,
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = options.FailSafeMaxDuration,
            })
            .TryWithRegisteredDistributedCache()
            .WithSystemTextJsonSerializer();

        // FusionCache is an internal implementation detail — consumers did not opt in to its logs.
        // PostConfigure runs after appsettings binding, so we only add the Warning floor if the
        // consumer has not already set an explicit rule for the FusionCache namespace. This means
        // setting "ZiggyCreatures.Caching.Fusion": "Debug" in appsettings.json will still work.
        services.PostConfigure<LoggerFilterOptions>(filterOptions =>
        {
            const string fusionCacheNamespace = "ZiggyCreatures.Caching.Fusion";

            var hasExplicitRule = filterOptions.Rules.Any(r =>
                r.CategoryName is not null &&
                fusionCacheNamespace.StartsWith(r.CategoryName, StringComparison.OrdinalIgnoreCase) &&
                r.LogLevel.HasValue);

            if (!hasExplicitRule)
                filterOptions.Rules.Add(new LoggerFilterRule(null, fusionCacheNamespace, LogLevel.Warning, null));
        });

        return new ProviderBasedLocalizationBuilder(services, cacheBuilder, section);
    }

    /// <summary>
    /// Resolves an <see cref="IConfigurationSection"/> by finding the <see cref="IConfiguration"/>
    /// singleton already registered in DI and calling <see cref="IConfiguration.GetSection"/>.
    /// Returns <c>null</c> if no <see cref="IConfiguration"/> is registered.
    /// </summary>
    private static IConfigurationSection? ResolveConfigurationSection(IServiceCollection services, string sectionName)
    {
        var configDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConfiguration));

        if (configDescriptor?.ImplementationInstance is IConfiguration config)
            return config.GetSection(sectionName);

        return null;
    }
}
