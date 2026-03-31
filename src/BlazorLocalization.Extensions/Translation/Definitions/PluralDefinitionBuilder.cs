using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable plural translation definition — key and plural forms —
/// without requiring an <see cref="IStringLocalizer"/> at definition time.
/// Created by <see cref="Translate.Plural"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translation(definition, howMany)</c>:
/// <code>
/// public static readonly PluralDefinitionBuilder CartItems =
///     Translate.Plural("Cart.Items")
///         .One("{ItemCount} item in your cart")
///         .Other("{ItemCount} items in your cart")
///         .For("da")
///         .One("{ItemCount} vare i din kurv")
///         .Other("{ItemCount} varer i din kurv");
///
/// // Usage:
/// Loc.Translation(CommonTranslations.CartItems, howMany: itemCount, replaceWith: new { ItemCount = itemCount })
/// </code>
/// </remarks>
public sealed class PluralDefinitionBuilder
{
    private string? _currentLocale;
    private readonly Dictionary<string, string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<string, string>> _inlineMessages = new();

    internal PluralDefinitionBuilder(string key) => Key = key;

    /// <summary>The translation key, e.g. <c>"Cart.Items"</c>.</summary>
    internal string Key { get; }

    /// <summary>Source text plural forms keyed by suffix.</summary>
    internal Dictionary<string, string> SourceMessages => _sourceMessages;

    /// <summary>Per-locale plural forms.</summary>
    internal Dictionary<string, Dictionary<string, string>> InlineMessages => _inlineMessages;

    /// <inheritdoc cref="PluralBuilder.Exactly"/>
    public PluralDefinitionBuilder Exactly(int value, string message) { SetMessage(KeySuffix.ForExactly(value), message); return this; }

    /// <inheritdoc cref="PluralBuilder.Zero"/>
    public PluralDefinitionBuilder Zero(string message) { SetMessage(KeySuffix.Zero, message); return this; }

    /// <inheritdoc cref="PluralBuilder.One"/>
    public PluralDefinitionBuilder One(string message) { SetMessage(KeySuffix.One, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Two"/>
    public PluralDefinitionBuilder Two(string message) { SetMessage(KeySuffix.Two, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Few"/>
    public PluralDefinitionBuilder Few(string message) { SetMessage(KeySuffix.Few, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Many"/>
    public PluralDefinitionBuilder Many(string message) { SetMessage(KeySuffix.Many, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Other"/>
    public PluralDefinitionBuilder Other(string message) { SetMessage(KeySuffix.Other, message); return this; }

    /// <summary>
    /// Starts defining plural forms for the specified locale.
    /// Subsequent <see cref="One"/>, <see cref="Other"/>, etc. apply to this locale.
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public PluralDefinitionBuilder For(string locale) { _currentLocale = locale; return this; }

    /// <summary>
    /// Resolves this definition against the given localizer and returns the translated string.
    /// Resolution chain: provider/cache → inline translation → source text → raw key.
    /// </summary>
    internal string Resolve(IStringLocalizer localizer, int howMany, bool ordinal, object? replaceWith)
    {
        var pluralSuffixes = GetSuffixes(howMany, ordinal);

        foreach (var suffix in pluralSuffixes)
        {
            var providerTranslation = TranslationResolver.TryLocalizer(localizer, Key + suffix);
            if (providerTranslation is not null)
                return TranslationFormatter.Format(providerTranslation, replaceWith);
        }

        foreach (var suffix in pluralSuffixes)
        {
            var inlineTranslation = ResolveInline(suffix);
            if (inlineTranslation is not null)
                return TranslationFormatter.Format(inlineTranslation, replaceWith);
        }

        foreach (var suffix in pluralSuffixes)
        {
            if (_sourceMessages.TryGetValue(suffix, out var sourceText))
                return TranslationFormatter.Format(sourceText, replaceWith);
        }

        return Key;
    }

    private void SetMessage(string suffix, string message)
    {
        if (_currentLocale is null)
            _sourceMessages[suffix] = message;
        else
        {
            if (!_inlineMessages.TryGetValue(_currentLocale, out var dict))
                _inlineMessages[_currentLocale] = dict = new Dictionary<string, string>();
            dict[suffix] = message;
        }
    }

    private static string[] GetSuffixes(int howMany, bool ordinal)
    {
        var locale = CultureInfo.CurrentUICulture.Name;
        var category = PluralRules.GetCategory(locale, howMany, ordinal);
        return [KeySuffix.ForExactly(howMany), KeySuffix.ForCategory(category), KeySuffix.Other];
    }

    private string? ResolveInline(string suffix)
    {
        Dictionary<string, string>? candidates = null;
        foreach (var (locale, dict) in _inlineMessages)
        {
            if (!dict.TryGetValue(suffix, out var msg)) continue;
            candidates ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            candidates[locale] = msg;
        }
        return TranslationResolver.TryInline(candidates);
    }
}
