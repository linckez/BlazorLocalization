namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// A place where a translation key IS used (call site).
/// Produced by code scanners (e.g. <c>localizer["key"]</c>, <c>localizer.GetString("key")</c>).
/// </summary>
public sealed record TranslationReference(
	string Key,
	bool IsLiteral,
	ReferenceSite Site);
