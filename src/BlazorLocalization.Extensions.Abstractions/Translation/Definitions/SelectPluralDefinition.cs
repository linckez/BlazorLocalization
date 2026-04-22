namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable select + plural translation definition — key, enum-based variants,
/// and plural forms per variant — without requiring an <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/> at definition time.
/// Created by <see cref="TranslationDefinitions.DefineSelectPlural{TSelect}"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translation(definition, select, howMany)</c>:
/// <code>
/// public static readonly SelectPluralDefinition&lt;Gender&gt; InboxMessage =
///     DefineSelectPlural&lt;Gender&gt;("Inbox")
///         .When(Gender.Female).One("She has {MessageCount} message").Other("She has {MessageCount} messages")
///         .Otherwise().One("They have {MessageCount} message").Other("They have {MessageCount} messages");
///
/// // Usage:
/// Loc.Translation(CommonTranslations.InboxMessage, user.Gender, messageCount, replaceWith: new { MessageCount = messageCount })
/// </code>
/// </remarks>
/// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
public sealed class SelectPluralDefinition<TSelect> where TSelect : Enum
{
    private string? _currentLocale;
    private string? _currentSelectCase;
    private readonly Dictionary<(string? selectCase, string pluralSuffix), string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<(string? selectCase, string pluralSuffix), string>> _inlineMessages = new();

    internal SelectPluralDefinition(string key) => Key = key;

    /// <summary>The translation key.</summary>
    internal string Key { get; }

    /// <summary>Source text messages keyed by (selectCase, pluralSuffix).</summary>
    internal Dictionary<(string? selectCase, string pluralSuffix), string> SourceMessages => _sourceMessages;

    /// <summary>Per-locale messages.</summary>
    internal Dictionary<string, Dictionary<(string? selectCase, string pluralSuffix), string>> InlineMessages => _inlineMessages;

    /// <summary>Opens a variant for the specified enum value. Chain <c>.One()</c>, <c>.Other()</c>, etc. after this.</summary>
    /// <param name="select">The enum value this variant applies to.</param>
    public SelectPluralDefinition<TSelect> When(TSelect select) { _currentSelectCase = select.ToString(); return this; }

    /// <summary>Opens the default variant when no <see cref="When"/> case matches. Chain plural forms after this.</summary>
    public SelectPluralDefinition<TSelect> Otherwise() { _currentSelectCase = null; return this; }

    /// <summary>Message shown when <c>howMany</c> equals <paramref name="value"/> exactly, within the current variant. Checked before singular/plural rules.</summary>
    /// <param name="value">The exact number to match against <c>howMany</c>.</param>
    /// <param name="message">The text shown when the exact match hits.</param>
    public SelectPluralDefinition<TSelect> Exactly(int value, string message) { SetMessage(KeySuffix.ForExactly(value), message); return this; }

    /// <summary>Message for zero, within the current variant (only relevant for some languages).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralDefinition<TSelect> Zero(string message) { SetMessage(KeySuffix.Zero, message); return this; }

    /// <summary>Message shown when <c>howMany</c> matches the singular form, within the current variant.</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralDefinition<TSelect> One(string message) { SetMessage(KeySuffix.One, message); return this; }

    /// <summary>Message for exactly two, within the current variant (Arabic, Welsh, etc.).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralDefinition<TSelect> Two(string message) { SetMessage(KeySuffix.Two, message); return this; }

    /// <summary>Message for small quantities, within the current variant (e.g. 2–4 in Polish).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralDefinition<TSelect> Few(string message) { SetMessage(KeySuffix.Few, message); return this; }

    /// <summary>Message for large quantities, within the current variant (e.g. 11–99 in Arabic).</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralDefinition<TSelect> Many(string message) { SetMessage(KeySuffix.Many, message); return this; }

    /// <summary>The default plural form within the current variant.</summary>
    /// <param name="message">The text for this form.</param>
    public SelectPluralDefinition<TSelect> Other(string message) { SetMessage(KeySuffix.Other, message); return this; }

    /// <summary>
    /// Starts defining variants for the specified locale.
    /// Subsequent <see cref="When"/>, <see cref="Otherwise"/>, and plural forms apply to this locale.
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public SelectPluralDefinition<TSelect> For(string locale) { _currentLocale = locale; return this; }

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
}
