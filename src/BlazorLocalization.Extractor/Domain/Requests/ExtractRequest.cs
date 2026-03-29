namespace BlazorLocalization.Extractor.Domain.Requests;

/// <summary>
/// Pure value object capturing all resolved inputs for the <c>extract</c> command.
/// Built from CLI settings after path resolution, validated before execution.
/// </summary>
public sealed record ExtractRequest(
	IReadOnlyList<string> ProjectDirs,
	ExportFormat Format,
	OutputTarget Output,
	HashSet<string>? LocaleFilter,
	bool SourceOnly,
	PathStyle PathStyle,
	bool Verbose,
	bool ExitOnDuplicateKey,
	ConflictStrategy OnDuplicateKey)
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

		if (Output is OutputTarget.StdoutTarget)
		{
			if (ProjectDirs.Count > 1)
				errors.Add("Multiple projects found. Use -o, or specify a single .csproj.");

			if (LocaleFilter is { Count: > 1 })
				errors.Add("Stdout supports one locale at a time. Use -o <dir> for multiple locales.");
		}

		if (Output is OutputTarget.FileTarget && ProjectDirs.Count > 1)
			errors.Add("Multiple projects require -o <dir>, not a single file.");

		return errors;
	}
}

/// <summary>
/// Where extracted output should go: stdout, a single file, or a directory (per-project/per-locale files).
/// </summary>
public abstract record OutputTarget
{
	private OutputTarget() { }

	/// <summary>Output to stdout in the requested format.</summary>
	public static readonly OutputTarget Stdout = new StdoutTarget();

	/// <summary>Output to a single file.</summary>
	public static OutputTarget File(string path) => new FileTarget(path);

	/// <summary>Output to a directory (one file per project and/or locale).</summary>
	public static OutputTarget Dir(string path) => new DirTarget(path);

	public sealed record StdoutTarget : OutputTarget;
	public sealed record FileTarget(string Path) : OutputTarget;
	public sealed record DirTarget(string Path) : OutputTarget;

	/// <summary>
	/// Determines the output target from the raw <c>-o</c> value.
	/// Pure logic only — does not touch the filesystem.
	/// </summary>
	public static OutputTarget FromRawOutput(string? output)
	{
		if (output is null)
			return Stdout;

		if (System.IO.Path.HasExtension(output))
			return File(output);

		return Dir(output);
	}
}
