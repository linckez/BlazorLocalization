namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// The source-language text for a translation key.
/// Discriminated union: <see cref="SingularText"/>, <see cref="PluralText"/>,
/// <see cref="SelectText"/>, or <see cref="SelectPluralText"/>.
/// </summary>
public abstract record TranslationSourceText;

/// <summary>
/// A non-pluralized source string, e.g. <c>"Welcome to Blazor"</c>.
/// </summary>
public sealed record SingularText(string Value) : TranslationSourceText;

/// <summary>
/// A pluralized source string with CLDR categories, optional exact matches, and ordinal support.
/// <see cref="Other"/> is always required (CLDR fallback). All other categories are optional.
/// </summary>
public sealed record PluralText(
	string Other,
	string? Zero = null,
	string? One = null,
	string? Two = null,
	string? Few = null,
	string? Many = null,
	IReadOnlyDictionary<int, string>? ExactMatches = null,
	bool IsOrdinal = false) : TranslationSourceText;

/// <summary>
/// A select translation that branches on a categorical value (gender, role, etc.).
/// Each case is a literal enum member name mapped to a source string.
/// </summary>
public sealed record SelectText(
	IReadOnlyDictionary<string, string> Cases,
	string? Otherwise = null) : TranslationSourceText;

/// <summary>
/// A composite select+plural translation. Each select case maps to a <see cref="PluralText"/>.
/// </summary>
public sealed record SelectPluralText(
	IReadOnlyDictionary<string, PluralText> Cases,
	PluralText? Otherwise = null) : TranslationSourceText;
