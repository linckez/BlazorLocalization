using BlazorLocalization.Extensions;

namespace SampleBlazorApp.Services;

/// <summary>
/// Stub <see cref="ITranslationProvider"/> that simulates a plain SQL database —
/// one row per (culture, key) pair, no batching, no fan-out.
/// </summary>
/// <remarks>
/// Each <see cref="GetTranslationAsync"/> call is an independent lookup with a small
/// simulated delay (200 ms by default) to mimic database round-trip latency.
/// FusionCache in <see cref="ProviderBasedStringLocalizer"/> absorbs the delay after the
/// first fetch — subsequent requests are cache hits.
/// </remarks>
public sealed class DictionaryTranslationProvider(
    ILogger<DictionaryTranslationProvider> logger,
    TimeSpan? simulatedDelay = null) : ITranslationProvider
{
    private readonly TimeSpan _delay = simulatedDelay ?? TimeSpan.FromMilliseconds(200);

    private static readonly Dictionary<(string Culture, string Key), string> Translations = new()
    {
        // ── en-US ──
        [("en-US", "Home.Title")] = "Welcome to Blazor",
        [("en-US", "Home.WelcomeMessage")] = "Hello! This is the localized home page.",
        [("en-US", "Home.RuntimeGreeting")] = "Loaded at runtime",
        [("en-US", "Home.VisitorCounter")] = "You have visited this page {VisitCount} times.",
        [("en-US", "Home.VisitorCounterLegacy")] = "You visited {0} times.",
        [("en-US", "Home.WelcomeBack")] = "Welcome back, {0}! You visited {1} times.",
        [("en-US", "Home.ItemCount")] = "Showing {Count} of {Total} items.",
        [("en-US", "Home.CartItems_one")] = "{Quantity} item in your cart",
        [("en-US", "Home.CartItems_other")] = "{Quantity} items in your cart",
        [("en-US", "Home.NamedArgs")] = "Explicit named args value",
        [("en-US", "Home.PluralNamed_one")] = "{Count} widget",
        [("en-US", "Home.PluralNamed_other")] = "{Count} widgets",
        [("en-US", "Home.PluralPositional_one")] = "{Count} item",
        [("en-US", "Home.PluralPositional_other")] = "{Count} items",
        [("en-US", "Greeting")] = "Welcome to our application!",
        [("en-US", "SaveButton")] = "Save Changes",
        [("en-US", "Common.Save")] = "Save",
        [("en-US", "Common.Cancel")] = "Cancel",
        [("en-US", "Common.Edit")] = "Edit",
        [("en-US", "Common.Delete")] = "Delete",
        [("en-US", "Common.Add")] = "Add",
        [("en-US", "Common.LastModified")] = "Last modified:",

        // ── da ──
        [("da", "Home.Title")] = "Velkommen til Blazor",
        [("da", "Home.WelcomeMessage")] = "Hej! Dette er den lokaliserede startside.",
        [("da", "Home.RuntimeGreeting")] = "Indlæst ved kørsel",
        [("da", "Home.VisitorCounter")] = "Du har besøgt denne side {VisitCount} gange.",
        [("da", "Home.VisitorCounterLegacy")] = "Du har besøgt denne side {0} gange.",
        [("da", "Home.WelcomeBack")] = "Velkommen tilbage, {0}! Du har besøgt {1} gange.",
        [("da", "Home.ItemCount")] = "Viser {Count} af {Total} elementer.",
        [("da", "Home.CartItems_one")] = "{Quantity} vare i din kurv",
        [("da", "Home.CartItems_other")] = "{Quantity} varer i din kurv",
        [("da", "Home.NamedArgs")] = "Eksplicit navngivne argumenter",
        [("da", "Home.PluralNamed_one")] = "{Count} widget",
        [("da", "Home.PluralNamed_other")] = "{Count} widgets",
        [("da", "Home.PluralPositional_one")] = "{Count} genstand",
        [("da", "Home.PluralPositional_other")] = "{Count} genstande",
        [("da", "Greeting")] = "Velkommen til vores applikation!",
        [("da", "SaveButton")] = "Gem ændringer",
        [("da", "Common.Save")] = "Gem",
        [("da", "Common.Cancel")] = "Annuller",
        [("da", "Common.Edit")] = "Rediger",
        [("da", "Common.Delete")] = "Slet",
        [("da", "Common.Add")] = "Tilføj",
        [("da", "Common.LastModified")] = "Sidst ændret:",

        // ── es-MX ──
        [("es-MX", "Home.Title")] = "Bienvenido a Blazor",
        [("es-MX", "Home.WelcomeMessage")] = "¡Hola! Esta es la página de inicio localizada.",
        [("es-MX", "Home.RuntimeGreeting")] = "Cargado en tiempo de ejecución",
        [("es-MX", "Home.VisitorCounter")] = "Has visitado esta página {VisitCount} veces.",
        [("es-MX", "Home.VisitorCounterLegacy")] = "Has visitado esta página {0} veces.",
        [("es-MX", "Home.WelcomeBack")] = "¡Bienvenido de nuevo, {0}! Has visitado {1} veces.",
        [("es-MX", "Home.ItemCount")] = "Mostrando {Count} de {Total} elementos.",
        [("es-MX", "Home.CartItems_one")] = "{Quantity} artículo en tu carrito",
        [("es-MX", "Home.CartItems_other")] = "{Quantity} artículos en tu carrito",
        [("es-MX", "Home.NamedArgs")] = "Valor de argumentos nombrados explícitos",
        [("es-MX", "Home.PluralNamed_one")] = "{Count} widget",
        [("es-MX", "Home.PluralNamed_other")] = "{Count} widgets",
        [("es-MX", "Home.PluralPositional_one")] = "{Count} artículo",
        [("es-MX", "Home.PluralPositional_other")] = "{Count} artículos",
        [("es-MX", "Greeting")] = "¡Bienvenido a nuestra aplicación!",
        [("es-MX", "SaveButton")] = "Guardar cambios",
        [("es-MX", "Common.Save")] = "Guardar",
        [("es-MX", "Common.Cancel")] = "Cancelar",
        [("es-MX", "Common.Edit")] = "Editar",
        [("es-MX", "Common.Delete")] = "Eliminar",
        [("es-MX", "Common.Add")] = "Agregar",
        [("es-MX", "Common.LastModified")] = "Última modificación:",
    };

    /// <inheritdoc/>
    public async Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default)
    {
        logger.LogDebug("DictionaryProvider: looking up [{Culture}] {Key}", culture, key);

        await Task.Delay(_delay, ct);

        var found = Translations.TryGetValue((culture, key), out var value);

        logger.LogDebug("DictionaryProvider: [{Culture}] {Key} → {Result}", culture, key,
            found ? value : "(not found)");

        return found ? value : null;
    }
}
