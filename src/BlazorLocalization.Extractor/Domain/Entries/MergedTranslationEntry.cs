namespace BlazorLocalization.Extractor.Domain.Entries;

/// <summary>
/// A translation entry after per-project deduplication. One entry per unique key,
/// with all source locations merged and conflicting definitions detected.
/// </summary>
public sealed record MergedTranslationEntry(
	string Key,
	TranslationSourceText? SourceText,
	IReadOnlyList<SourceReference> Sources,
	IReadOnlyDictionary<string, TranslationSourceText>? InlineTranslations = null)
{
	/// <summary>
	/// Returns a copy with all <see cref="Sources"/> file paths made relative to <paramref name="projectDir"/>.
	/// </summary>
	public MergedTranslationEntry RelativizeSources(string projectDir) =>
		this with { Sources = Sources.Select(s => s.Relativize(projectDir)).ToList() };

	/// <summary>
	/// Merges raw <see cref="TranslationEntry"/> records into deduplicated entries.
	/// Definitions (<see cref="TranslationSourceText"/> non-null) must agree on source text for the same key.
	/// References (null source text) yield to definitions.
	/// </summary>
	public static MergeResult FromRaw(IReadOnlyList<TranslationEntry> entries)
	{
		var merged = new Dictionary<string, MergedBuilder>();

		foreach (var entry in entries)
		{
			if (!merged.TryGetValue(entry.Key, out var existing))
			{
				merged[entry.Key] = new MergedBuilder(entry.SourceText, entry.Source, entry.InlineTranslations);
				continue;
			}

			existing.AddSource(entry.SourceText, entry.Source, entry.InlineTranslations);
		}

		var result = merged.Select(kvp =>
			new MergedTranslationEntry(kvp.Key, kvp.Value.SourceText, kvp.Value.AllSources,
				kvp.Value.MergedInlineTranslations)).ToList();

		var conflicts = merged
			.Where(kvp => kvp.Value.HasConflict)
			.Select(kvp => new KeyConflict(kvp.Key, kvp.Value.ConflictingValues))
			.ToList();

		return new MergeResult(result, conflicts);
	}

	private sealed class MergedBuilder
	{
		/// <summary>First-seen definition source text (used for the merged entry).</summary>
		public TranslationSourceText? SourceText { get; private set; }

		public List<SourceReference> AllSources { get; } = [];

		/// <summary>All distinct source texts seen for this key, with their locations.</summary>
		private Dictionary<TranslationSourceText, List<SourceReference>> ValueMap { get; } = new();

		/// <summary>First-seen-per-locale inline translations.</summary>
		private Dictionary<string, TranslationSourceText>? InlineMap { get; set; }

		public IReadOnlyDictionary<string, TranslationSourceText>? MergedInlineTranslations =>
			InlineMap is { Count: > 0 } ? InlineMap : null;

		public MergedBuilder(
			TranslationSourceText? sourceText,
			SourceReference source,
			IReadOnlyDictionary<string, TranslationSourceText>? inlineTranslations)
		{
			SourceText = sourceText;
			AllSources.Add(source);
			if (sourceText is not null)
				ValueMap[sourceText] = [source];
			MergeInlineTranslations(inlineTranslations);
		}

		public void AddSource(
			TranslationSourceText? sourceText,
			SourceReference source,
			IReadOnlyDictionary<string, TranslationSourceText>? inlineTranslations)
		{
			AllSources.Add(source);

			if (sourceText is not null)
			{
				SourceText ??= sourceText;

				if (ValueMap.TryGetValue(sourceText, out var sources))
					sources.Add(source);
				else
					ValueMap[sourceText] = [source];
			}

			MergeInlineTranslations(inlineTranslations);
		}

		private void MergeInlineTranslations(IReadOnlyDictionary<string, TranslationSourceText>? inlineTranslations)
		{
			if (inlineTranslations is null) return;

			InlineMap ??= new Dictionary<string, TranslationSourceText>(StringComparer.OrdinalIgnoreCase);
			foreach (var (locale, text) in inlineTranslations)
				InlineMap.TryAdd(locale, text); // first-seen wins
		}

		public bool HasConflict => ValueMap.Count > 1;

		public IReadOnlyList<ConflictingValue> ConflictingValues =>
			ValueMap.Select(kvp => new ConflictingValue(kvp.Key, kvp.Value)).ToList();
	}
}

/// <summary>
/// Result of merging raw translation entries: deduplicated entries plus any conflicts found.
/// </summary>
public sealed record MergeResult(
	IReadOnlyList<MergedTranslationEntry> Entries,
	IReadOnlyList<KeyConflict> Conflicts);

/// <summary>
/// A key with two or more definitions that disagree on source text.
/// Each <see cref="ConflictingValue"/> groups all locations sharing the same source text.
/// </summary>
public sealed record KeyConflict(
	string Key,
	IReadOnlyList<ConflictingValue> Values);

/// <summary>
/// One distinct source text for a conflicting key, with all locations that use it.
/// </summary>
public sealed record ConflictingValue(
	TranslationSourceText SourceText,
	IReadOnlyList<SourceReference> Sources);
