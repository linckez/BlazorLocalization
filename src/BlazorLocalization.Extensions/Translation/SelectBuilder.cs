using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation;

/// <summary>
/// Resolves a select translation — picks a message variant based on an enum value.
/// Created by <see cref="StringLocalizerExtensions.Translation{TSelect}(IStringLocalizer, string, TSelect, object?)"/>.
/// </summary>
/// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
public sealed class SelectBuilder<TSelect> where TSelect : Enum
{
    private const string OtherwiseSentinel = "__otherwise__";

    private readonly IStringLocalizer _localizer;
    private readonly string _key;
    private readonly TSelect _selectValue;
    private readonly object? _replaceWith;
    private string? _currentLocale;
    private readonly Dictionary<string, string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<string, string>> _inlineMessages = new();

    internal SelectBuilder(IStringLocalizer localizer, string key, TSelect selectValue, object? replaceWith)
    {
        _localizer = localizer;
        _key = key;
        _selectValue = selectValue;
        _replaceWith = replaceWith;
    }

    /// <summary>Defines the message for a specific enum value.</summary>
    /// <param name="select">The enum value this message applies to.</param>
    /// <param name="message">The text shown when <paramref name="select"/> matches.</param>
    public SelectBuilder<TSelect> When(TSelect select, string message)
    {
        SetMessage(select.ToString(), message);
        return this;
    }

    /// <summary>
    /// The default message when no <see cref="When"/> case matches.
    /// Recommended as a safety net — if you add new enum members later,
    /// this ensures a message always shows.
    /// </summary>
    /// <param name="message">The fallback text.</param>
    public SelectBuilder<TSelect> Otherwise(string message)
    {
        SetMessage(OtherwiseSentinel, message);
        return this;
    }

    /// <summary>
    /// Starts defining translations for the specified locale.
    /// Subsequent <see cref="When"/> and <see cref="Otherwise"/> apply to this locale
    /// instead of the original text:
    /// <code>
    /// Loc.Translation(key: "Greeting", select: Gender.Female)
    ///     .When(select: Gender.Female, message: "Welcome, ma'am!")
    ///     .Otherwise(message: "Welcome!")
    ///     .For(locale: "da")
    ///     .When(select: Gender.Female, message: "Velkommen, frue!")
    ///     .Otherwise(message: "Velkommen!")
    /// </code>
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public SelectBuilder<TSelect> For(string locale) { _currentLocale = locale; return this; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var selectedValue = _selectValue.ToString();
        var selectCases = new[]
        {
            (keySuffix: KeySuffix.ForSelect(selectedValue), messageKey: selectedValue),
            (keySuffix: "", messageKey: OtherwiseSentinel)
        };

        // 1. Provider/cache — a real translation always wins, even for the otherwise case
        foreach (var (keySuffix, _) in selectCases)
        {
            var providerTranslation = TranslationResolver.TryLocalizer(_localizer, _key + keySuffix);
            if (providerTranslation is not null)
                return TranslationFormatter.Format(providerTranslation, _replaceWith);
        }

        // 2. Inline translations for the current locale (from .For() chains)
        foreach (var (_, messageKey) in selectCases)
        {
            var inlineTranslation = ResolveInline(messageKey);
            if (inlineTranslation is not null)
                return TranslationFormatter.Format(inlineTranslation, _replaceWith);
        }

        // 3. Source text defined in code — last resort before falling back to the raw key
        foreach (var (_, messageKey) in selectCases)
        {
            if (_sourceMessages.TryGetValue(messageKey, out var sourceText))
                return TranslationFormatter.Format(sourceText, _replaceWith);
        }

        return _key;
    }

    private void SetMessage(string selectKey, string message)
    {
        if (_currentLocale is null)
            _sourceMessages[selectKey] = message;
        else
        {
            if (!_inlineMessages.TryGetValue(_currentLocale, out var dict))
                _inlineMessages[_currentLocale] = dict = new Dictionary<string, string>();
            dict[selectKey] = message;
        }
    }

    private string? ResolveInline(string dictKey)
    {
        Dictionary<string, string>? candidates = null;
        foreach (var (locale, dict) in _inlineMessages)
        {
            if (!dict.TryGetValue(dictKey, out var msg)) continue;
            candidates ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            candidates[locale] = msg;
        }
        return TranslationResolver.TryInline(candidates);
    }
}
