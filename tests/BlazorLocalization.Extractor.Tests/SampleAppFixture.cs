using BlazorLocalization.Extractor.Application;
using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Runs ProjectScanner against SampleBlazorApp once.
/// Shared across all SampleApp* test classes via <see cref="IClassFixture{T}"/>.
/// </summary>
public sealed class SampleAppFixture
{
	public ProjectScanResult ScanResult { get; }
	public MergeResult MergeResult { get; }
	public string SolutionDirectory { get; }

	/// <summary>Keyed lookup into merged entries for spot-check assertions.</summary>
	public IReadOnlyDictionary<string, MergedTranslation> EntryByKey { get; }

	public SampleAppFixture()
	{
		var (solutionDir, sampleAppDir) = ResolvePaths();
		SolutionDirectory = solutionDir;

		ScanResult = ProjectScanner.Scan(sampleAppDir);
		MergeResult = ScanResult.MergeResult;
		EntryByKey = MergeResult.Entries.ToDictionary(e => e.Key);
	}

	private static (string SolutionDir, string SampleAppDir) ResolvePaths()
	{
		var dir = AppContext.BaseDirectory;
		while (dir is not null && !File.Exists(Path.Combine(dir, "BlazorLocalization.sln")))
			dir = Directory.GetParent(dir)?.FullName;

		if (dir is null)
			throw new InvalidOperationException(
				"Cannot find repo root (BlazorLocalization.sln). " +
				"Ensure tests run from within the repository tree.");

		var sampleApp = Path.Combine(dir, "tests", "SampleBlazorApp");
		if (!Directory.Exists(sampleApp))
			throw new DirectoryNotFoundException($"SampleBlazorApp not found at {sampleApp}");

		return (dir, sampleApp);
	}
}
