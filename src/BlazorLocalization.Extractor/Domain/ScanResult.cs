using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;

namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Composite result from <see cref="Scanning.Scanner.Run"/>: raw Roslyn call data for debug/inspect,
/// plus semantically interpreted <see cref="TranslationEntry"/> records for export.
/// </summary>
public sealed record ScanResult(
	IReadOnlyList<ExtractedCall> Calls,
	IReadOnlyList<TranslationEntry> Entries);
