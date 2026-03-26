namespace BlazorLocalization.Extractor.Domain.Entries;

/// <summary>
/// A translation extracted from source code, ready for downstream export.
/// This is the boundary type between the scanning engine and format-specific exporters.
/// </summary>
/// <param name="Key">The translation key (e.g. "Home.WelcomeMessage").</param>
/// <param name="SourceText">The source text shape, or <c>null</c> for reference-only entries (indexer/GetString).</param>
/// <param name="Source">File path, line number, and project where the call was found.</param>
/// <param name="InlineTranslations">
/// Per-locale inline translations from <c>.For()</c> calls. Key is the locale (e.g. "da", "es-MX"),
/// value is the source text shape for that locale (same discriminated union as <see cref="SourceText"/>).
/// </param>
public sealed record TranslationEntry(
    string Key,
    TranslationSourceText? SourceText,
    SourceReference Source,
    IReadOnlyDictionary<string, TranslationSourceText>? InlineTranslations = null);
