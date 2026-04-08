using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Roslyn adapter's implementation of <see cref="IScannerOutput"/>.
/// Also carries adapter-specific <see cref="RawCalls"/> for the inspect command.
/// </summary>
internal sealed record RoslynScannerOutput(
    IReadOnlyList<TranslationDefinition> Definitions,
    IReadOnlyList<TranslationReference> References,
    IReadOnlyList<ScanDiagnostic> Diagnostics,
    IReadOnlyList<ScannedCallSite> RawCalls) : IScannerOutput;
