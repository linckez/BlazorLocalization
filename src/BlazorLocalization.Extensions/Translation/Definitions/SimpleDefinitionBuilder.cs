using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable simple translation definition — key, source text, and optional
/// inline translations — without requiring an <see cref="IStringLocalizer"/> at definition time.
/// Created by <see cref="Translate.Simple"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translate(definition)</c>:
/// <code>
/// public static readonly SimpleDefinitionBuilder SaveButton =
///     Translate.Simple("Common.Save", "Save")
///         .For("da", "Gem");
///
/// // Usage:
/// Loc.Translate(CommonTranslations.SaveButton)
/// </code>
/// </remarks>
public sealed class SimpleDefinitionBuilder
{
    private Dictionary<string, string>? _inlineMessages;

    internal SimpleDefinitionBuilder(string key, string message)
    {
        Key = key;
        Message = message;
    }

    /// <summary>The translation key, e.g. <c>"Common.Save"</c>.</summary>
    internal string Key { get; }

    /// <summary>The source text, used as fallback when no translation is available.</summary>
    internal string Message { get; }

    /// <summary>Inline translations keyed by locale, from <see cref="For"/> calls.</summary>
    internal Dictionary<string, string>? InlineMessages => _inlineMessages;

    /// <summary>
    /// Adds a translation for the specified locale directly in the definition.
    /// <code>
    /// Translate.Simple("Common.Save", "Save")
    ///     .For("da", "Gem")
    ///     .For("de", "Speichern")
    /// </code>
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    /// <param name="message">The translated text for this locale.</param>
    public SimpleDefinitionBuilder For(string locale, string message)
    {
        _inlineMessages ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _inlineMessages[locale] = message;
        return this;
    }

    /// <summary>
    /// Resolves this definition against the given localizer and returns the translated string.
    /// Resolution chain: provider/cache → inline translation → source text.
    /// </summary>
    internal string Resolve(IStringLocalizer localizer, object? replaceWith)
    {
        var resolved = TranslationResolver.TryLocalizer(localizer, Key)
            ?? TranslationResolver.TryInline(_inlineMessages)
            ?? Message;

        return TranslationFormatter.Format(resolved, replaceWith);
    }
}
