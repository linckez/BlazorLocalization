namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Static factory for reusable translation definitions. Define translations once,
/// use everywhere via <c>Loc.Translation(definition)</c>.
/// </summary>
/// <remarks>
/// Add <c>using static BlazorLocalization.Extensions.Translation.Definitions.TranslationDefinitions;</c> to call
/// factory methods directly:
/// <code>
/// using static BlazorLocalization.Extensions.Translation.Definitions.TranslationDefinitions;
///
/// public static class CommonTranslations
/// {
///     // Simple — key + source text:
///     public static readonly SimpleDefinition SaveButton =
///         DefineSimple("Common.Save", "Save")
///             .For("da", "Gem");
///
///     // Plural — key only, forms from chain:
///     public static readonly PluralDefinition CartItems =
///         DefinePlural("Cart.Items")
///             .One("{ItemCount} item in your cart")
///             .Other("{ItemCount} items in your cart");
///
///     // Select — generic enum type:
///     public static readonly SelectDefinition&lt;UserTitle&gt; Greeting =
///         DefineSelect&lt;UserTitle&gt;("Home.Greeting")
///             .When(UserTitle.Mr, "Dear Mr. Smith")
///             .Otherwise("Dear customer");
/// }
/// </code>
/// </remarks>
public static class TranslationDefinitions
{
    /// <summary>
    /// Creates a simple translation definition with a key and source text.
    /// Chain <see cref="SimpleDefinition.For"/> to add inline translations.
    /// <code>
    /// DefineSimple(key: "Common.Save", message: "Save")
    ///     .For("da", "Gem")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Common.Save"</c>.</param>
    /// <param name="message">
    /// The original text. Used as fallback when your translation providers
    /// don't have a translation for the user's language.
    /// </param>
    public static SimpleDefinition DefineSimple(string key, string message)
        => new(key, message);

    /// <summary>
    /// Creates a plural translation definition with a key.
    /// Chain <c>.One()</c>, <c>.Other()</c>, etc. to define each plural form.
    /// <code>
    /// DefinePlural(key: "Cart.Items")
    ///     .One("{ItemCount} item in your cart")
    ///     .Other("{ItemCount} items in your cart")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Cart.Items"</c>.</param>
    public static PluralDefinition DefinePlural(string key)
        => new(key);

    /// <summary>
    /// Creates a select translation definition that varies by an enum value.
    /// Chain <c>.When()</c> for each case and <c>.Otherwise()</c> for the default.
    /// <code>
    /// DefineSelect&lt;UserTitle&gt;(key: "Home.Greeting")
    ///     .When(UserTitle.Mr, "Dear Mr. Smith")
    ///     .Otherwise("Dear customer")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation, e.g. <c>"Greeting"</c>.</param>
    /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
    public static SelectDefinition<TSelect> DefineSelect<TSelect>(string key) where TSelect : Enum
        => new(key);

    /// <summary>
    /// Creates a select + plural translation definition that varies by both an enum value and a quantity.
    /// Chain <c>.When()</c> to start a variant, then <c>.One()</c>, <c>.Other()</c>, etc. for plural forms.
    /// <code>
    /// DefineSelectPlural&lt;Gender&gt;(key: "Inbox")
    ///     .When(Gender.Female)
    ///         .One("She has {MessageCount} message")
    ///         .Other("She has {MessageCount} messages")
    ///     .Otherwise()
    ///         .One("They have {MessageCount} message")
    ///         .Other("They have {MessageCount} messages")
    /// </code>
    /// </summary>
    /// <param name="key">A unique identifier for this translation.</param>
    /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
    public static SelectPluralDefinition<TSelect> DefineSelectPlural<TSelect>(string key) where TSelect : Enum
        => new(key);
}
