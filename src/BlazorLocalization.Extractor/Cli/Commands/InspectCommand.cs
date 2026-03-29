using BlazorLocalization.Extractor.Cli.Rendering;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning;
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

		var (projectDirs, resolveErrors) = ProjectDiscovery.ResolveAll(settings.Paths);
		if (resolveErrors.Count > 0)
		{
			foreach (var err in resolveErrors)
				AnsiConsole.MarkupLine($"[red]{Markup.Escape(err)}[/]");
		}
		if (projectDirs.Count == 0)
		{
			if (resolveErrors.Count == 0 && !settings.ShouldOutputJson)
				AnsiConsole.MarkupLine("[red]No projects found.[/]");
			return 1;
		}

		foreach (var projectDir in projectDirs)
		{
			var scanResult = ProjectScanner.Scan(projectDir);
			var projectName = scanResult.ProjectName;

			var calls = settings.PathStyle is PathStyle.Relative
				? scanResult.Calls.Select(c => c.RelativizeLocation(projectDir)).ToList()
				: scanResult.Calls;

			var entries = scanResult.MergeResult.Entries;
			if (settings.PathStyle is PathStyle.Relative)
				entries = entries.Select(e => e.RelativizeSources(projectDir)).ToList();

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
				JsonRenderer.RenderInspect(projectName, calls, entries, scanResult.MergeResult.Conflicts, poLimitations,
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
				ConflictRenderer.Render(scanResult.MergeResult.Conflicts, projectName);
				PoLimitationRenderer.Render(poLimitations, projectName);
			}
		}

		return 0;
	}
}
