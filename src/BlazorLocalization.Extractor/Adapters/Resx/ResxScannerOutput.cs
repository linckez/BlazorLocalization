using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;

namespace BlazorLocalization.Extractor.Adapters.Resx;

/// <summary>
/// Resx adapter's implementation of <see cref="IScannerOutput"/>.
/// Produces definitions only — .resx files define source text, they don't reference keys in code.
/// </summary>
internal sealed record ResxScannerOutput(
    IReadOnlyList<TranslationDefinition> Definitions,
    IReadOnlyList<ScanDiagnostic> Diagnostics) : IScannerOutput
{
    public IReadOnlyList<TranslationReference> References { get; } = [];
}
