namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable simple translation definition — key, source text, and optional
/// inline translations — without requiring an <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/> at definition time.
/// Created by <see cref="TranslationDefinitions.DefineSimple"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translation(definition)</c>:
/// <code>
/// public static readonly SimpleDefinition SaveButton =
///     DefineSimple("Common.Save", "Save")
///         .For("da", "Gem");
///
/// // Usage:
/// Loc.Translation(CommonTranslations.SaveButton)
/// </code>
/// </remarks>
public sealed class SimpleDefinition
{
    private Dictionary<string, string>? _inlineMessages;

    internal SimpleDefinition(string key, string sourceMessage)
    {
        Key = key;
        SourceMessage = sourceMessage;
    }

    /// <summary>The translation key, e.g. <c>"Common.Save"</c>.</summary>
    internal string Key { get; }

    /// <summary>The source text, used as fallback when no translation is available.</summary>
    internal string SourceMessage { get; }

    /// <summary>Inline translations keyed by locale, from <see cref="For"/> calls.</summary>
    internal Dictionary<string, string>? InlineMessages => _inlineMessages;

    /// <summary>
    /// Adds a translation for the specified locale directly in the definition.
    /// <code>
    /// DefineSimple("Common.Save", "Save")
    ///     .For("da", "Gem")
    ///     .For("de", "Speichern")
    /// </code>
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    /// <param name="message">The translated text for this locale.</param>
    public SimpleDefinition For(string locale, string message)
    {
        _inlineMessages ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _inlineMessages[locale] = message;
        return this;
    }
}
