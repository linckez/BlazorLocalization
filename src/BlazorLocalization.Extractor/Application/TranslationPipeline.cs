using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;

namespace BlazorLocalization.Extractor.Application;

/// <summary>
/// The application service. Takes N scanner outputs, merges by key, detects conflicts,
/// and computes cross-reference status. Scanner-agnostic — it only speaks domain types.
/// </summary>
public static class TranslationPipeline
{
	/// <summary>
	/// Merges all scanner outputs into a single <see cref="MergeResult"/>.
	/// </summary>
	public static MergeResult Run(IReadOnlyList<IScannerOutput> scannerOutputs)
	{
		var allDefinitions = scannerOutputs.SelectMany(s => s.Definitions).ToList();
		var allReferences = scannerOutputs.SelectMany(s => s.References).ToList();
		var allDiagnostics = scannerOutputs.SelectMany(s => s.Diagnostics).ToList();

		return Merge(allDefinitions, allReferences, allDiagnostics);
	}

	private static MergeResult Merge(
		List<TranslationDefinition> definitions,
		List<TranslationReference> references,
		List<ScanDiagnostic> diagnostics)
	{
		var builders = new Dictionary<string, MergeBuilder>(StringComparer.OrdinalIgnoreCase);

		foreach (var def in definitions)
		{
			if (!builders.TryGetValue(def.Key, out var builder))
			{
				builder = new MergeBuilder();
				builders[def.Key] = builder;
			}

			builder.AddDefinition(def);
		}

		foreach (var reference in references)
		{
			if (!builders.TryGetValue(reference.Key, out var builder))
			{
				builder = new MergeBuilder();
				builders[reference.Key] = builder;
			}

			builder.AddReference(reference);
		}

		var entries = new List<MergedTranslation>();
		var conflicts = new List<KeyConflict>();
		var invalid = new List<InvalidEntry>();

		foreach (var (key, builder) in builders.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				invalid.Add(new InvalidEntry(key, "Empty key", builder.DefinitionSites));
				continue;
			}

			entries.Add(new MergedTranslation(
				key,
				builder.SourceText,
				builder.DefinitionSites,
				builder.ReferenceSites,
				builder.MergedInlineTranslations,
				builder.IsKeyLiteral));

			if (builder.HasConflict)
				conflicts.Add(new KeyConflict(key, builder.ConflictingValues));
		}

		return new MergeResult(entries, conflicts, invalid, diagnostics);
	}

	private sealed class MergeBuilder
	{
		private TranslationSourceText? _sourceText;
		private readonly List<DefinitionSite> _definitionSites = [];
		private readonly List<ReferenceSite> _referenceSites = [];
		private readonly Dictionary<TranslationSourceText, List<DefinitionSite>> _valueMap = new();
		private Dictionary<string, TranslationSourceText>? _inlineMap;
		private bool _allKeysLiteral = true;

		public TranslationSourceText? SourceText => _sourceText;
		public List<DefinitionSite> DefinitionSites => _definitionSites;
		public List<ReferenceSite> ReferenceSites => _referenceSites;
		public bool IsKeyLiteral => _allKeysLiteral;

		public IReadOnlyDictionary<string, TranslationSourceText>? MergedInlineTranslations =>
			_inlineMap is { Count: > 0 } ? _inlineMap : null;

		public void AddDefinition(TranslationDefinition def)
		{
			_definitionSites.Add(def.Site);
			_sourceText ??= def.SourceText; // first-seen wins

			if (_valueMap.TryGetValue(def.SourceText, out var sites))
				sites.Add(def.Site);
			else
				_valueMap[def.SourceText] = [def.Site];

			MergeInlineTranslations(def.InlineTranslations);
		}

		public void AddReference(TranslationReference reference)
		{
			_referenceSites.Add(reference.Site);
			if (!reference.IsLiteral)
				_allKeysLiteral = false;
		}

		private void MergeInlineTranslations(IReadOnlyDictionary<string, TranslationSourceText>? inlineTranslations)
		{
			if (inlineTranslations is null) return;

			_inlineMap ??= new Dictionary<string, TranslationSourceText>(StringComparer.OrdinalIgnoreCase);
			foreach (var (locale, text) in inlineTranslations)
				_inlineMap.TryAdd(locale, text); // first-seen wins
		}

		public bool HasConflict => _valueMap.Count > 1;

		public IReadOnlyList<ConflictingValue> ConflictingValues =>
			_valueMap.Select(kvp => new ConflictingValue(kvp.Key, kvp.Value)).ToList();
	}
}
