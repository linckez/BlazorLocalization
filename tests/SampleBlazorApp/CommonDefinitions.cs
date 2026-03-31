using BlazorLocalization.Extensions.Translation.Definitions;
using static BlazorLocalization.Extensions.Translation.Definitions.TranslationDefinitions;

namespace SampleBlazorApp;

/// <summary>
/// Reusable translation definitions shared across components.
/// Each definition is created once and resolved at call time via <c>Loc.Translation(definition)</c>.
/// </summary>
public static class CommonDefinitions
{
    // ── Simple ──────────────────────────────────────────────────────

    public static readonly SimpleDefinition SaveButton =
        DefineSimple("Def.Save", "Save")
            .For("da", "Gem")
            .For("es-MX", "Guardar");

    // ── Plural ──────────────────────────────────────────────────────

    public static readonly PluralDefinition CartItems =
        DefinePlural("Def.Cart")
            .One("{ItemCount} item")
            .Other("{ItemCount} items")
            .For("da")
            .One("{ItemCount} vare")
            .Other("{ItemCount} varer");

    // ── Select ──────────────────────────────────────────────────────

    public static readonly SelectDefinition<TestCategory> CategoryGreeting =
        DefineSelect<TestCategory>("Def.Greeting")
            .When(TestCategory.Alpha, "Hello Alpha")
            .When(TestCategory.Beta, "Hello Beta")
            .Otherwise("Hello friend")
            .For("da")
            .When(TestCategory.Alpha, "Hej Alfa")
            .When(TestCategory.Beta, "Hej Beta")
            .Otherwise("Hej ven");

    // ── SelectPlural ────────────────────────────────────────────────

    public static readonly SelectPluralDefinition<TestCategory> CategoryInbox =
        DefineSelectPlural<TestCategory>("Def.Inbox")
            .When(TestCategory.Alpha)
            .One("{ItemCount} Alpha message")
            .Other("{ItemCount} Alpha messages")
            .Otherwise()
            .One("{ItemCount} message")
            .Other("{ItemCount} messages")
            .For("da")
            .When(TestCategory.Alpha)
            .One("{ItemCount} Alfa-besked")
            .Other("{ItemCount} Alfa-beskeder")
            .Otherwise()
            .One("{ItemCount} besked")
            .Other("{ItemCount} beskeder");
}
