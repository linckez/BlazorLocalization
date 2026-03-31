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

    public static readonly SimpleDefinitionBuilder ValidationFailed =
        Translate("Api.Validation.Title", "Validation failed")
            .For("de", "Validierung fehlgeschlagen")
            .For("pl", "Walidacja nie powiodła się")
            .For("da", "Validering fejlede");

    // ── Plural ──────────────────────────────────────────────────────

    public static readonly PluralDefinitionBuilder ResultCount =
        Translate("Api.ResultCount")
            .One("{Count} result")
            .Other("{Count} results")
            .For("da")
            .One("{Count} resultat")
            .Other("{Count} resultater");

    // ── Select ──────────────────────────────────────────────────────

    public static readonly SelectDefinitionBuilder<CustomerType> CustomerGreeting =
        Translate<CustomerType>("Api.CustomerGreeting")
            .When(CustomerType.Premium, "Welcome back, valued customer")
            .Otherwise("Welcome back")
            .For("da")
            .When(CustomerType.Premium, "Velkommen tilbage, værdsat kunde")
            .Otherwise("Velkommen tilbage");

    // ── SelectPlural ────────────────────────────────────────────────

    public static readonly SelectPluralDefinitionBuilder<CustomerType> NotificationSummary =
        Translate<CustomerType>("Api.NotificationSummary", howMany: 0)
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
