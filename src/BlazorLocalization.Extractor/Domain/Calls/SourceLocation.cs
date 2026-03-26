namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// Identifies a source code location by file path and line number.
/// </summary>
public sealed record SourceLocation(string FilePath, int Line, string ProjectName);
