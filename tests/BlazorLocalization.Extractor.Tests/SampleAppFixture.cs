using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning;
using BlazorLocalization.Extractor.Scanning.Providers;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Runs Scanner + ResxImporter + merge against SampleBlazorApp once.
/// Shared across all SampleApp* test classes via <see cref="IClassFixture{T}"/>.
/// </summary>
public sealed class SampleAppFixture
{
	public ScanResult ScanResult { get; }
	public MergeResult MergeResult { get; }
	public string SolutionDirectory { get; }

	/// <summary>Keyed lookup into merged entries for spot-check assertions.</summary>
	public IReadOnlyDictionary<string, MergedTranslationEntry> EntryByKey { get; }

	public SampleAppFixture()
	{
		var (solutionDir, sampleAppDir) = ResolvePaths();
		SolutionDirectory = solutionDir;

		var providers = new ISourceProvider[]
		{
			new RazorGeneratedSourceProvider(sampleAppDir),
			new CSharpFileSourceProvider(sampleAppDir)
		};

		ScanResult = new Scanner(providers).Run();

		var codeEntries = ScanResult.Entries;
		var resxEntries = ResxImporter.ImportFromProject(sampleAppDir);

		var all = new List<TranslationEntry>(codeEntries.Count + resxEntries.Count);
		all.AddRange(codeEntries);
		all.AddRange(resxEntries);

		MergeResult = MergedTranslationEntry.FromRaw(all);
		EntryByKey = MergeResult.Entries.ToDictionary(e => e.Key);
	}

	private static (string SolutionDir, string SampleAppDir) ResolvePaths()
	{
		// Walk up from the test assembly's output directory to the repo root,
		// then navigate to tests/SampleBlazorApp.
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
