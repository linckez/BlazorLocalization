namespace BlazorLocalization.Extractor.Scanning;

/// <summary>
/// Discovers .csproj project directories by scanning a root path recursively.
/// Skips common build-output and dependency directories.
/// </summary>
public static class ProjectDiscovery
{
	private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
	{
		"obj", "bin", "node_modules", ".git", ".vs", ".idea"
	};

	/// <summary>
	/// Resolves one or more input paths (directories or .csproj files) into
	/// deduplicated project directories. Returns errors for paths that don't exist.
	/// </summary>
	public static (IReadOnlyList<string> ProjectDirs, IReadOnlyList<string> Errors) ResolveAll(string[] paths)
	{
		var dirs = new List<string>();
		var errors = new List<string>();

		foreach (var raw in paths)
		{
			var expanded = ExpandPath(raw);

			if (expanded.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
			{
				if (!File.Exists(expanded))
				{
					errors.Add($"File not found: {raw}");
					continue;
				}

				dirs.Add(Path.GetDirectoryName(Path.GetFullPath(expanded))!);
			}
			else if (Directory.Exists(expanded))
			{
				var found = Discover(raw);
				if (found.Count == 0)
					errors.Add($"No projects found in: {raw}");
				else
					dirs.AddRange(found);
			}
			else
			{
				errors.Add($"Not found: {raw}");
			}
		}

		var deduped = dirs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		return (deduped, errors);
	}

	/// <summary>
	/// Returns distinct project root directories under <paramref name="root"/>.
	/// Each returned path is the directory containing a .csproj file.
	/// </summary>
	public static IReadOnlyList<string> Discover(string root)
	{
		var fullRoot = Path.GetFullPath(ExpandPath(root));
		if (!Directory.Exists(fullRoot))
			return [];

		return Directory.EnumerateFiles(fullRoot, "*.csproj", SearchOption.AllDirectories)
			.Where(path => !ShouldSkip(path))
			.Select(path => Path.GetDirectoryName(path)!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string ExpandPath(string path) =>
		path.StartsWith('~')
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'))
			: Environment.ExpandEnvironmentVariables(path);

	private static bool ShouldSkip(string path)
	{
		var sep = Path.DirectorySeparatorChar;
		return SkipDirs.Any(dir =>
			path.Contains($"{sep}{dir}{sep}", StringComparison.OrdinalIgnoreCase));
	}
}
