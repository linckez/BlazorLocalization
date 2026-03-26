using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions;

/// <summary>
/// Builder returned by <see cref="LocalizationServiceCollectionExtensions.AddProviderBasedLocalization(IServiceCollection, IConfiguration, Action{ProviderBasedLocalizationOptions}?)"/>
/// that allows fluent registration of <see cref="ITranslationProvider"/> implementations.
/// </summary>
/// <remarks>
/// Follows the same pattern as <c>MicrosoftIdentityWebAppAuthenticationBuilder</c> — a thin wrapper
/// around <see cref="IServiceCollection"/> that provider packages extend with their own
/// <c>Add…</c> methods.
/// </remarks>
public sealed class ProviderBasedLocalizationBuilder(IServiceCollection services, IFusionCacheBuilder cacheBuilder, IConfigurationSection? configurationSection)
{
    private readonly HashSet<string> _registeredProviders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The application's service collection.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// The configuration section that was passed to <c>AddProviderBasedLocalization()</c>,
    /// or <c>null</c> if no configuration was provided.
    /// </summary>
    public IConfigurationSection? ConfigurationSection { get; } = configurationSection;

    /// <summary>
    /// Returns the <c>TranslationProviders</c> sub-section of <see cref="ConfigurationSection"/>,
    /// or <c>null</c> if no configuration section was provided.
    /// </summary>
    public IConfigurationSection? TranslationProvidersSection =>
        ConfigurationSection?.GetSection("TranslationProviders");

    /// <summary>
    /// Tracks a provider name to detect duplicate registrations.
    /// </summary>
    /// <exception cref="InvalidOperationException">A provider with the same name was already registered.</exception>
    public void TrackProviderName(string providerName)
    {
        if (!_registeredProviders.Add(providerName))
            throw new InvalidOperationException(
                $"A translation provider named '{providerName}' has already been registered. " +
                $"Use a unique name for each provider instance.");
    }

    /// <summary>
    /// Exposes the underlying FusionCache builder for advanced cache configuration
    /// (e.g. wiring a distributed cache, backplane, or custom serializer).
    /// </summary>
    /// <remarks>
    /// By default, <see cref="LocalizationServiceCollectionExtensions.AddProviderBasedLocalization(IServiceCollection, IConfiguration, Action{ProviderBasedLocalizationOptions}?)"/>
    /// uses <c>TryWithRegisteredDistributedCache()</c> — if an <c>IDistributedCache</c> is already
    /// in DI, FusionCache picks it up as L2 automatically. If none is registered, it runs L1-only.
    /// Use this method to override that or add additional infrastructure (e.g. a backplane).
    /// </remarks>
    public ProviderBasedLocalizationBuilder ConfigureCache(Action<IFusionCacheBuilder> configure)
    {
        configure(cacheBuilder);
        return this;
    }
}
