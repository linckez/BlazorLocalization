namespace BlazorLocalization.Extensions.Translation.Definitions;

/// <summary>
/// Captures a reusable plural translation definition — key and plural forms —
/// without requiring an <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/> at definition time.
/// Created by <see cref="TranslationDefinitions.DefinePlural"/>.
/// </summary>
/// <remarks>
/// Define once as a static field, use everywhere via <c>Loc.Translation(definition, howMany)</c>:
/// <code>
/// public static readonly PluralDefinition CartItems =
///     DefinePlural("Cart.Items")
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
public sealed class PluralDefinition
{
    private string? _currentLocale;
    private readonly Dictionary<string, string> _sourceMessages = new();
    private readonly Dictionary<string, Dictionary<string, string>> _inlineMessages = new();

    internal PluralDefinition(string key) => Key = key;

    /// <summary>The translation key, e.g. <c>"Cart.Items"</c>.</summary>
    internal string Key { get; }

    /// <summary>Source text plural forms keyed by suffix.</summary>
    internal Dictionary<string, string> SourceMessages => _sourceMessages;

    /// <summary>Per-locale plural forms.</summary>
    internal Dictionary<string, Dictionary<string, string>> InlineMessages => _inlineMessages;

    /// <inheritdoc cref="PluralBuilder.Exactly"/>
    public PluralDefinition Exactly(int value, string message) { SetMessage(KeySuffix.ForExactly(value), message); return this; }

    /// <inheritdoc cref="PluralBuilder.Zero"/>
    public PluralDefinition Zero(string message) { SetMessage(KeySuffix.Zero, message); return this; }

    /// <inheritdoc cref="PluralBuilder.One"/>
    public PluralDefinition One(string message) { SetMessage(KeySuffix.One, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Two"/>
    public PluralDefinition Two(string message) { SetMessage(KeySuffix.Two, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Few"/>
    public PluralDefinition Few(string message) { SetMessage(KeySuffix.Few, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Many"/>
    public PluralDefinition Many(string message) { SetMessage(KeySuffix.Many, message); return this; }

    /// <inheritdoc cref="PluralBuilder.Other"/>
    public PluralDefinition Other(string message) { SetMessage(KeySuffix.Other, message); return this; }

    /// <summary>
    /// Starts defining plural forms for the specified locale.
    /// Subsequent <see cref="One"/>, <see cref="Other"/>, etc. apply to this locale.
    /// </summary>
    /// <param name="locale">A culture code, e.g. <c>"da"</c>, <c>"es-MX"</c>.</param>
    public PluralDefinition For(string locale) { _currentLocale = locale; return this; }

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
}
