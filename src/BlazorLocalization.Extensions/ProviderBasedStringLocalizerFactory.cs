using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions;

/// <summary>
/// Factory that creates <see cref="ProviderBasedStringLocalizer"/> instances.
/// Ignores the resource source — all translations live in one flat namespace per culture.
/// Resolves the named <c>BlazorLocalization</c> FusionCache instance via <see cref="IFusionCacheProvider"/>.
/// All registered <see cref="ITranslationProvider"/> implementations are tried in registration order.
/// </summary>
public class ProviderBasedStringLocalizerFactory(
    IFusionCacheProvider cacheProvider,
    IEnumerable<ITranslationProvider> translationProviders,
    ILoggerFactory loggerFactory,
    ProviderBasedLocalizationOptions options) : IProviderBasedStringLocalizerFactory
{
    private readonly IFusionCache _cache = cacheProvider.GetCache(options.CacheName);
    private readonly IReadOnlyList<ITranslationProvider> _providers = [.. translationProviders];
    private readonly ILogger<ProviderBasedStringLocalizer> _logger = loggerFactory.CreateLogger<ProviderBasedStringLocalizer>();

    public IStringLocalizer Create(Type resourceSource) =>
        new ProviderBasedStringLocalizer(_cache, _providers, _logger);

    public IStringLocalizer Create(string baseName, string location) =>
        new ProviderBasedStringLocalizer(_cache, _providers, _logger);
}
