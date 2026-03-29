using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning.Providers;

namespace BlazorLocalization.Extractor.Scanning;

/// <summary>
/// Scans a single project directory: Roslyn analysis + .resx import → merged entries.
/// Single source of truth for the scanning pipeline — used by both extract and inspect commands.
/// </summary>
public static class ProjectScanner
{
	/// <summary>
	/// Full scan result including raw calls (for inspect) and merged entries (for export).
	/// </summary>
	public sealed record Result(
		string ProjectName,
		IReadOnlyList<ExtractedCall> Calls,
		MergeResult MergeResult);

	/// <summary>
	/// Scans <paramref name="projectDir"/> and returns calls, merged entries, and conflicts.
	/// </summary>
	public static Result Scan(string projectDir)
	{
		var projectName = Path.GetFileName(projectDir);

		var providers = new ISourceProvider[]
		{
			new RazorGeneratedSourceProvider(projectDir),
			new CSharpFileSourceProvider(projectDir)
		};

		var scanResult = new Scanner(providers).Run();

		var rawEntries = scanResult.Entries;
		var resxEntries = ResxImporter.ImportFromProject(projectDir);
		if (resxEntries.Count > 0)
		{
			var combined = new List<TranslationEntry>(rawEntries.Count + resxEntries.Count);
			combined.AddRange(rawEntries);
			combined.AddRange(resxEntries);
			rawEntries = combined;
		}

		return new Result(projectName, scanResult.Calls, MergedTranslationEntry.FromRaw(rawEntries));
	}
}
