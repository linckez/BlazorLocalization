namespace BlazorLocalization.Extensions;

/// <summary>
/// Marks an enum member with a translatable display name.
/// Apply once without <see cref="Locale"/> for the source text,
/// and additional times with <see cref="Locale"/> for inline translations in other languages.
/// </summary>
/// <example>
/// <code>
/// public enum FlightStatus
/// {
///     [Translation("Delayed")]
///     [Translation("Forsinket", Locale = "da")]
///     Delayed,
///
///     [Translation("On time")]
///     OnTime
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class TranslationAttribute(string message) : Attribute
{
    /// <summary>
    /// The display text — either the source text (when <see cref="Locale"/> is <c>null</c>)
    /// or an inline translation for the specified locale.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// When set, this attribute provides an inline translation for the given culture code
    /// (e.g. <c>"da"</c>, <c>"es-MX"</c>). When <c>null</c>, <see cref="Message"/> is
    /// the source text.
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Overrides the auto-generated translation key (<c>Enum.{TypeName}_{MemberName}</c>).
    /// Used verbatim — no <c>Enum.</c> prefix is added.
    /// </summary>
    public string? Key { get; set; }
}
