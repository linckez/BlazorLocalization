using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Ports;

/// <summary>
/// The contract any scanner adapter implements to deliver data to the domain.
/// Roslyn scanner, Resx importer, or any future adapter — all produce this shape.
/// </summary>
public interface IScannerOutput
{
	/// <summary>Places where translation source text is defined.</summary>
	IReadOnlyList<TranslationDefinition> Definitions { get; }

	/// <summary>Places where translation keys are used (call sites).</summary>
	IReadOnlyList<TranslationReference> References { get; }

	/// <summary>Issues the scanner encountered during analysis.</summary>
	IReadOnlyList<ScanDiagnostic> Diagnostics { get; }
}
