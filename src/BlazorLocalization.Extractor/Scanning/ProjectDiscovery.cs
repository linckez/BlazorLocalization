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
	/// Returns distinct project root directories under <paramref name="root"/>.
	/// Each returned path is the directory containing a .csproj file.
	/// </summary>
	public static IReadOnlyList<string> Discover(string root)
	{
		var expanded = root.StartsWith('~')
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), root[1..].TrimStart('/'))
			: Environment.ExpandEnvironmentVariables(root);
		var fullRoot = Path.GetFullPath(expanded);
		if (!Directory.Exists(fullRoot))
			return [];

		return Directory.EnumerateFiles(fullRoot, "*.csproj", SearchOption.AllDirectories)
			.Where(path => !ShouldSkip(path))
			.Select(path => Path.GetDirectoryName(path)!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static bool ShouldSkip(string path)
	{
		var sep = Path.DirectorySeparatorChar;
		return SkipDirs.Any(dir =>
			path.Contains($"{sep}{dir}{sep}", StringComparison.OrdinalIgnoreCase));
	}
}
