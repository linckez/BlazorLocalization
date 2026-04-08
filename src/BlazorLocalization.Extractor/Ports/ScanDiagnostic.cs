using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Ports;

/// <summary>
/// A problem a scanner encountered during analysis.
/// </summary>
public sealed record ScanDiagnostic(
	DiagnosticLevel Level,
	string Message,
	SourceFilePath? File = null,
	int? Line = null);

public enum DiagnosticLevel
{
	Warning,
	Error
}
