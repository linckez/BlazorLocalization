using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation;

/// <summary>
/// Resolves a simple translation with optional inline translations for other locales.
/// Created by <see cref="StringLocalizerExtensions.Translation(IStringLocalizer, string, string, object?)"/>.
/// </summary>
public sealed class SimpleBuilder
{
    private readonly IStringLocalizer _localizer;
    private readonly string _key;
    private readonly string _sourceMessage;
    private readonly object? _replaceWith;
    private Dictionary<string, string>? _inlineMessages;

    internal SimpleBuilder(IStringLocalizer localizer, string key, string sourceMessage, object? replaceWith)
    {
        _localizer = localizer;
        _key = key;
        _sourceMessage = sourceMessage;
        _replaceWith = replaceWith;
    }

    /// <summary>Replay constructor: builds from a definition's pre-populated state.</summary>
    internal SimpleBuilder(IStringLocalizer localizer, string key, string sourceMessage, Dictionary<string, string>? inlineMessages, object? replaceWith)
        : this(localizer, key, sourceMessage, replaceWith)
    {
        if (inlineMessages is not null)
        {
            _inlineMessages ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (locale, msg) in inlineMessages)
                _inlineMessages[locale] = msg;
        }
    }

    /// <summary>
    /// Adds a translation for the specified locale directly in code.
    /// Used as fallback when your providers don't have this translation yet:
    /// <code>
    /// Loc.Translation(key: "Home.Title", sourceMessage: "Welcome!")
    ///     .For(locale: "da", message: "Velkommen!")
    ///     .For(locale: "es", message: "¡Bienvenido!")
    /// </code>
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    /// <param name="message">The translated text for this locale.</param>
    public SimpleBuilder For(string locale, string message)
    {
        _inlineMessages ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _inlineMessages[locale] = message;
        return this;
    }

    public static implicit operator string(SimpleBuilder builder) => builder.ToString();

    /// <inheritdoc/>
    public override string ToString()
    {
        var resolved = TranslationResolver.TryLocalizer(_localizer, _key)
            ?? TranslationResolver.TryInline(_inlineMessages)
            ?? _sourceMessage;

        return TranslationFormatter.Format(resolved, _replaceWith);
    }
}
