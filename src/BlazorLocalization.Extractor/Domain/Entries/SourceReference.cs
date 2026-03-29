namespace BlazorLocalization.Extractor.Domain.Entries;

/// <summary>
/// Where in the source code a translation string was found.
/// Carries enough context for translators and auditors to trace back to the original usage.
/// </summary>
public sealed record SourceReference(string FilePath, int Line, string ProjectName, string? Context)
{
	/// <summary>
	/// Returns a copy with <see cref="FilePath"/> made relative to <paramref name="basePath"/>,
	/// using forward slashes for cross-platform consistency.
	/// </summary>
	public SourceReference Relativize(string basePath) =>
		this with { FilePath = Path.GetRelativePath(basePath, FilePath).Replace('\\', '/') };
}
