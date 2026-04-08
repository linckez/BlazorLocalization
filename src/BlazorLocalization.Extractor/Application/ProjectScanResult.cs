using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;

namespace BlazorLocalization.Extractor.Application;

/// <summary>
/// The result of scanning a single project through the full pipeline.
/// Carries everything any driving adapter (CLI, API, etc.) needs.
/// </summary>
/// <param name="ProjectName">Directory name of the scanned project.</param>
/// <param name="MergeResult">Merged translations, conflicts, and invalid entries.</param>
/// <param name="ScannerOutputs">
/// The raw scanner outputs preserved alongside the merge result.
/// Driving adapters can access diagnostics via <c>SelectMany(o => o.Diagnostics)</c>,
/// and adapter-specific data (e.g. raw calls) via pattern matching on concrete types.
/// </param>
public sealed record ProjectScanResult(
    string ProjectName,
    MergeResult MergeResult,
    IReadOnlyList<IScannerOutput> ScannerOutputs);
