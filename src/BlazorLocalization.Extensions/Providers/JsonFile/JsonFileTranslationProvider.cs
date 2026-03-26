using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Providers.JsonFile;

/// <summary>
/// Reads translations from flat JSON files on disk using a sentinel+fan-out cache pattern.
/// </summary>
/// <remarks>
/// <para>
/// Each culture's file is read once per TTL cycle. Individual translation keys are fanned out
/// into FusionCache entries so subsequent lookups are O(1) cache hits with zero I/O.
/// </para>
/// <para>
/// Missing files are treated as "no translations for this culture" — the localizer walks the
/// culture fallback chain (<c>es-MX</c> → <c>es</c> → source text) automatically.
/// </para>
/// </remarks>
internal sealed class JsonFileTranslationProvider(
    string providerName,
    IFusionCacheProvider cacheProvider,
    ProviderBasedLocalizationOptions cacheOptions,
    JsonFileTranslationProviderOptions providerOptions,
    ILogger<JsonFileTranslationProvider> logger) : ITranslationProvider
{
    private readonly IFusionCache _cache = cacheProvider.GetCache(cacheOptions.CacheName);

    /// <inheritdoc/>
    public async Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default)
    {
        var cacheKey = $"jsonfile:{providerName}:{culture}:{key}";

        return await _cache.GetOrSetAsync<string?>(
            cacheKey,
            async (ctx, innerCt) =>
            {
                await EnsureCultureLoadedAsync(culture, innerCt);

                var result = await _cache.TryGetAsync<string>(cacheKey, token: innerCt);
                if (result.HasValue)
                {
                    ctx.Options.SkipMemoryCacheWrite = true;
                    ctx.Options.SkipDistributedCacheWrite = true;
                    return result.Value;
                }

                return null;
            },
            token: ct);
    }

    private async Task EnsureCultureLoadedAsync(string culture, CancellationToken ct)
    {
        var sentinelKey = $"jsonfile:{providerName}:culture:{culture}";

        await _cache.GetOrSetAsync<bool>(
            sentinelKey,
            async (_, innerCt) =>
            {
                try
                {
                    var fileName = providerOptions.FilePattern.Replace("{culture}", culture);
                    var filePath = Path.Combine(providerOptions.TranslationsPath, fileName);

                    if (!File.Exists(filePath))
                    {
                        logger.LogDebug("No JSON file found at '{FilePath}' for culture '{Culture}' (provider '{ProviderName}')",
                            filePath, culture, providerName);
                        return true;
                    }

                    logger.LogDebug("Loading translations from '{FilePath}' for culture '{Culture}' (provider '{ProviderName}')",
                        filePath, culture, providerName);

                    var content = await File.ReadAllTextAsync(filePath, innerCt);
                    var translations = FlatJsonParser.Parse(content);

                    logger.LogDebug("Loaded {Count} key(s) for culture '{Culture}' from '{FilePath}' (provider '{ProviderName}')",
                        translations.Count, culture, filePath, providerName);

                    foreach (var (k, v) in translations)
                        await _cache.SetAsync($"jsonfile:{providerName}:{culture}:{k}", v, token: innerCt);
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to load translations for culture '{Culture}' from disk " +
                        "(provider '{ProviderName}'). Will retry after TTL expires.",
                        culture, providerName);
                }

                return true;
            },
            token: ct);
    }
}
