using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Scanning.Sources;

/// <summary>
/// Holds a syntax tree and its original source context.
/// </summary>
public sealed record SourceDocument(SyntaxTree Tree, SourceOrigin Origin);
