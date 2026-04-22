using BlazorLocalization.Extensions.Translation.Definitions;
using Microsoft.Extensions.Localization;

namespace SampleBlazorApp;

// ═══════════════════════════════════════════════════════
// BL0003 — Duplicate translation key (static definitions)
// ═══════════════════════════════════════════════════════

/// <summary>
/// Correct definitions + "Showcase.Save" intentionally duplicated in <see cref="ConflictingDefinitions"/>.
/// </summary>
public static class AnalyzerShowcaseDefinitions
{
    // BL0003 sub-case A — also defined in ConflictingDefinitions ↓
    public static readonly SimpleDefinition SaveButton =
        TranslationDefinitions.DefineSimple("Showcase.Save", "Save");

    public static readonly SimpleDefinition CancelButton =
        TranslationDefinitions.DefineSimple("Showcase.Cancel", "Cancel");

    public static readonly PluralDefinition CartItems =
        TranslationDefinitions.DefinePlural("Showcase.CartItems")
            .One("{Count} item")
            .Other("{Count} items");

    public static readonly SelectDefinition<UserRole> RoleGreeting =
        TranslationDefinitions.DefineSelect<UserRole>("Showcase.RoleGreeting")
            .When(UserRole.Admin, "Welcome back, administrator")
            .When(UserRole.Member, "Hello, member")
            .Otherwise("Welcome, guest");

    public static readonly SelectPluralDefinition<UserRole> RoleNotifications =
        TranslationDefinitions.DefineSelectPlural<UserRole>("Showcase.RoleNotifications")
            .When(UserRole.Admin)
                .One("Admin: {Count} alert")
                .Other("Admin: {Count} alerts")
            .When(UserRole.Member)
                .One("You have {Count} notification")
                .Other("You have {Count} notifications")
            .Otherwise()
                .One("{Count} notification")
                .Other("{Count} notifications");
}

/// <summary>
/// Intentional conflicts to trigger BL0003 diagnostics.
/// </summary>
public static class ConflictingDefinitions
{
    // BL0003 sub-case A — duplicate key, different message
    public static readonly SimpleDefinition SaveAgain =
        TranslationDefinitions.DefineSimple("Showcase.Save", "Save changes");

    // BL0003 sub-case D — type mismatch: same key as Simple AND Plural
    public static readonly SimpleDefinition GreetingSimple =
        TranslationDefinitions.DefineSimple("Showcase.Mismatch.Greeting", "Hello");

    public static readonly PluralDefinition GreetingPlural =
        TranslationDefinitions.DefinePlural("Showcase.Mismatch.Greeting")
            .One("Hello")
            .Other("Hellos");

    // BL0003 sub-case D — type mismatch: same key as Select AND SelectPlural
    public static readonly SelectDefinition<UserRole> StatusSelect =
        TranslationDefinitions.DefineSelect<UserRole>("Showcase.Mismatch.Status")
            .When(UserRole.Admin, "Active")
            .Otherwise("Inactive");

    public static readonly SelectPluralDefinition<UserRole> StatusSelectPlural =
        TranslationDefinitions.DefineSelectPlural<UserRole>("Showcase.Mismatch.Status")
            .When(UserRole.Admin)
                .One("{Count} active")
                .Other("{Count} active")
            .Otherwise()
                .One("{Count} inactive")
                .Other("{Count} inactive");
}

// ═══════════════════════════════════════════════════════
// BL0004 — Constructor injection variant
// ═══════════════════════════════════════════════════════

/// <summary>
/// BL0004 fires on <c>IStringLocalizer&lt;GreetingService&gt;</c> in the constructor.
/// Also BL0002 on the GetString call.
/// </summary>
public class GreetingService(IStringLocalizer<GreetingService> localizer)
{
    public string GetGreeting() => localizer.GetString("Greeting.Hello");
}
