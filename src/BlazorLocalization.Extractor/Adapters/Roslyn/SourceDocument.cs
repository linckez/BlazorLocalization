using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// A syntax tree paired with its origin metadata.
/// For plain C# files, <see cref="LineMap"/> is null.
/// For Razor-generated C#, <see cref="LineMap"/> maps generated lines back to the .razor source.
/// </summary>
internal sealed record SourceDocument(
    SyntaxTree Tree,
    string OriginalFilePath,
    string ProjectDir,
    LineMap? LineMap)
{
    /// <summary>
    /// Resolves a 1-based generated line number to the original source line.
    /// For plain C# files (no <see cref="LineMap"/>), returns the line as-is.
    /// For Razor files, maps through <c>#line</c> directives.
    /// </summary>
    public int ResolveOriginalLine(int generatedLine)
    {
        if (LineMap is null)
            return generatedLine;

        var mapped = LineMap.MapToOriginalLine(generatedLine);
        return mapped > 0 ? mapped : generatedLine;
    }
}
