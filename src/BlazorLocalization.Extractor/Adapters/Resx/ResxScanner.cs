using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;

namespace BlazorLocalization.Extractor.Adapters.Resx;

/// <summary>
/// Orchestrates a .resx-based scan of a project directory.
/// Enumerates .resx files, groups by base name, parses XML, and converts to domain types.
/// </summary>
internal static class ResxScanner
{
    /// <summary>
    /// Scans a project directory for .resx files and returns a port-compliant <see cref="IScannerOutput"/>.
    /// Neutral .resx → <see cref="TranslationDefinition"/> with source text.
    /// Culture-specific .resx → inline translations on the same definition.
    /// Culture-only keys (no neutral) → skipped with a <see cref="ScanDiagnostic"/> warning.
    /// </summary>
    public static ResxScannerOutput Scan(string projectDir)
    {
        var definitions = new List<TranslationDefinition>();
        var diagnostics = new List<ScanDiagnostic>();

        var files = ResxFileParser.EnumerateResxFiles(projectDir);
        var groups = ResxFileParser.GroupByBaseName(files);

        foreach (var (_, group) in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            ImportGroup(group, projectDir, definitions, diagnostics);
        }

        return new ResxScannerOutput(definitions, diagnostics);
    }

    private static void ImportGroup(
        ResxFileParser.ResxFileGroup group,
        string projectDir,
        List<TranslationDefinition> definitions,
        List<ScanDiagnostic> diagnostics)
    {
        // Parse neutral file
        var neutralEntries = group.NeutralPath is not null
            ? ResxFileParser.ParseResx(group.NeutralPath)
            : new Dictionary<string, (string Value, string? Comment, int Line)>();

        // Parse all culture files
        var cultureEntries = new Dictionary<string, IReadOnlyDictionary<string, (string Value, string? Comment, int Line)>>();
        foreach (var (culture, path) in group.CulturePaths)
            cultureEntries[culture] = ResxFileParser.ParseResx(path);

        // Collect all keys across neutral and culture files
        var allKeys = new HashSet<string>(neutralEntries.Keys);
        foreach (var entries in cultureEntries.Values)
            allKeys.UnionWith(entries.Keys);

        foreach (var key in allKeys.Order(StringComparer.Ordinal))
        {
            // Neutral → SourceText + DefinitionSite
            if (!neutralEntries.TryGetValue(key, out var neutral))
            {
                // Culture-only key: no source text available from resx
                var cultures = cultureEntries
                    .Where(e => e.Value.ContainsKey(key))
                    .Select(e => e.Key);
                diagnostics.Add(new ScanDiagnostic(
                    DiagnosticLevel.Warning,
                    $"Key \"{key}\" found in culture file(s) [{string.Join(", ", cultures)}] but not in neutral .resx"));
                continue;
            }

            var file = new SourceFilePath(group.NeutralPath!, projectDir);
            var site = new DefinitionSite(file, neutral.Line, DefinitionKind.ResourceFile, neutral.Comment);

            // Culture files → InlineTranslations
            Dictionary<string, TranslationSourceText>? inlineTranslations = null;
            foreach (var (culture, entries) in cultureEntries)
            {
                if (entries.TryGetValue(key, out var cultureEntry))
                {
                    inlineTranslations ??= new Dictionary<string, TranslationSourceText>(StringComparer.OrdinalIgnoreCase);
                    inlineTranslations[culture] = new SingularText(cultureEntry.Value);
                }
            }

            definitions.Add(new TranslationDefinition(
                key,
                new SingularText(neutral.Value),
                site,
                inlineTranslations));
        }
    }
}
