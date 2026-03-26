using System.Text.Json;
using BlazorLocalization.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace SampleBlazorApp.Services;

/// <summary>
/// Stub <see cref="ITranslationProvider"/> that simulates a Crowdin-like CDN —
/// translations arrive as a single JSON blob per culture and are fanned out into
/// individual FusionCache entries via a sentinel pattern.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the architecture of
/// <c>BlazorLocalization.TranslationProvider.Crowdin.CrowdinTranslationProvider</c>:
/// </para>
/// <list type="bullet">
///   <item>
///     A sentinel key <c>stub-fanout:culture:{culture}</c> gates the bulk "download".
///     When missing or expired, the JSON for that culture is deserialized and each key
///     is written to FusionCache individually. Subsequent key lookups are O(1) cache hits.
///   </item>
///   <item>
///     FusionCache's stampede protection ensures the JSON is parsed at most once per
///     TTL cycle, regardless of concurrent requests.
///   </item>
/// </list>
/// <para>
/// The JSON strings are hardcoded constants — no file I/O or HTTP involved.
/// A configurable delay (default 2 s) simulates CDN network latency on the first fetch.
/// </para>
/// </remarks>
public sealed class JsonFanoutTranslationProvider : ITranslationProvider
{
    private readonly IFusionCache _cache;
    private readonly ILogger<JsonFanoutTranslationProvider> _logger;
    private readonly TimeSpan _simulatedDelay;

    public JsonFanoutTranslationProvider(
        IFusionCacheProvider cacheProvider,
        ProviderBasedLocalizationOptions cacheOptions,
        ILogger<JsonFanoutTranslationProvider> logger,
        TimeSpan? simulatedDelay = null)
    {
        _cache = cacheProvider.GetCache(cacheOptions.CacheName);
        _logger = logger;
        _simulatedDelay = simulatedDelay ?? TimeSpan.FromSeconds(2);
    }

    #region Hardcoded JSON blobs (one per culture, flat key-value)

    private static readonly Dictionary<string, string> CultureJsonBlobs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-US"] = """
        {
            "Home.Title": "Welcome to Blazor",
            "Home.WelcomeMessage": "Hello! This is the localized home page.",
            "Home.RuntimeGreeting": "Loaded at runtime",
            "Home.VisitorCounter": "You have visited this page {VisitCount} times.",
            "Home.VisitorCounterLegacy": "You visited {0} times.",
            "Home.WelcomeBack": "Welcome back, {0}! You visited {1} times.",
            "Home.ItemCount": "Showing {Count} of {Total} items.",
            "Home.CartItems_one": "{Quantity} item in your cart",
            "Home.CartItems_other": "{Quantity} items in your cart",
            "Home.NamedArgs": "Explicit named args value",
            "Home.PluralNamed_one": "{Count} widget",
            "Home.PluralNamed_other": "{Count} widgets",
            "Home.PluralPositional_one": "{Count} item",
            "Home.PluralPositional_other": "{Count} items",
            "Greeting": "Welcome to our application!",
            "SaveButton": "Save Changes",
            "Common.Save": "Save",
            "Common.Cancel": "Cancel",
            "Common.Edit": "Edit",
            "Common.Delete": "Delete",
            "Common.Add": "Add",
            "Common.LastModified": "Last modified:"
        }
        """,

        ["da"] = """
        {
            "Home.Title": "Velkommen til Blazor",
            "Home.WelcomeMessage": "Hej! Dette er den lokaliserede startside.",
            "Home.RuntimeGreeting": "Indlæst ved kørsel",
            "Home.VisitorCounter": "Du har besøgt denne side {VisitCount} gange.",
            "Home.VisitorCounterLegacy": "Du har besøgt denne side {0} gange.",
            "Home.WelcomeBack": "Velkommen tilbage, {0}! Du har besøgt {1} gange.",
            "Home.ItemCount": "Viser {Count} af {Total} elementer.",
            "Home.CartItems_one": "{Quantity} vare i din kurv",
            "Home.CartItems_other": "{Quantity} varer i din kurv",
            "Home.NamedArgs": "Eksplicit navngivne argumenter",
            "Home.PluralNamed_one": "{Count} widget",
            "Home.PluralNamed_other": "{Count} widgets",
            "Home.PluralPositional_one": "{Count} genstand",
            "Home.PluralPositional_other": "{Count} genstande",
            "Greeting": "Velkommen til vores applikation!",
            "SaveButton": "Gem ændringer",
            "Common.Save": "Gem",
            "Common.Cancel": "Annuller",
            "Common.Edit": "Rediger",
            "Common.Delete": "Slet",
            "Common.Add": "Tilføj",
            "Common.LastModified": "Sidst ændret:"
        }
        """,

        ["es-MX"] = """
        {
            "Home.Title": "Bienvenido a Blazor",
            "Home.WelcomeMessage": "¡Hola! Esta es la página de inicio localizada.",
            "Home.RuntimeGreeting": "Cargado en tiempo de ejecución",
            "Home.VisitorCounter": "Has visitado esta página {VisitCount} veces.",
            "Home.VisitorCounterLegacy": "Has visitado esta página {0} veces.",
            "Home.WelcomeBack": "¡Bienvenido de nuevo, {0}! Has visitado {1} veces.",
            "Home.ItemCount": "Mostrando {Count} de {Total} elementos.",
            "Home.CartItems_one": "{Quantity} artículo en tu carrito",
            "Home.CartItems_other": "{Quantity} artículos en tu carrito",
            "Home.NamedArgs": "Valor de argumentos nombrados explícitos",
            "Home.PluralNamed_one": "{Count} widget",
            "Home.PluralNamed_other": "{Count} widgets",
            "Home.PluralPositional_one": "{Count} artículo",
            "Home.PluralPositional_other": "{Count} artículos",
            "Greeting": "¡Bienvenido a nuestra aplicación!",
            "SaveButton": "Guardar cambios",
            "Common.Save": "Guardar",
            "Common.Cancel": "Cancelar",
            "Common.Edit": "Editar",
            "Common.Delete": "Eliminar",
            "Common.Add": "Agregar",
            "Common.LastModified": "Última modificación:"
        }
        """,
    };

    #endregion

    /// <inheritdoc/>
    public async Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default)
    {
        var cacheKey = $"stub-fanout:{culture}:{key}";

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
    /// Uses a sentinel key so the JSON deserialization runs at most once per TTL cycle.
    /// </summary>
    private async Task EnsureCultureLoadedAsync(string culture, CancellationToken ct)
    {
        var sentinelKey = $"stub-fanout:culture:{culture}";

        await _cache.GetOrSetAsync<bool>(
            sentinelKey,
            async (_, innerCt) =>
            {
                _logger.LogDebug("JsonFanoutProvider: deserializing culture '{Culture}' (simulating CDN download)", culture);

                // Simulate CDN network latency.
                await Task.Delay(_simulatedDelay, innerCt);

                if (CultureJsonBlobs.TryGetValue(culture, out var json))
                {
                    var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;

                    _logger.LogDebug("JsonFanoutProvider: fanning out {Count} key(s) for culture '{Culture}'",
                        translations.Count, culture);

                    foreach (var (k, v) in translations)
                        await _cache.SetAsync($"stub-fanout:{culture}:{k}", v, token: innerCt);
                }
                else
                {
                    _logger.LogDebug("JsonFanoutProvider: no JSON blob for culture '{Culture}'", culture);
                }

                return true;
            },
            token: ct);
    }
}
