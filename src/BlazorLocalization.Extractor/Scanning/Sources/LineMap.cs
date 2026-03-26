namespace BlazorLocalization.Extractor.Scanning.Sources;

/// <summary>
/// Maps generated C# line numbers back to original Razor line numbers.
/// </summary>
public sealed class LineMap
{
	private readonly List<(int GeneratedLine, int OriginalLine)> _entries;

	public LineMap(List<(int GeneratedLine, int OriginalLine)> entries)
	{
		_entries = entries;
	}

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
