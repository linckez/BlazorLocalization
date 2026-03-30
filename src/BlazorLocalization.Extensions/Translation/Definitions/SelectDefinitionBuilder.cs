using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable select translation definition — key and enum-based variants —
/// without requiring an <see cref="IStringLocalizer"/> at definition time.
/// Created by <see cref="Translate.Select{TSelect}"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translate(definition, select)</c>:
/// <code>
/// public static readonly SelectDefinitionBuilder&lt;UserTitle&gt; Greeting =
///     Translate.Select&lt;UserTitle&gt;("Home.Greeting")
///         .When(UserTitle.Mr, "Dear Mr. Smith")
///         .When(UserTitle.Mrs, "Dear Mrs. Smith")
///         .Otherwise("Dear customer");
///
/// // Usage:
/// Loc.Translate(CommonTranslations.Greeting, selectedTitle)
/// </code>
/// </remarks>
/// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
public sealed class SelectDefinitionBuilder<TSelect> where TSelect : Enum
{
    private const string OtherwiseSentinel = "__otherwise__";

    private string? _currentLocale;
    private readonly Dictionary<string, string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<string, string>> _inlineMessages = new();

    internal SelectDefinitionBuilder(string key) => Key = key;

    /// <summary>The translation key, e.g. <c>"Home.Greeting"</c>.</summary>
    internal string Key { get; }

    /// <summary>Source text select cases keyed by enum value name or otherwise sentinel.</summary>
    internal Dictionary<string, string> SourceMessages => _sourceMessages;

    /// <summary>Per-locale select cases.</summary>
    internal Dictionary<string, Dictionary<string, string>> InlineMessages => _inlineMessages;

    /// <inheritdoc cref="SelectBuilder{TSelect}.When"/>
    public SelectDefinitionBuilder<TSelect> When(TSelect select, string message)
    {
        SetMessage(select.ToString(), message);
        return this;
    }

    /// <inheritdoc cref="SelectBuilder{TSelect}.Otherwise"/>
    public SelectDefinitionBuilder<TSelect> Otherwise(string message)
    {
        SetMessage(OtherwiseSentinel, message);
        return this;
    }

    /// <summary>
    /// Starts defining select variants for the specified locale.
    /// Subsequent <see cref="When"/> and <see cref="Otherwise"/> apply to this locale.
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public SelectDefinitionBuilder<TSelect> For(string locale) { _currentLocale = locale; return this; }

    /// <summary>
    /// Resolves this definition against the given localizer and returns the translated string.
    /// Resolution chain: provider/cache → inline translation → source text → raw key.
    /// </summary>
    internal string Resolve(IStringLocalizer localizer, TSelect selectValue, object? replaceWith)
    {
        var selectedValue = selectValue.ToString();
        var selectCases = new[]
        {
            (keySuffix: KeySuffix.ForSelect(selectedValue), messageKey: selectedValue),
            (keySuffix: "", messageKey: OtherwiseSentinel)
        };

        foreach (var (keySuffix, _) in selectCases)
        {
            var providerTranslation = TranslationResolver.TryLocalizer(localizer, Key + keySuffix);
            if (providerTranslation is not null)
                return TranslationFormatter.Format(providerTranslation, replaceWith);
        }

        foreach (var (_, messageKey) in selectCases)
        {
            var inlineTranslation = ResolveInline(messageKey);
            if (inlineTranslation is not null)
                return TranslationFormatter.Format(inlineTranslation, replaceWith);
        }

        foreach (var (_, messageKey) in selectCases)
        {
            if (_sourceMessages.TryGetValue(messageKey, out var sourceText))
                return TranslationFormatter.Format(sourceText, replaceWith);
        }

        return Key;
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
