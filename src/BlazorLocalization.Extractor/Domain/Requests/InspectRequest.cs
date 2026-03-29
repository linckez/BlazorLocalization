namespace BlazorLocalization.Extractor.Domain.Requests;

/// <summary>
/// Pure value object capturing all resolved inputs for the <c>inspect</c> command.
/// Built from CLI settings after path resolution, validated before execution.
/// </summary>
public sealed record InspectRequest(
	IReadOnlyList<string> ProjectDirs,
	bool JsonOutput,
	HashSet<string>? LocaleFilter,
	bool SourceOnly,
	PathStyle PathStyle)
{
	/// <summary>
	/// Returns an empty list when all inputs are consistent, or a list of human-readable error messages.
	/// </summary>
	public IReadOnlyList<string> Validate()
	{
		var errors = new List<string>();

		if (ProjectDirs.Count == 0)
			errors.Add("No .csproj projects found. Check that the path contains .NET projects.");

		if (SourceOnly && LocaleFilter is not null)
			errors.Add("--source-only and --locale cannot be used together.");

		return errors;
	}
}
