using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Scanning.Sources;

/// <summary>
/// Describes the original source context for a syntax tree.
/// </summary>
public sealed record SourceOrigin(string FilePath, string ProjectName, LineMap? LineMap)
{
	/// <summary>
	/// Resolves a syntax node's position to the correct <see cref="SourceLocation"/>,
	/// mapping through the <see cref="LineMap"/> for generated-from-Razor sources.
	/// </summary>
	public SourceLocation ResolveLocation(SyntaxNode node)
	{
		var generatedLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
		if (LineMap is null)
			return new SourceLocation(FilePath, generatedLine, ProjectName);

		var mapped = LineMap.MapToOriginalLine(generatedLine);
		return new SourceLocation(FilePath, mapped > 0 ? mapped : generatedLine, ProjectName);
	}
}
