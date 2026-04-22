namespace BlazorLocalization.Extensions;

/// <summary>
/// Resolves translations for a given culture and key.
/// </summary>
/// <remarks>
/// <para>
/// Implementations fetch from an external source such as Crowdin, a database, or a static file.
/// The calling layer (ProviderBasedStringLocalizer) manages per-key TTL and fail-safe;
/// providers do not need to handle freshness or retry logic at that level.
/// </para>
/// <para>
/// Providers <b>may</b> use internal caching (e.g. FusionCache with a provider-specific key prefix)
/// to bridge impedance mismatches — for example, when the external source returns an entire culture
/// file per HTTP call but the interface is per-key. In that case, the provider should use the same
/// TranslationDuration option to stay in sync with the outer cache layer.
/// </para>
/// </remarks>
public interface ITranslationProvider
{
    /// <summary>
    /// Returns the translation for <paramref name="key"/> in <paramref name="culture"/>,
    /// or <c>null</c> if no translation exists.
    /// </summary>
    /// <param name="culture">The culture name, e.g. <c>da</c> or <c>es-MX</c>.</param>
    /// <param name="key">The translation key, e.g. <c>Home.Title</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default);
}
