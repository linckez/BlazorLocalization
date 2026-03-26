using BlazorLocalization.Extensions;
using BlazorLocalization.Extensions.Exceptions;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.TranslationProvider.Crowdin;

/// <summary>
/// Implements <see cref="ITranslationProvider"/> by fetching translations from the Crowdin OTA CDN.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ITranslationProvider.GetTranslationAsync"/> is a per-key API, but Crowdin's CDN
/// returns an entire file of translations per culture in a single HTTP request.
/// This class bridges that impedance mismatch using FusionCache with a culture-sentinel pattern:
/// </para>
/// <list type="bullet">
///   <item>
///     Each key is stored as <c>crowdin:{providerName}:{culture}:{key}</c> in the shared FusionCache instance,
///     where <c>providerName</c> is the name passed to
///     <see cref="CrowdinServiceCollectionExtensions.AddCrowdinTranslationProvider(ProviderBasedLocalizationBuilder, string, Action{CrowdinTranslationProviderOptions})"/>.
///   </item>
///   <item>
///     A sentinel key <c>crowdin:{providerName}:culture:{culture}</c> gates the bulk HTTP fetch.
///     When the sentinel is missing or expired, the entire culture file is fetched from the CDN
///     and all keys are fanned out into individual cache entries in one operation.
///   </item>
///   <item>
///     Subsequent key lookups for the same culture are O(1) cache hits with zero network cost.
///     FusionCache's stampede protection ensures only one HTTP call per culture per TTL cycle.
///   </item>
/// </list>
/// <para>
/// Provider entries use the same <see cref="ProviderBasedLocalizationOptions.TranslationDuration"/>
/// as the outer factory layer — one knob controls the end-to-end CDN refresh rate.
/// L2 persistence (e.g. SQLite, Redis) is inherited automatically, so translations survive
/// app restarts without a cold-start HTTP storm.
/// </para>
/// <para>
/// Multiple Crowdin providers can coexist — each gets a unique <c>providerName</c> prefix
/// that prevents cache key collisions between different OTA distributions.
/// </para>
/// </remarks>
internal sealed class CrowdinTranslationProvider(
    CrowdinOtaClient client,
    string providerName,
    IFusionCacheProvider cacheProvider,
    ProviderBasedLocalizationOptions cacheOptions,
    ILogger<CrowdinTranslationProvider> logger) : ITranslationProvider
{
    private readonly IFusionCache _cache = cacheProvider.GetCache(cacheOptions.CacheName);

    /// <inheritdoc/>
    public async Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default)
    {
        var cacheKey = $"crowdin:{providerName}:{culture}:{key}";

        return await _cache.GetOrSetAsync<string?>(
            cacheKey,
            async (ctx, innerCt) =>
            {
                await EnsureCultureLoadedAsync(culture, innerCt);

                // After fan-out, the key should be in cache. Try to get it directly.
                var result = await _cache.TryGetAsync<string>(cacheKey, token: innerCt);
                if (result.HasValue)
                {
                    // Tell FusionCache not to overwrite the entry we just set during fan-out,
                    // preserving its TTL alignment with the rest of the culture's keys.
                    ctx.Options.SkipMemoryCacheWrite = true;
                    ctx.Options.SkipDistributedCacheWrite = true;
                    return result.Value;
                }

                // Key genuinely does not exist in this culture.
                return null;
            },
            token: ct);
    }

    /// <summary>
    /// Ensures all translation keys for <paramref name="culture"/> are populated in the cache.
    /// Uses a sentinel key so the bulk HTTP fetch runs at most once per TTL cycle,
    /// regardless of how many individual keys are requested concurrently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All runtime errors are transient (the root cause — Crowdin dashboard settings, CDN
    /// availability — is external and can self-heal without redeploying the app). On failure
    /// the sentinel is still set to <c>true</c>, so the next retry won't happen until the
    /// TTL expires — the TTL acts as the natural backoff window. FusionCache's fail-safe
    /// keeps serving previously cached translations in the meantime.
    /// </para>
    /// <para>
    /// Log level varies by root cause: network errors at <c>Debug</c> (expected, temporary),
    /// format/parse errors at <c>Warning</c> (someone changed the Crowdin CDN Distribution
    /// export format and needs to fix it).
    /// </para>
    /// </remarks>
    private async Task EnsureCultureLoadedAsync(string culture, CancellationToken ct)
    {
        var sentinelKey = $"crowdin:{providerName}:culture:{culture}";

        await _cache.GetOrSetAsync<bool>(
            sentinelKey,
            async (_, innerCt) =>
            {
                try
                {
                    logger.LogDebug("Fetching translations for culture '{Culture}' from Crowdin CDN (provider '{ProviderName}')",
                        culture, providerName);
                    var translations = await client.GetTranslationsAsync(culture, innerCt);

                    if (translations is not null)
                    {
                        logger.LogDebug("Loaded {Count} key(s) for culture '{Culture}' from Crowdin CDN (provider '{ProviderName}')",
                            translations.Count, culture, providerName);

                        foreach (var (k, v) in translations)
                            await _cache.SetAsync($"crowdin:{providerName}:{culture}:{k}", v, token: innerCt);
                    }
                    else
                    {
                        logger.LogDebug("No translations found for culture '{Culture}' in Crowdin CDN (provider '{ProviderName}')",
                            culture, providerName);
                    }
                }
                catch (TranslationProviderTransientException ex) when (ex.InnerException is HttpRequestException)
                {
                    logger.LogDebug(
                        ex,
                        "Transient network failure fetching translations for culture '{Culture}' from Crowdin CDN " +
                        "(provider '{ProviderName}'). Will retry after TTL expires. " +
                        "FusionCache fail-safe serves stale translations in the meantime.",
                        culture, providerName);
                }
                catch (TranslationProviderTransientException ex)
                {
                    // Format/parse errors — someone changed the CDN Distribution export format.
                    logger.LogWarning(
                        ex,
                        "Crowdin CDN returned an unexpected format for culture '{Culture}' " +
                        "(provider '{ProviderName}'). Verify the CDN Distribution uses the " +
                        "Android XML export format. Will retry after TTL expires.",
                        culture, providerName);
                }

                // Always set the sentinel — the TTL window is the backoff.
                return true;
            },
            token: ct);
    }
}
