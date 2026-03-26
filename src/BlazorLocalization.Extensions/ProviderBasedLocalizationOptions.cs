namespace BlazorLocalization.Extensions;

/// <summary>
/// Controls how <see cref="ProviderBasedStringLocalizer"/> caches translations.
/// </summary>
public sealed class ProviderBasedLocalizationOptions
{
    /// <summary>
    /// How long a cached translation is considered fresh.
    /// After this period FusionCache will refresh the translation from the provider in the background.
    /// Defaults to 1 hour.
    /// </summary>
    public TimeSpan TranslationDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum time a stale translation can be served when all providers fail at refresh time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After <see cref="TranslationDuration"/> expires, FusionCache refreshes in the background.
    /// If the provider is down (HTTP 5xx, network timeout, DNS failure), FusionCache
    /// <b>keeps serving the last successful value</b> instead of falling back to the
    /// source-language string. This property controls how long that stale value remains usable.
    /// </para>
    /// <para>
    /// Once this duration is exceeded without a successful refresh, the cache entry is evicted
    /// and the next request sees <see cref="Microsoft.Extensions.Localization.LocalizedString.ResourceNotFound"/>
    /// — the source-language fallback text supplied via <c>Translation("…")</c>.
    /// </para>
    /// <para>
    /// Defaults to 365 days — stale translations are served effectively indefinitely
    /// rather than falling back to the source-language string. This is the safest default for user-facing
    /// applications where showing untranslated text is worse than showing a slightly
    /// outdated translation. Omit from configuration to keep the default, or set a finite
    /// value for earlier eviction (e.g. <c>TimeSpan.FromDays(7)</c> / <c>"7.00:00:00"</c> in JSON).
    /// </para>
    /// </remarks>
    public TimeSpan FailSafeMaxDuration { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// The name of the FusionCache instance used internally for translation caching.
    /// Useful to avoid collisions if the application already uses a FusionCache instance.
    /// Defaults to <c>BlazorLocalization</c>.
    /// </summary>
    public string CacheName { get; set; } = "BlazorLocalization";
}
