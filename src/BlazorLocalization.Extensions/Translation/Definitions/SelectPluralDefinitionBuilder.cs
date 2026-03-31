using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable select + plural translation definition — key, enum-based variants,
/// and plural forms per variant — without requiring an <see cref="IStringLocalizer"/> at definition time.
/// Created by <see cref="Translate.SelectPlural{TSelect}"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translation(definition, select, howMany)</c>:
/// <code>
/// public static readonly SelectPluralDefinitionBuilder&lt;Gender&gt; InboxMessage =
///     Translate.SelectPlural&lt;Gender&gt;("Inbox")
///         .When(Gender.Female).One("She has {MessageCount} message").Other("She has {MessageCount} messages")
///         .Otherwise().One("They have {MessageCount} message").Other("They have {MessageCount} messages");
///
/// // Usage:
/// Loc.Translation(CommonTranslations.InboxMessage, user.Gender, messageCount, replaceWith: new { MessageCount = messageCount })
/// </code>
/// </remarks>
/// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
public sealed class SelectPluralDefinitionBuilder<TSelect> where TSelect : Enum
{
    private string? _currentLocale;
    private string? _currentSelectCase;
    private readonly Dictionary<(string? selectCase, string pluralSuffix), string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<(string? selectCase, string pluralSuffix), string>> _inlineMessages = new();

    internal SelectPluralDefinitionBuilder(string key) => Key = key;

    /// <summary>The translation key.</summary>
    internal string Key { get; }

    /// <summary>Source text messages keyed by (selectCase, pluralSuffix).</summary>
    internal Dictionary<(string? selectCase, string pluralSuffix), string> SourceMessages => _sourceMessages;

    /// <summary>Per-locale messages.</summary>
    internal Dictionary<string, Dictionary<(string? selectCase, string pluralSuffix), string>> InlineMessages => _inlineMessages;

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.When"/>
    public SelectPluralDefinitionBuilder<TSelect> When(TSelect select) { _currentSelectCase = select.ToString(); return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.Otherwise()"/>
    public SelectPluralDefinitionBuilder<TSelect> Otherwise() { _currentSelectCase = null; return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.Exactly"/>
    public SelectPluralDefinitionBuilder<TSelect> Exactly(int value, string message) { SetMessage(KeySuffix.ForExactly(value), message); return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.Zero"/>
    public SelectPluralDefinitionBuilder<TSelect> Zero(string message) { SetMessage(KeySuffix.Zero, message); return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.One"/>
    public SelectPluralDefinitionBuilder<TSelect> One(string message) { SetMessage(KeySuffix.One, message); return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.Two"/>
    public SelectPluralDefinitionBuilder<TSelect> Two(string message) { SetMessage(KeySuffix.Two, message); return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.Few"/>
    public SelectPluralDefinitionBuilder<TSelect> Few(string message) { SetMessage(KeySuffix.Few, message); return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.Many"/>
    public SelectPluralDefinitionBuilder<TSelect> Many(string message) { SetMessage(KeySuffix.Many, message); return this; }

    /// <inheritdoc cref="SelectPluralBuilder{TSelect}.Other"/>
    public SelectPluralDefinitionBuilder<TSelect> Other(string message) { SetMessage(KeySuffix.Other, message); return this; }

    /// <summary>
    /// Starts defining variants for the specified locale.
    /// Subsequent <see cref="When"/>, <see cref="Otherwise"/>, and plural forms apply to this locale.
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public SelectPluralDefinitionBuilder<TSelect> For(string locale) { _currentLocale = locale; return this; }

    /// <summary>
    /// Resolves this definition against the given localizer and returns the translated string.
    /// Resolution chain: provider/cache → inline translation → source text → raw key.
    /// </summary>
    internal string Resolve(IStringLocalizer localizer, TSelect selectValue, int howMany, bool ordinal, object? replaceWith)
    {
        var selectPluralCombos = GetCombos(selectValue, howMany, ordinal);

        foreach (var (_, _, providerKey) in selectPluralCombos)
        {
            var providerTranslation = TranslationResolver.TryLocalizer(localizer, providerKey);
            if (providerTranslation is not null)
                return TranslationFormatter.Format(providerTranslation, replaceWith);
        }

        foreach (var (selectCase, pluralSuffix, _) in selectPluralCombos)
        {
            var inlineTranslation = ResolveInline(selectCase, pluralSuffix);
            if (inlineTranslation is not null)
                return TranslationFormatter.Format(inlineTranslation, replaceWith);
        }

        foreach (var (selectCase, pluralSuffix, _) in selectPluralCombos)
        {
            if (_sourceMessages.TryGetValue((selectCase, pluralSuffix), out var sourceText))
                return TranslationFormatter.Format(sourceText, replaceWith);
        }

        return Key;
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

    private List<(string? selectCase, string pluralSuffix, string suffixedKey)> GetCombos(
        TSelect selectValue, int howMany, bool ordinal)
    {
        var locale = CultureInfo.CurrentUICulture.Name;
        var selectStr = selectValue.ToString();
        var category = PluralRules.GetCategory(locale, howMany, ordinal);
        var pluralSuffixes = new[] { KeySuffix.ForExactly(howMany), KeySuffix.ForCategory(category), KeySuffix.Other };
        var selectCases = new[] { selectStr, null };

        return (from sc in selectCases
                from ps in pluralSuffixes
                select (sc, ps, sc is not null ? Key + KeySuffix.ForSelect(sc) + ps : Key + ps)).ToList();
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
