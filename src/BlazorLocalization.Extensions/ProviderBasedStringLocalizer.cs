using System.Globalization;
using BlazorLocalization.Extensions.Exceptions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions;

/// <summary>
/// <see cref="IStringLocalizer"/> backed by <see cref="IFusionCache"/>.
/// This is infrastructure — application code calls <c>Translation()</c> on the injected
/// <see cref="IStringLocalizer"/>, which adds source-text fallback, named placeholders,
/// and plural support on top of this raw interface implementation.
/// </summary>
/// <remarks>
/// Fetches translations on-demand from one or more <see cref="ITranslationProvider"/> instances
/// and caches them. Providers are tried in registration order — the first non-null result wins.
/// Returns <see cref="LocalizedString.ResourceNotFound"/> on a cold miss while the background
/// fetch completes. Walks the culture fallback chain: exact → parent (e.g. <c>da-DK</c> → <c>da</c>).
/// </remarks>
public class ProviderBasedStringLocalizer(
    IFusionCache cache,
    IEnumerable<ITranslationProvider> providers,
    ILogger<ProviderBasedStringLocalizer> logger) : IStringLocalizer
{
    private readonly IReadOnlyList<ITranslationProvider> _providers = [.. providers];
    private volatile bool _invariantCultureWarned;

    /// <inheritdoc/>
    /// <remarks>
    /// Infrastructure indexer implementing <see cref="IStringLocalizer"/>.
    /// Application code should use <c>Translation()</c> which adds source-text fallback,
    /// named placeholders, and plural support.
    /// </remarks>
    public LocalizedString this[string name]
    {
        get
        {
            var value = GetLocalizedString(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Legacy positional-placeholder path. Prefer <c>Translation(key, sourceText, args)</c>
    /// which provides source-text fallback on cache misses.
    /// </remarks>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var raw = this[name];
            if (raw.ResourceNotFound)
                return raw;

            try
            {
                return new LocalizedString(name, string.Format(raw.Value, arguments), false);
            }
            catch (FormatException ex)
            {
                logger.LogWarning(
                    ex,
                    "Translation for '{Key}' contains invalid format placeholders — " +
                    "returning the unformatted translation. Fix the translation string.",
                    name);
                return new LocalizedString(name, raw.Value, false);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Always returns an empty sequence. <see cref="IFusionCache"/> has no key enumeration API,
    /// so there is nothing to return. Returning empty instead of throwing ensures third-party
    /// components that enumerate <see cref="IStringLocalizer"/> don't crash the host application.
    /// </remarks>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        [];
    
    /// <summary>
    /// Resolves a translation by walking the culture fallback chain.
    /// Returns <c>null</c> if no translation is found — the caller supplies the fallback.
    /// </summary>
    private string? GetLocalizedString(string key)
    {
        var hasCulture = false;
        foreach (var cultureName in GetCultureFallbackChain())
        {
            hasCulture = true;
        {
            var cacheKey = $"locale_{cultureName}_{key}";

            // Fast sync path — return immediately if already cached.
            var cached = cache.TryGet<string>(cacheKey);
            if (cached.HasValue)
                return cached.Value;

            // Cold miss — fire background fetch and continue to next culture in the chain.
            // The ValueTask is intentionally discarded: FusionCache's internal memory locker
            // ensures only one factory runs per key regardless of concurrent callers.
            // When no provider returns a value, the factory sets SkipMemoryCacheWrite and
            // SkipDistributedCacheWrite to prevent FusionCache from caching null — avoiding
            // ghost entries for intermediate cultures (e.g. the neutral 'es' parent of 'es-MX').
#pragma warning disable CA2012
            _ = cache.GetOrSetAsync<string?>(
                cacheKey,
                async (ctx, ct) =>
                {
                    foreach (var p in _providers)
                    {
                        try
                        {
                            var translation = await p.GetTranslationAsync(cultureName, key, ct);
                            if (translation is not null)
                                return translation;
                        }
                        catch (TranslationProviderConfigurationException ex)
                        {
                            logger.LogWarning(
                                ex,
                                "Translation provider '{ProviderName}' is misconfigured — this will not self-heal. " +
                                "Check your provider setup.",
                                ex.ProviderName);
                        }
                        catch (TranslationProviderTransientException ex)
                        {
                            logger.LogDebug(
                                ex,
                                "Translation provider '{ProviderName}' failed transiently for " +
                                "'{Culture}:{Key}' — trying next provider",
                                ex.ProviderName, cultureName, key);
                        }
                    }

                    ctx.Options.SkipMemoryCacheWrite = true;
                    ctx.Options.SkipDistributedCacheWrite = true;
                    return null;
                }
            );
#pragma warning restore CA2012
            }
        }

        if (!hasCulture && !_invariantCultureWarned)
        {
            _invariantCultureWarned = true;
            logger.LogWarning(
                "No language is set for the current request — translations will fall back to source text. " +
                "This usually means the app is missing app.UseRequestLocalization() in Program.cs. " +
                "See the ASP.NET Core globalization and localization docs for setup guidance.");
        }

        return null;
    }

    /// <summary>
    /// Yields the culture fallback chain for <see cref="CultureInfo.CurrentUICulture"/>.
    /// Walks from specific to general — e.g. <c>da-DK</c> → <c>da</c> — so that a user with
    /// a regional culture still matches a generic language translation.
    /// </summary>
    private static IEnumerable<string> GetCultureFallbackChain()
    {
        var culture = CultureInfo.CurrentUICulture;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (culture.Name != string.Empty)
        {
            if (seen.Add(culture.Name))
                yield return culture.Name;

            culture = culture.Parent;
        }
    }
}