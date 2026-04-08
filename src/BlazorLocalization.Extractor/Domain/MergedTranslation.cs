namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Per-key truth after merging all scanner outputs.
/// One <see cref="MergedTranslation"/> per unique key, carrying every definition site,
/// every reference site, and the resolved source text (if any definition provided one).
/// </summary>
public sealed record MergedTranslation(
	string Key,
	TranslationSourceText? SourceText,
	IReadOnlyList<DefinitionSite> Definitions,
	IReadOnlyList<ReferenceSite> References,
	IReadOnlyDictionary<string, TranslationSourceText>? InlineTranslations = null,
	bool IsKeyLiteral = true)
{
	/// <summary>
	/// Computes the cross-reference status from structural presence/absence.
	/// No origin labels needed — the shape tells the story.
	/// </summary>
	public TranslationStatus Status
	{
		get
		{
			if (!IsKeyLiteral)
				return TranslationStatus.Review;

			var hasDef = Definitions.Count > 0;
			var hasRef = References.Count > 0
			             || Definitions.Any(d => d.Kind == DefinitionKind.InlineTranslation);

			return (hasDef, hasRef) switch
			{
				(true, true) => TranslationStatus.Resolved,
				(true, false) => TranslationStatus.Review,
				(false, true) => TranslationStatus.Missing,
				(false, false) => TranslationStatus.Review // shouldn't happen, but defensive
			};
		}
	}
}

/// <summary>
/// Result of merging all scanner outputs: deduplicated entries, conflicts, and invalid entries.
/// </summary>
public sealed record MergeResult(
	IReadOnlyList<MergedTranslation> Entries,
	IReadOnlyList<KeyConflict> Conflicts,
	IReadOnlyList<InvalidEntry> InvalidEntries,
	IReadOnlyList<Ports.ScanDiagnostic> Diagnostics);

/// <summary>
/// A key with two or more definitions that disagree on source text.
/// </summary>
public sealed record KeyConflict(
	string Key,
	IReadOnlyList<ConflictingValue> Values);

/// <summary>
/// One distinct source text for a conflicting key, with all definition sites that use it.
/// </summary>
public sealed record ConflictingValue(
	TranslationSourceText SourceText,
	IReadOnlyList<DefinitionSite> Sites);

/// <summary>
/// An entry rejected during merge (e.g. empty key).
/// </summary>
public sealed record InvalidEntry(
	string Key,
	string Reason,
	IReadOnlyList<DefinitionSite> Sites);
