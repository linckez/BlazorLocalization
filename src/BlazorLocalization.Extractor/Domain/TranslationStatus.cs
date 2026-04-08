namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Health status of a translation key after cross-referencing definitions and references.
/// </summary>
public enum TranslationStatus
{
	/// <summary>Source text found and code reference confirmed.</summary>
	Resolved,

	/// <summary>Needs manual review (definition-only, or dynamic key that can't be verified).</summary>
	Review,

	/// <summary>Code references this key but no source text was found anywhere.</summary>
	Missing
}
