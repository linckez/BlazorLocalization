using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation;

/// <summary>
/// Resolves a translation that varies by both an enum value and a quantity.
/// Created by <see cref="StringLocalizerExtensions.Translation{TSelect}(IStringLocalizer, string, TSelect, int, object?)"/>.
/// </summary>
/// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
public sealed class SelectPluralBuilder<TSelect> where TSelect : Enum
{
    private readonly IStringLocalizer _localizer;
    private readonly string _key;
    private readonly TSelect _selectValue;
    private readonly int _howMany;
    private readonly object? _replaceWith;
    private bool _useOrdinal;
    private string? _currentLocale;
    private string? _currentSelectCase;

    private readonly Dictionary<(string? selectCase, string pluralSuffix), string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<(string? selectCase, string pluralSuffix), string>> _inlineMessages = new();

    internal SelectPluralBuilder(IStringLocalizer localizer, string key, TSelect selectValue, int howMany, object? replaceWith)
    {
        _localizer = localizer;
        _key = key;
        _selectValue = selectValue;
        _howMany = howMany;
        _replaceWith = replaceWith;
    }

    /// <summary>Switches to ordinal rules (1st, 2nd, 3rd) instead of cardinal (1, 2, 3).</summary>
    public SelectPluralBuilder<TSelect> Ordinal() { _useOrdinal = true; return this; }

    /// <summary>Opens a variant for the specified enum value. Chain <c>.One()</c>, <c>.Other()</c>, etc. after this.</summary>
    /// <param name="select">The enum value this variant applies to.</param>
    public SelectPluralBuilder<TSelect> When(TSelect select) { _currentSelectCase = select.ToString(); return this; }

    /// <summary>Opens the default variant when no <see cref="When"/> case matches. Chain plural forms after this.</summary>
    public SelectPluralBuilder<TSelect> Otherwise() { _currentSelectCase = null; return this; }

    /// <summary>
    /// Message shown when <c>howMany</c> equals <paramref name="value"/> exactly, within the current variant.
    /// Checked before singular/plural rules.
    /// </summary>
    /// <param name="value">The exact number to match against <c>howMany</c>.</param>
    /// <param name="message">The text shown when the exact match hits.</param>
    public SelectPluralBuilder<TSelect> Exactly(int value, string message) { SetMessage(KeySuffix.ForExactly(value), message); return this; }

    /// <summary>Message for zero, within the current variant (only relevant for some languages).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralBuilder<TSelect> Zero(string message) { SetMessage(KeySuffix.Zero, message); return this; }

    /// <summary>Message shown when <c>howMany</c> matches the singular form, within the current variant.</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralBuilder<TSelect> One(string message) { SetMessage(KeySuffix.One, message); return this; }

    /// <summary>Message for exactly two, within the current variant (Arabic, Welsh, etc.).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralBuilder<TSelect> Two(string message) { SetMessage(KeySuffix.Two, message); return this; }

    /// <summary>Message for small quantities, within the current variant (e.g. 2–4 in Polish).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralBuilder<TSelect> Few(string message) { SetMessage(KeySuffix.Few, message); return this; }

    /// <summary>Message for large quantities, within the current variant (e.g. 11–99 in Arabic).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralBuilder<TSelect> Many(string message) { SetMessage(KeySuffix.Many, message); return this; }

    /// <summary>The default plural form within the current variant.</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralBuilder<TSelect> Other(string message) { SetMessage(KeySuffix.Other, message); return this; }

    /// <summary>
    /// Starts defining translations for the specified locale.
    /// Subsequent <see cref="When"/>, <see cref="Otherwise"/>, and plural forms
    /// apply to this locale instead of the original text.
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public SelectPluralBuilder<TSelect> For(string locale) { _currentLocale = locale; return this; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var selectPluralCombos = GetCombos();

        // 1. Provider/cache — a real translation always wins, even for a less specific combo
        foreach (var (_, _, providerKey) in selectPluralCombos)
        {
            var providerTranslation = TranslationResolver.TryLocalizer(_localizer, providerKey);
            if (providerTranslation is not null)
                return TranslationFormatter.Format(providerTranslation, _replaceWith);
        }

        // 2. Inline translations for the current locale (from .For() chains)
        foreach (var (selectCase, pluralSuffix, _) in selectPluralCombos)
        {
            var inlineTranslation = ResolveInline(selectCase, pluralSuffix);
            if (inlineTranslation is not null)
                return TranslationFormatter.Format(inlineTranslation, _replaceWith);
        }

        // 3. Source text defined in code — last resort before falling back to the raw key
        foreach (var (selectCase, pluralSuffix, _) in selectPluralCombos)
        {
            if (_sourceMessages.TryGetValue((selectCase, pluralSuffix), out var sourceText))
                return TranslationFormatter.Format(sourceText, _replaceWith);
        }

        return _key;
    }

    private void SetMessage(string pluralSuffix, string message)
    {
        if (_currentLocale is null)
            _sourceMessages[(_currentSelectCase, pluralSuffix)] = message;
        else
        {
            if (!_inlineMessages.TryGetValue(_currentLocale, out var dict))
                _inlineMessages[_currentLocale] = dict = new Dictionary<(string? selectCase, string pluralSuffix), string>();
            dict[(_currentSelectCase, pluralSuffix)] = message;
        }
    }

    private List<(string? selectCase, string pluralSuffix, string suffixedKey)> GetCombos()
    {
        var locale = CultureInfo.CurrentUICulture.Name;
        var selectStr = _selectValue.ToString();
        var category = PluralRules.GetCategory(locale, _howMany, _useOrdinal);
        var pluralSuffixes = new[] { KeySuffix.ForExactly(_howMany), KeySuffix.ForCategory(category), KeySuffix.Other };
        var selectCases = new[] { selectStr, null };

        return (from sc in selectCases from ps in pluralSuffixes select (sc, ps, sc is not null ? _key + KeySuffix.ForSelect(sc) + ps : _key + ps)).ToList();
    }

    private string? ResolveInline(string? selectCase, string pluralSuffix)
    {
        Dictionary<string, string>? candidates = null;
        foreach (var (locale, dict) in _inlineMessages)
        {
            if (!dict.TryGetValue((selectCase, pluralSuffix), out var msg)) continue;
            candidates ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            candidates[locale] = msg;
        }
        return TranslationResolver.TryInline(candidates);
    }
}
