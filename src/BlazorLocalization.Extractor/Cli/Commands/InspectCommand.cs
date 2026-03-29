using BlazorLocalization.Extractor.Cli.Rendering;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Domain.Requests;
using BlazorLocalization.Extractor.Scanning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Cli.Commands;

/// <summary>
/// Debug command: shows raw call details and mapped translation entries.
/// </summary>
public sealed class InspectCommand : Command<InspectSettings>
{
	public override int Execute(CommandContext context, InspectSettings settings, CancellationToken cancellationToken)
	{
		// 1. Resolve paths
		var (projectDirs, resolveErrors) = ProjectDiscovery.ResolveAll(settings.Paths);
		foreach (var err in resolveErrors)
			Console.Error.WriteLine(err);

		// 2. Build request
		var localeFilter = settings.Locales is { Length: > 0 }
			? new HashSet<string>(settings.Locales, StringComparer.OrdinalIgnoreCase)
			: null;

		var request = new InspectRequest(
			ProjectDirs: projectDirs,
			JsonOutput: settings.ShouldOutputJson,
			LocaleFilter: localeFilter,
			SourceOnly: settings.SourceOnly,
			PathStyle: settings.PathStyle);

		// 3. Validate
		var errors = request.Validate();
		if (errors.Count > 0)
		{
			foreach (var error in errors)
				Console.Error.WriteLine(error);
			return 1;
		}

		// 4. Execute
		foreach (var projectDir in request.ProjectDirs)
		{
			var scan = ProjectScanner.Scan(projectDir);

			var calls = request.PathStyle is PathStyle.Relative
				? scan.Calls.Select(c => c.RelativizeLocation(projectDir)).ToList()
				: scan.Calls;

			var entries = request.PathStyle is PathStyle.Relative
				? scan.MergeResult.Entries.Select(e => e.RelativizeSources(projectDir)).ToList()
				: scan.MergeResult.Entries;

			var poLimitations = PoLimitation.Detect(entries);

			if (request.LocaleFilter is not null)
			{
				var discovered = LocaleDiscovery.DiscoverLocales(entries);
				foreach (var requested in request.LocaleFilter.Where(r =>
					!discovered.Contains(r, StringComparer.OrdinalIgnoreCase)))
				{
					Console.Error.WriteLine($"Warning: locale '{requested}' not found in any translation");
				}
			}

			if (request.JsonOutput)
			{
				JsonRenderer.RenderInspect(scan.ProjectName, calls, entries, scan.MergeResult.Conflicts,
					poLimitations,
					request.SourceOnly ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : request.LocaleFilter);
			}
			else
			{
				ExtractedCallRenderer.Render(calls, scan.ProjectName);
				TranslationEntryRenderer.Render(entries, scan.ProjectName);
				if (!request.SourceOnly)
				{
					TranslationEntryRenderer.RenderLocaleSummary(entries, request.LocaleFilter);
					TranslationEntryRenderer.RenderLocales(entries, request.LocaleFilter);
				}
				ConflictRenderer.Render(scan.MergeResult.Conflicts, scan.ProjectName);
				PoLimitationRenderer.Render(poLimitations, scan.ProjectName);
			}
		}

		return 0;
	}
}
