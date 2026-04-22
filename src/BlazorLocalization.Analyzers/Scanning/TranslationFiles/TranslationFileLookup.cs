using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BlazorLocalization.Analyzers.Scanning.TranslationFiles;

/// <summary>
/// Format-agnostic aggregator that merges per-file parse results into a unified key→entry lookup.
/// Built once per CompilationStart from AdditionalFiles.
/// Consumers call <see cref="TryGet"/> and never know whether data came from resx, PO, or JSON.
/// </summary>
internal sealed class TranslationFileLookup
{
    /// <summary>Singleton for "no translation files found" — avoids null checks in consumers.</summary>
    public static readonly TranslationFileLookup Empty = new(
        new Dictionary<string, TranslationEntry>(),
        new HashSet<string>(StringComparer.Ordinal),
        Array.Empty<TranslationConflict>());

    private readonly IReadOnlyDictionary<string, TranslationEntry> _entries;
    private readonly IReadOnlyCollection<string> _conflictedKeys;

    public IReadOnlyList<TranslationConflict> Conflicts { get; }

    private TranslationFileLookup(
        IReadOnlyDictionary<string, TranslationEntry> entries,
        IReadOnlyCollection<string> conflictedKeys,
        IReadOnlyList<TranslationConflict> conflicts)
    {
        _entries = entries;
        _conflictedKeys = conflictedKeys;
        Conflicts = conflicts;
    }

    /// <summary>
    /// Returns the merged entry for a key, or false if the key is unknown or conflicted.
    /// Conflicted keys are intentionally excluded — BL0006 informs the user, enrichment is held back.
    /// </summary>
    public bool TryGet(string key, out TranslationEntry entry)
    {
        entry = null!;
        if (_conflictedKeys.Contains(key))
            return false;
        if (_entries.TryGetValue(key, out var found))
        {
            entry = found;
            return true;
        }
        return false;
    }

    /// <summary>Returns true if the key has conflicting values across translation files.</summary>
    public bool HasConflict(string key) => _conflictedKeys.Contains(key);

    // ──────────────────────────────────────────────────────────
    // L1 cache: per-file, keyed on SourceText identity
    // ──────────────────────────────────────────────────────────
    private static readonly ConditionalWeakTable<SourceText, FileParseResult> ParseCache = new();

    /// <summary>
    /// Builds a lookup from all AdditionalFiles using the provided parsers.
    /// Files are processed in path-sorted order for deterministic output.
    /// L1 cache skips re-parsing unchanged files (same SourceText reference).
    /// </summary>
    public static TranslationFileLookup Build(
        ImmutableArray<AdditionalText> additionalFiles,
        IReadOnlyList<ITranslationFileParser> parsers,
        CancellationToken cancellationToken)
    {
        if (additionalFiles.IsDefaultOrEmpty || parsers.Count == 0)
            return Empty;

        // Collect per-file results, path-sorted for determinism
        var fileResults = new List<(string Path, FileParseResult Result)>();

        foreach (var file in additionalFiles.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parser = FindParser(parsers, file.Path);
            if (parser is null)
                continue;

            var sourceText = file.GetText(cancellationToken);
            if (sourceText is null)
                continue;

            // L1 cache: same SourceText object → reuse parsed result
            var result = ParseCache.GetValue(sourceText,
                text => parser.Parse(file.Path, text, cancellationToken));

            if (result.Entries.Count > 0)
                fileResults.Add((file.Path, result));
        }

        if (fileResults.Count == 0)
            return Empty;

        return Merge(fileResults);
    }

    /// <summary>
    /// Merges per-file results into entries + conflicts.
    /// Merge rules:
    /// 1. Same key + same culture + same value → merge silently
    /// 2. Same key + same culture + different value → conflict, key excluded from TryGet
    /// 3. Same key + different cultures → normal
    /// </summary>
    private static TranslationFileLookup Merge(List<(string Path, FileParseResult Result)> fileResults)
    {
        // Sentinel for neutral culture — Dictionary<string, ...> can't use null keys
        const string neutralKey = "\0__neutral__";

        // Accumulate: key → culture (or sentinel) → list of (file, value)
        var accumulator = new Dictionary<string, Dictionary<string, List<(string File, string Value)>>>(StringComparer.Ordinal);

        foreach (var (path, result) in fileResults)
        {
            foreach (var kvp in result.Entries)
            {
                var key = kvp.Key;
                var value = kvp.Value;
                var culture = result.Culture ?? neutralKey;

                if (!accumulator.TryGetValue(key, out var cultureMap))
                {
                    cultureMap = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
                    accumulator[key] = cultureMap;
                }

                if (!cultureMap.TryGetValue(culture, out var sources))
                {
                    sources = new List<(string, string)>();
                    cultureMap[culture] = sources;
                }

                sources.Add((path, value));
            }
        }

        // Build entries and detect conflicts
        var entries = new Dictionary<string, TranslationEntry>(StringComparer.Ordinal);
        var conflictedKeys = new HashSet<string>(StringComparer.Ordinal);
        var conflicts = new List<TranslationConflict>();

        foreach (var kvp in accumulator)
        {
            var key = kvp.Key;
            var cultureMap = kvp.Value;
            var hasConflict = false;

            string? sourceText = null;
            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allSources = new List<TranslationSource>();

            foreach (var cultureKvp in cultureMap)
            {
                var cultureOrSentinel = cultureKvp.Key;
                var isNeutral = cultureOrSentinel == neutralKey;
                var culture = isNeutral ? null : cultureOrSentinel;
                var sources = cultureKvp.Value;

                // Record all sources for provenance
                foreach (var (file, value) in sources)
                    allSources.Add(new TranslationSource(file, culture, value));

                // Check for conflict: multiple distinct values for same culture
                var distinctValues = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (_, value) in sources)
                    distinctValues.Add(value);

                if (distinctValues.Count > 1)
                {
                    hasConflict = true;
                    conflictedKeys.Add(key);
                    conflicts.Add(new TranslationConflict(
                        key,
                        culture,
                        sources.Select(s => new TranslationSource(s.File, culture, s.Value))
                            .ToList()));
                    continue;
                }

                // No conflict — take the single value
                var resolvedValue = sources[0].Value;
                if (isNeutral)
                    sourceText = resolvedValue;
                else
                    translations[culture] = resolvedValue;
            }

            // Only populate entry if no conflict on this key
            if (!hasConflict)
            {
                entries[key] = new TranslationEntry(sourceText, translations, allSources);
            }
        }

        return new TranslationFileLookup(entries, conflictedKeys, conflicts);
    }

    private static ITranslationFileParser? FindParser(IReadOnlyList<ITranslationFileParser> parsers, string path)
    {
        for (var i = 0; i < parsers.Count; i++)
        {
            if (parsers[i].CanHandle(path))
                return parsers[i];
        }
        return null;
    }
}
