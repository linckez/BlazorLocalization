using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation;

/// <summary>
/// Resolves a plural translation — picks the right wording for the quantity.
/// Created by <see cref="StringLocalizerExtensions.Translation(IStringLocalizer, string, int, object?)"/>.
/// </summary>
public sealed class PluralBuilder
{
    private readonly IStringLocalizer _localizer;
    private readonly string _key;
    private readonly int _howMany;
    private readonly object? _replaceWith;
    private bool _useOrdinal;
    private string? _currentLocale;
    private readonly Dictionary<string, string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<string, string>> _inlineMessages = new();

    internal PluralBuilder(IStringLocalizer localizer, string key, int howMany, object? replaceWith)
    {
        _localizer = localizer;
        _key = key;
        _howMany = howMany;
        _replaceWith = replaceWith;
    }

    /// <summary>
    /// Switches to ordinal rules (1st, 2nd, 3rd) instead of cardinal (1, 2, 3):
    /// <code>
    /// Loc.Translation(key: "Race.Place", howMany: position, replaceWith: new { Position = position })
    ///     .Ordinal()
    ///     .One(message: "{Position}st place")
    ///     .Two(message: "{Position}nd place")
    ///     .Few(message: "{Position}rd place")
    ///     .Other(message: "{Position}th place")
    /// </code>
    /// </summary>
    public PluralBuilder Ordinal() { _useOrdinal = true; return this; }

    /// <summary>
    /// Message shown when <c>howMany</c> equals <paramref name="value"/> exactly.
    /// Checked before singular/plural rules:
    /// <code>
    /// Loc.Translation(key: "Cart.Items", howMany: 0)
    ///     .Exactly(value: 0, message: "Your cart is empty")
    ///     .One(message: "1 item in your cart")
    ///     .Other(message: "Several items in your cart")
    /// </code>
    /// </summary>
    /// <param name="value">The exact number to match against <c>howMany</c>.</param>
    /// <param name="message">The text shown when the exact match hits.</param>
    public PluralBuilder Exactly(int value, string message) { SetMessage(KeySuffix.ForExactly(value), message); return this; }

    /// <summary>Message shown when <c>howMany</c> is zero (only relevant for some languages, e.g. Arabic).</summary>
    /// <param name="message">The text for this form.</param>
    public PluralBuilder Zero(string message) { SetMessage(KeySuffix.Zero, message); return this; }

    /// <summary>Message shown when <c>howMany</c> matches the singular form (typically 1).</summary>
    /// <param name="message">The text for this form, e.g. <c>"1 item in your cart"</c>.</param>
    public PluralBuilder One(string message) { SetMessage(KeySuffix.One, message); return this; }

    /// <summary>Message shown when <c>howMany</c> is exactly two (dual form — Arabic, Welsh, etc.).</summary>
    /// <param name="message">The text for this form.</param>
    public PluralBuilder Two(string message) { SetMessage(KeySuffix.Two, message); return this; }

    /// <summary>Message shown for small quantities (e.g. 2–4 in Polish, Czech).</summary>
    /// <param name="message">The text for this form.</param>
    public PluralBuilder Few(string message) { SetMessage(KeySuffix.Few, message); return this; }

    /// <summary>Message shown for large quantities (e.g. 11–99 in Arabic, Maltese).</summary>
    /// <param name="message">The text for this form.</param>
    public PluralBuilder Many(string message) { SetMessage(KeySuffix.Many, message); return this; }

    /// <summary>
    /// The default plural form — the one form every language uses.
    /// If you define only one plural form, make it this one.
    /// </summary>
    /// <param name="message">The text for this form, e.g. <c>"{ItemCount} items in your cart"</c>.</param>
    public PluralBuilder Other(string message) { SetMessage(KeySuffix.Other, message); return this; }

    /// <summary>
    /// Starts defining translations for the specified locale.
    /// Subsequent <see cref="One"/>, <see cref="Other"/>, etc. apply to this locale
    /// instead of the original text:
    /// <code>
    /// Loc.Translation(key: "Cart.Items", howMany: itemCount)
    ///     .One(message: "1 item").Other(message: "Several items")
    ///     .For(locale: "da")
    ///     .One(message: "1 vare").Other(message: "Flere varer")
    /// </code>
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public PluralBuilder For(string locale) { _currentLocale = locale; return this; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var pluralSuffixes = GetSuffixes();

        // 1. Provider/cache — a real translation always wins, even for a less specific plural form
        foreach (var suffix in pluralSuffixes)
        {
            var providerTranslation = TranslationResolver.TryLocalizer(_localizer, _key + suffix);
            if (providerTranslation is not null)
                return TranslationFormatter.Format(providerTranslation, _replaceWith);
        }

        // 2. Inline translations for the current locale (from .For() chains)
        foreach (var suffix in pluralSuffixes)
        {
            var inlineTranslation = ResolveInline(suffix);
            if (inlineTranslation is not null)
                return TranslationFormatter.Format(inlineTranslation, _replaceWith);
        }

        // 3. Source text defined in code — last resort before falling back to the raw key
        foreach (var suffix in pluralSuffixes)
        {
            if (_sourceMessages.TryGetValue(suffix, out var sourceText))
                return TranslationFormatter.Format(sourceText, _replaceWith);
        }

        return _key;
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

    private string[] GetSuffixes()
    {
        var locale = CultureInfo.CurrentUICulture.Name;
        var category = PluralRules.GetCategory(locale, _howMany, _useOrdinal);
        return [KeySuffix.ForExactly(_howMany), KeySuffix.ForCategory(category), KeySuffix.Other];
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
