using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Providers.PoFile;

/// <summary>
/// Reads translations from GNU gettext PO files on disk using a sentinel+fan-out cache pattern.
/// </summary>
/// <remarks>
/// <para>
/// Each culture's PO file is read once per TTL cycle. Individual translation keys are fanned out
/// into FusionCache entries so subsequent lookups are O(1) cache hits with zero I/O.
/// </para>
/// <para>
/// Plural entries (<c>msgid_plural</c>/<c>msgstr[0]</c>/<c>msgstr[1]</c>) are emitted as
/// <c>key_one</c>/<c>key_other</c> suffixes, matching SmartFormat's plural resolution.
/// </para>
/// </remarks>
internal sealed class PoFileTranslationProvider(
    string providerName,
    IFusionCacheProvider cacheProvider,
    ProviderBasedLocalizationOptions cacheOptions,
    PoFileTranslationProviderOptions providerOptions,
    ILogger<PoFileTranslationProvider> logger) : ITranslationProvider
{
    private readonly IFusionCache _cache = cacheProvider.GetCache(cacheOptions.CacheName);

    /// <inheritdoc/>
    public async Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default)
    {
        var cacheKey = $"pofile:{providerName}:{culture}:{key}";

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

    /// <summary>
    /// Ensures all translation keys for <paramref name="culture"/> are populated in the cache.
    /// Uses a sentinel key so the file is read at most once per TTL cycle.
    /// </summary>
    private async Task EnsureCultureLoadedAsync(string culture, CancellationToken ct)
    {
        var sentinelKey = $"pofile:{providerName}:culture:{culture}";

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
                        logger.LogDebug("No PO file found at '{FilePath}' for culture '{Culture}' (provider '{ProviderName}')",
                            filePath, culture, providerName);
                        return true;
                    }

                    logger.LogDebug("Loading translations from '{FilePath}' for culture '{Culture}' (provider '{ProviderName}')",
                        filePath, culture, providerName);

                    var content = await File.ReadAllTextAsync(filePath, innerCt);
                    var translations = PoFileParser.Parse(content, culture);

                    logger.LogDebug("Loaded {Count} key(s) for culture '{Culture}' from '{FilePath}' (provider '{ProviderName}')",
                        translations.Count, culture, filePath, providerName);

                    foreach (var (k, v) in translations)
                        await _cache.SetAsync($"pofile:{providerName}:{culture}:{k}", v, token: innerCt);
                }
                catch (IOException ex)
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
