namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// Identifies a source code location by file path and line number.
/// </summary>
public sealed record SourceLocation(string FilePath, int Line, string ProjectName)
{
	/// <summary>
	/// Returns a copy with <see cref="FilePath"/> made relative to <paramref name="basePath"/>,
	/// using forward slashes for cross-platform consistency.
	/// </summary>
	public SourceLocation Relativize(string basePath) =>
		this with { FilePath = Path.GetRelativePath(basePath, FilePath).Replace('\\', '/') };
}
