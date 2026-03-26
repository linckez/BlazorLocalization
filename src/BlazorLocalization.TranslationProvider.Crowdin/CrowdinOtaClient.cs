using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;
using BlazorLocalization.Extensions;
using BlazorLocalization.Extensions.Exceptions;
using BlazorLocalization.TranslationProvider.Crowdin.Models;
using BlazorLocalization.TranslationProvider.Crowdin.Parsing;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.TranslationProvider.Crowdin;

/// <summary>
/// Low-level HTTP client for the Crowdin OTA CDN.
/// Fetches the distribution manifest and translation files, both cached via FusionCache.
/// </summary>
/// <remarks>
/// This is an internal implementation detail created by
/// <see cref="CrowdinServiceCollectionExtensions.AddCrowdinTranslationProvider(ProviderBasedLocalizationBuilder, string, Action{CrowdinTranslationProviderOptions})"/>.
/// Each named provider registration gets its own <see cref="CrowdinOtaClient"/> instance
/// with a dedicated <see cref="HttpClient"/>, resolved options, and cache key prefix.
/// </remarks>
internal sealed class CrowdinOtaClient(
    IHttpClientFactory httpClientFactory,
    CrowdinTranslationProviderOptions options,
    string httpClientName,
    string providerName,
    IFusionCacheProvider cacheProvider,
    ProviderBasedLocalizationOptions cacheOptions,
    ILogger<CrowdinOtaClient> logger)
{
    private readonly ICrowdinFileParser _parser = new AndroidXmlParser();
    private readonly IFusionCache _cache = cacheProvider.GetCache(cacheOptions.CacheName);

    /// <summary>
    /// Fetches and parses the translation file for <paramref name="culture"/> from the CDN.
    /// </summary>
    /// <returns>
    /// A dictionary of key/value translations, or <c>null</c> if the culture has no content
    /// in the Crowdin distribution.
    /// </returns>
    /// <exception cref="TranslationProviderTransientException">
    /// CDN is unreachable, manifest is not valid JSON, response is not valid Android XML,
    /// or the parser extracted zero keys from a non-empty response. All are transient because
    /// the root cause (Crowdin dashboard settings, CDN availability) is external and can
    /// self-heal without redeploying the app.
    /// </exception>
    public async Task<Dictionary<string, string>?> GetTranslationsAsync(
        string culture,
        CancellationToken ct = default)
    {
        CrowdinManifest manifest;
        try
        {
            manifest = await GetManifestAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationProviderTransientException(
                providerName,
                $"Failed to fetch Crowdin OTA manifest (provider '{providerName}').",
                ex);
        }
        catch (JsonException ex)
        {
            // Bad DistributionHash or CDN returned an error page instead of JSON.
            throw new TranslationProviderTransientException(
                providerName,
                $"Crowdin OTA manifest is not valid JSON (provider '{providerName}'). " +
                "Verify your DistributionHash is correct.",
                ex);
        }

        var contentPath = ResolveCulturePath(manifest, culture);
        if (contentPath is null)
        {
            logger.LogDebug("No content path found for culture '{Culture}' in Crowdin manifest (provider '{ProviderName}')",
                culture, providerName);
            return null;
        }

        var url = $"{options.BaseUrl}/{options.DistributionHash}{contentPath}";
        logger.LogDebug("Downloading translation file from {Url} (provider '{ProviderName}')", url, providerName);
        var client = httpClientFactory.CreateClient(httpClientName);

        string raw;
        try
        {
            raw = await client.GetStringAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationProviderTransientException(
                providerName,
                $"Failed to download translation file for culture '{culture}' from Crowdin CDN (provider '{providerName}').",
                ex);
        }

        Dictionary<string, string> translations;
        try
        {
            translations = _parser.Parse(raw, culture);
        }
        catch (XmlException ex)
        {
            // CDN returned non-XML content (iOS Strings, PO, JSON from a community exporter).
            throw new TranslationProviderTransientException(
                providerName,
                $"Crowdin CDN response for culture '{culture}' is not valid Android XML (provider '{providerName}'). " +
                "Verify your CDN Distribution uses the Android XML export format.",
                ex);
        }

        // Valid XML but zero <string> elements — likely XLIFF or another XML-based format.
        if (translations.Count == 0 && raw.Length > 0)
        {
            throw new TranslationProviderTransientException(
                providerName,
                $"Crowdin CDN returned {raw.Length} bytes for culture '{culture}' but the parser " +
                $"extracted 0 translation keys (provider '{providerName}'). " +
                "Verify your CDN Distribution uses the Android XML export format.",
                new InvalidOperationException("Parser returned an empty dictionary from non-empty content."));
        }

        return translations;
    }

    /// <summary>
    /// Fetches the Crowdin OTA manifest via FusionCache.
    /// The manifest maps culture codes to CDN content paths.
    /// Cached with the same TTL as translations so it refreshes in sync.
    /// </summary>
    private async Task<CrowdinManifest> GetManifestAsync(CancellationToken ct)
    {
        return await _cache.GetOrSetAsync<CrowdinManifest>(
            $"crowdin:{providerName}:manifest",
            async (_, innerCt) =>
            {
                var url = $"{options.BaseUrl}/{options.DistributionHash}/manifest.json";
                logger.LogDebug("Fetching Crowdin OTA manifest from {Url} (provider '{ProviderName}')", url, providerName);
                var client = httpClientFactory.CreateClient(httpClientName);
                return (await client.GetFromJsonAsync<CrowdinManifest>(url, innerCt))!;
            },
            token: ct);
    }

    /// <summary>
    /// Tries exact culture match (e.g. <c>es-MX</c>), then falls back to the two-letter code (e.g. <c>es</c>).
    /// </summary>
    internal static string? ResolveCulturePath(CrowdinManifest manifest, string culture)
    {
        if (manifest.Content.TryGetValue(culture, out var paths) && paths.Length > 0)
            return paths[0];

        var twoLetter = new CultureInfo(culture).TwoLetterISOLanguageName;
        if (twoLetter != culture && manifest.Content.TryGetValue(twoLetter, out paths) && paths.Length > 0)
            return paths[0];

        return null;
    }

}
