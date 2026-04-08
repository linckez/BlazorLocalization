namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// A place where translation source text IS defined.
/// Produced by scanners (Roslyn adapter from .Translation() calls, Resx adapter from .resx files).
/// </summary>
public sealed record TranslationDefinition(
	string Key,
	TranslationSourceText SourceText,
	DefinitionSite Site,
	IReadOnlyDictionary<string, TranslationSourceText>? InlineTranslations = null);
