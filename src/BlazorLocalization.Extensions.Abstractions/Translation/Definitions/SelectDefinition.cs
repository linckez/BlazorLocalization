namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable select translation definition — key and enum-based variants —
/// without requiring an <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/> at definition time.
/// Created by <see cref="TranslationDefinitions.DefineSelect{TSelect}"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translation(definition, select)</c>:
/// <code>
/// public static readonly SelectDefinition&lt;UserTitle&gt; Greeting =
///     DefineSelect&lt;UserTitle&gt;("Home.Greeting")
///         .When(UserTitle.Mr, "Dear Mr. Smith")
///         .When(UserTitle.Mrs, "Dear Mrs. Smith")
///         .Otherwise("Dear customer");
///
/// // Usage:
/// Loc.Translation(CommonTranslations.Greeting, selectedTitle)
/// </code>
/// </remarks>
/// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
public sealed class SelectDefinition<TSelect> where TSelect : Enum
{
    private string? _currentLocale;
    private readonly Dictionary<string, string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<string, string>> _inlineMessages = new();

    internal SelectDefinition(string key) => Key = key;

    /// <summary>The translation key, e.g. <c>"Home.Greeting"</c>.</summary>
    internal string Key { get; }

    /// <summary>Source text select cases keyed by enum value name or otherwise sentinel.</summary>
    internal Dictionary<string, string> SourceMessages => _sourceMessages;

    /// <summary>Per-locale select cases.</summary>
    internal Dictionary<string, Dictionary<string, string>> InlineMessages => _inlineMessages;

    /// <inheritdoc cref="SelectBuilder{TSelect}.When"/>
    public SelectDefinition<TSelect> When(TSelect select, string message)
    {
        SetMessage(select.ToString(), message);
        return this;
    }

    /// <inheritdoc cref="SelectBuilder{TSelect}.Otherwise"/>
    public SelectDefinition<TSelect> Otherwise(string message)
    {
        SetMessage("__otherwise__", message);
        return this;
    }

    /// <summary>
    /// Starts defining select variants for the specified locale.
    /// Subsequent <see cref="When"/> and <see cref="Otherwise"/> apply to this locale.
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public SelectDefinition<TSelect> For(string locale) { _currentLocale = locale; return this; }

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
}
