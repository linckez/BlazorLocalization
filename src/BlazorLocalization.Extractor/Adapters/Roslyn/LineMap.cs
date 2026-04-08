namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Maps generated C# line numbers back to original Razor line numbers
/// using <c>#line</c> directive positions collected during Razor compilation.
/// </summary>
internal sealed class LineMap
{
    private readonly List<(int GeneratedLine, int OriginalLine)> _entries;

    public LineMap(List<(int GeneratedLine, int OriginalLine)> entries)
    {
        _entries = entries;
    }

    /// <summary>
    /// Given a 1-based line number in generated C#, returns the corresponding
    /// 1-based line in the original .razor / .cshtml file.
    /// Returns 0 if the line precedes any <c>#line</c> directive.
    /// </summary>
    public int MapToOriginalLine(int generatedLine)
    {
        var mapped = 0;
        foreach (var entry in _entries)
        {
            if (entry.GeneratedLine > generatedLine)
                break;
            // #line N means the line AFTER the directive is original line N
            mapped = entry.OriginalLine + (generatedLine - entry.GeneratedLine - 1);
        }
        return mapped;
    }
}
