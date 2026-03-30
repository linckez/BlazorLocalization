using BlazorLocalization.Extensions.Translation.Definitions;

namespace BlazorLocalization.Extensions;

/// <summary>
/// Static factory for reusable translation definitions. Define translations once,
/// use everywhere via <c>Loc.Translate(definition)</c>.
/// </summary>
/// <remarks>
/// Translation definitions capture the key, source text, and inline translations
/// without needing an <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/>.
/// Store them as static fields in a shared class:
/// <code>
/// public static class CommonTranslations
/// {
///     public static readonly SimpleDefinitionBuilder SaveButton =
///         Translate.Simple("Common.Save", "Save")
///             .For("da", "Gem");
///
///     public static readonly PluralDefinitionBuilder CartItems =
///         Translate.Plural("Cart.Items")
///             .One("{ItemCount} item in your cart")
///             .Other("{ItemCount} items in your cart");
/// }
/// </code>
/// </remarks>
public static class Translate
{
    /// <summary>
    /// Creates a simple translation definition with a key and source text.
    /// Chain <see cref="SimpleDefinitionBuilder.For"/> to add inline translations.
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Common.Save"</c>.</param>
    /// <param name="message">
    /// The original text. Used as fallback when your translation providers
    /// don't have a translation for the user's language.
    /// </param>
    public static SimpleDefinitionBuilder Simple(string key, string message)
        => new(key, message);

    /// <summary>
    /// Creates a plural translation definition with a key.
    /// Chain <c>.One()</c>, <c>.Other()</c>, etc. to define each plural form.
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Cart.Items"</c>.</param>
    public static PluralDefinitionBuilder Plural(string key)
        => new(key);

    /// <summary>
    /// Creates a select translation definition that varies by an enum value.
    /// Chain <c>.When()</c> for each case and <c>.Otherwise()</c> for the default.
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Greeting"</c>.</param>
    /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
    public static SelectDefinitionBuilder<TSelect> Select<TSelect>(string key) where TSelect : Enum
        => new(key);

    /// <summary>
    /// Creates a select + plural translation definition that varies by both an enum value and a quantity.
    /// Chain <c>.When()</c> to start a variant, then <c>.One()</c>, <c>.Other()</c>, etc. for plural forms.
    /// </summary>
    /// <param name="key">A unique identifier for this translation.</param>
    /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
    public static SelectPluralDefinitionBuilder<TSelect> SelectPlural<TSelect>(string key) where TSelect : Enum
        => new(key);
}
