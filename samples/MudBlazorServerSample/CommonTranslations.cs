using BlazorLocalization.Extensions.Translation.Definitions;
using static BlazorLocalization.Extensions.Translations;

namespace MudBlazorServerSample;

/// <summary>
/// Reusable translation definitions shared across components.
/// Define once, use everywhere via <c>Loc.Translation(definition)</c>.
/// </summary>
public static class CommonTranslations
{
    // ── Simple ──────────────────────────────────────────────────────

    public static readonly SimpleDefinitionBuilder SaveButton =
        Translate("Common.Save", "Save")
            .For("de", "Speichern")
            .For("pl", "Zapisz")
            .For("da", "Gem");

    // ── Plural ──────────────────────────────────────────────────────

    public static readonly PluralDefinitionBuilder CartItems =
        Translate("Common.CartItems")
            .One("{Count} item in your cart")
            .Other("{Count} items in your cart")
            .For("da")
            .One("{Count} vare i din kurv")
            .Other("{Count} varer i din kurv");

    // ── Select ──────────────────────────────────────────────────────

    public static readonly SelectDefinitionBuilder<UserTitle> TitleGreeting =
        Translate<UserTitle>("Common.TitleGreeting")
            .When(UserTitle.Mr, "Dear Mr. Smith")
            .When(UserTitle.Mrs, "Dear Mrs. Smith")
            .Otherwise("Dear customer")
            .For("da")
            .When(UserTitle.Mr, "Kære hr. Smith")
            .When(UserTitle.Mrs, "Kære fru Smith")
            .Otherwise("Kære kunde");

    // ── SelectPlural ────────────────────────────────────────────────

    public static readonly SelectPluralDefinitionBuilder<UserTitle> TitleInbox =
        Translate<UserTitle>("Common.TitleInbox", howMany: 0)
            .When(UserTitle.Mr)
            .One("Mr. Smith has {Count} message")
            .Other("Mr. Smith has {Count} messages")
            .When(UserTitle.Mrs)
            .One("Mrs. Smith has {Count} message")
            .Other("Mrs. Smith has {Count} messages")
            .Otherwise()
            .One("{Count} message")
            .Other("{Count} messages");
}
