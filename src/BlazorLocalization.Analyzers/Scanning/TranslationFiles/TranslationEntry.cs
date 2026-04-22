namespace BlazorLocalization.Analyzers.Scanning.TranslationFiles;

/// <summary>
/// A merged translation entry aggregated across all translation files for a single key.
/// Built by <see cref="TranslationFileLookup"/> — consumers never see individual file results.
/// </summary>
internal sealed class TranslationEntry
{
    /// <summary>Neutral-culture source text (e.g. English). Null if only culture files define this key.</summary>
    public string? SourceText { get; }

    /// <summary>Culture code → translated text (e.g. "da" → "Velkommen").</summary>
    public IReadOnlyDictionary<string, string> Translations { get; }

    /// <summary>Every contributing file + value, for provenance tracking and conflict diagnostics.</summary>
    public IReadOnlyList<TranslationSource> Sources { get; }

    public TranslationEntry(
        string? sourceText,
        IReadOnlyDictionary<string, string> translations,
        IReadOnlyList<TranslationSource> sources)
    {
        SourceText = sourceText;
        Translations = translations;
        Sources = sources;
    }
}

/// <summary>
/// One contribution from a specific translation file to a specific key.
/// </summary>
internal sealed class TranslationSource
{
    public string FilePath { get; }
    public string? Culture { get; }
    public string Value { get; }

    public TranslationSource(string filePath, string? culture, string value)
    {
        FilePath = filePath;
        Culture = culture;
        Value = value;
    }
}

/// <summary>
/// Two or more translation files define different values for the same key + culture.
/// Reported via BL0006. Conflicted keys are excluded from <see cref="TranslationFileLookup.TryGet"/>.
/// </summary>
internal sealed class TranslationConflict
{
    public string Key { get; }
    public string? Culture { get; }
    public IReadOnlyList<TranslationSource> ConflictingSources { get; }

    public TranslationConflict(string key, string? culture, IReadOnlyList<TranslationSource> conflictingSources)
    {
        Key = key;
        Culture = culture;
        ConflictingSources = conflictingSources;
    }
}
