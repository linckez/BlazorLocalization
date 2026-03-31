using BlazorLocalization.Extensions.Translation.Definitions;

namespace BlazorLocalization.Extensions;

/// <summary>
/// Static factory for reusable translation definitions. Define translations once,
/// use everywhere via <c>Loc.Translation(definition)</c>.
/// </summary>
/// <remarks>
/// Add <c>using static BlazorLocalization.Extensions.Translations;</c> to call
/// <c>Translate()</c> directly:
/// <code>
/// using static BlazorLocalization.Extensions.Translations;
///
/// public static class CommonTranslations
/// {
///     // Simple — key + source text:
///     public static readonly SimpleDefinitionBuilder SaveButton =
///         Translate("Common.Save", "Save")
///             .For("da", "Gem");
///
///     // Plural — key only, forms from chain:
///     public static readonly PluralDefinitionBuilder CartItems =
///         Translate("Cart.Items")
///             .One("{ItemCount} item in your cart")
///             .Other("{ItemCount} items in your cart");
///
///     // Select — generic enum type:
///     public static readonly SelectDefinitionBuilder&lt;UserTitle&gt; Greeting =
///         Translate&lt;UserTitle&gt;("Home.Greeting")
///             .When(UserTitle.Mr, "Dear Mr. Smith")
///             .Otherwise("Dear customer");
/// }
/// </code>
/// </remarks>
public static class Translations
{
    /// <summary>
    /// Creates a simple translation definition with a key and source text.
    /// Chain <see cref="SimpleDefinitionBuilder.For"/> to add inline translations.
    /// <code>
    /// Translate(key: "Common.Save", message: "Save")
    ///     .For("da", "Gem")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Common.Save"</c>.</param>
    /// <param name="message">
    /// The original text. Used as fallback when your translation providers
    /// don't have a translation for the user's language.
    /// </param>
    public static SimpleDefinitionBuilder Translate(string key, string message)
        => new(key, message);

    /// <summary>
    /// Creates a plural translation definition with a key.
    /// Chain <c>.One()</c>, <c>.Other()</c>, etc. to define each plural form.
    /// <code>
    /// Translate(key: "Cart.Items")
    ///     .One("{ItemCount} item in your cart")
    ///     .Other("{ItemCount} items in your cart")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Cart.Items"</c>.</param>
    public static PluralDefinitionBuilder Translate(string key)
        => new(key);

    /// <summary>
    /// Creates a select translation definition that varies by an enum value.
    /// Chain <c>.When()</c> for each case and <c>.Otherwise()</c> for the default.
    /// <code>
    /// Translate&lt;UserTitle&gt;(key: "Home.Greeting")
    ///     .When(UserTitle.Mr, "Dear Mr. Smith")
    ///     .Otherwise("Dear customer")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Greeting"</c>.</param>
    /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
    public static SelectDefinitionBuilder<TSelect> Translate<TSelect>(string key) where TSelect : Enum
        => new(key);

    /// <summary>
    /// Creates a select + plural translation definition that varies by both an enum value and a quantity.
    /// Chain <c>.When()</c> to start a variant, then <c>.One()</c>, <c>.Other()</c>, etc. for plural forms.
    /// <code>
    /// Translate&lt;Gender&gt;(key: "Inbox", howMany: 0)
    ///     .When(Gender.Female)
    ///         .One("She has {MessageCount} message")
    ///         .Other("She has {MessageCount} messages")
    ///     .Otherwise()
    ///         .One("They have {MessageCount} message")
    ///         .Other("They have {MessageCount} messages")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation.</param>
    /// <param name="howMany">
    /// Not used at definition time — only distinguishes this overload from
    /// <see cref="Translate{TSelect}(string)"/>. Pass any value (e.g. <c>0</c>).
    /// The actual quantity is provided at the call site via <c>Loc.Translation(definition, select, howMany)</c>.
    /// </param>
    /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
    public static SelectPluralDefinitionBuilder<TSelect> Translate<TSelect>(string key, int howMany) where TSelect : Enum
        => new(key);
}
