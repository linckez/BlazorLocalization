using BlazorLocalization.Extensions.Translation.Definitions;
using static BlazorLocalization.Extensions.Translations;

namespace WebApiSample;

/// <summary>
/// Reusable translation definitions shared across API endpoints.
/// Define once, use everywhere via <c>loc.Translation(definition)</c>.
/// </summary>
public static class CommonTranslations
{
    // ── Simple ──────────────────────────────────────────────────────

    public static readonly SimpleDefinition ValidationFailed =
        DefineSimple("Api.Validation.Title", "Validation failed")
            .For("de", "Validierung fehlgeschlagen")
            .For("pl", "Walidacja nie powiodła się")
            .For("da", "Validering fejlede");

    // ── Plural ──────────────────────────────────────────────────────

    public static readonly PluralDefinition ResultCount =
        DefinePlural("Api.ResultCount")
            .One("{Count} result")
            .Other("{Count} results")
            .For("da")
            .One("{Count} resultat")
            .Other("{Count} resultater");

    // ── Select ──────────────────────────────────────────────────────

    public static readonly SelectDefinition<CustomerType> CustomerGreeting =
        DefineSelect<CustomerType>("Api.CustomerGreeting")
            .When(CustomerType.Premium, "Welcome back, valued customer")
            .Otherwise("Welcome back")
            .For("da")
            .When(CustomerType.Premium, "Velkommen tilbage, værdsat kunde")
            .Otherwise("Velkommen tilbage");

    // ── SelectPlural ────────────────────────────────────────────────

    public static readonly SelectPluralDefinition<CustomerType> NotificationSummary =
        DefineSelectPlural<CustomerType>("Api.NotificationSummary")
            .When(CustomerType.Premium)
            .One("{Count} priority notification")
            .Other("{Count} priority notifications")
            .Otherwise()
            .One("{Count} notification")
            .Other("{Count} notifications")
            .For("da")
            .When(CustomerType.Premium)
            .One("{Count} prioritetsbesked")
            .Other("{Count} prioritetsbeskeder")
            .Otherwise()
            .One("{Count} besked")
            .Other("{Count} beskeder");
}
