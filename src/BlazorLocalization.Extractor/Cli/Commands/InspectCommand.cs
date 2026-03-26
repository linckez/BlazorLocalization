using BlazorLocalization.Extractor.Cli.Rendering;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning;
using BlazorLocalization.Extractor.Scanning.Providers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Cli.Commands;

/// <summary>
/// Debug command: shows raw <see cref="ExtractedCall"/> details and mapped <see cref="TranslationEntry"/> records.
/// </summary>
public sealed class InspectCommand : Command<InspectSettings>
{
	public override int Execute(CommandContext context, InspectSettings settings)
	{
		if (settings.SourceOnly && settings.Locales is { Length: > 0 })
		{
			AnsiConsole.MarkupLine("[red]--source-only and --locale cannot be used together.[/]");
			return 1;
		}

		var projectDirs = ResolveProjectDirs(settings);
		if (projectDirs.Count == 0)
		{
			if (!settings.ShouldOutputJson)
				AnsiConsole.MarkupLine("[red]No projects found.[/]");
			return 1;
		}

		foreach (var projectDir in projectDirs)
		{
			var providers = new ISourceProvider[]
			{
				new RazorGeneratedSourceProvider(projectDir),
				new CSharpFileSourceProvider(projectDir)
			};

			var projectName = Path.GetFileName(projectDir);

			var scanResult = new Scanner(providers).Run();

			var calls = settings.Paths is PathStyle.Relative
				? scanResult.Calls.Select(c => c with
				{
					Location = c.Location with
					{
						FilePath = Path.GetRelativePath(projectDir, c.Location.FilePath).Replace('\\', '/')
					}
				}).ToList()
				: scanResult.Calls;

			var rawEntries = scanResult.Entries;
			var resxEntries = ResxImporter.ImportFromProject(projectDir);
			if (resxEntries.Count > 0)
			{
				var combined = new List<TranslationEntry>(rawEntries.Count + resxEntries.Count);
				combined.AddRange(rawEntries);
				combined.AddRange(resxEntries);
				rawEntries = combined;
			}

			var mergeResult = MergedTranslationEntry.FromRaw(rawEntries);

			var entries = mergeResult.Entries;
			if (settings.Paths is PathStyle.Relative)
			{
				entries = entries.Select(e => e with
				{
					Sources = e.Sources.Select(s => s with
					{
						FilePath = Path.GetRelativePath(projectDir, s.FilePath).Replace('\\', '/')
					}).ToList()
				}).ToList();
			}

			var poLimitations = PoLimitation.Detect(entries);
			var localeFilter = settings.SourceOnly ? null
				: settings.Locales is { Length: > 0 }
					? new HashSet<string>(settings.Locales, StringComparer.OrdinalIgnoreCase)
					: null;

			if (localeFilter is not null)
			{
				var discoveredLocales = entries
					.Where(e => e.InlineTranslations is not null)
					.SelectMany(e => e.InlineTranslations!.Keys)
					.ToHashSet(StringComparer.OrdinalIgnoreCase);

				foreach (var requested in settings.Locales!.Where(r => !discoveredLocales.Contains(r)))
					AnsiConsole.MarkupLine($"[yellow]Warning: locale '{Markup.Escape(requested)}' not found in any .For() translation[/]");
			}

			if (settings.ShouldOutputJson)
			{
				JsonRenderer.RenderInspect(projectName, calls, entries, mergeResult.Conflicts, poLimitations,
					settings.SourceOnly ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : localeFilter);
			}
			else
			{
				ExtractedCallRenderer.Render(calls, projectName);
				TranslationEntryRenderer.Render(entries, projectName);
				if (!settings.SourceOnly)
				{
					TranslationEntryRenderer.RenderLocaleSummary(entries, localeFilter);
					TranslationEntryRenderer.RenderLocales(entries, localeFilter);
				}
				ConflictRenderer.Render(mergeResult.Conflicts, projectName);
				PoLimitationRenderer.Render(poLimitations, projectName);
			}
		}

		return 0;
	}

	private static IReadOnlyList<string> ResolveProjectDirs(SharedSettings settings)
	{
		if (settings.Projects is { Length: > 0 })
			return settings.Projects.Select(Path.GetFullPath).ToList();

		return ProjectDiscovery.Discover(settings.Path);
	}
}
