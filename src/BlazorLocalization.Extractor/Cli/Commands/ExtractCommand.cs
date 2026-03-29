using BlazorLocalization.Extractor.Cli.Rendering;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Exporters;
using BlazorLocalization.Extractor.Scanning;
using BlazorLocalization.Extractor.Scanning.Providers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Cli.Commands;

/// <summary>
/// Extracts translation entries from Blazor projects and exports them in the specified format.
/// </summary>
public sealed class ExtractCommand : Command<ExtractSettings>
{
	public override int Execute(CommandContext context, ExtractSettings settings)
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
			if (resolveErrors.Count == 0)
				AnsiConsole.MarkupLine("[red]No projects found.[/]");
			return 1;
		}

		// When writing to stdout with JSON format, use the JSON renderer directly.
		var useJsonStdout = settings.Output is null && settings.Format is ExportFormat.Json;
		if (useJsonStdout)
			return ExecuteJson(settings, projectDirs);

		var exporter = ExporterFactory.Create(settings.Format);
		var (outputDir, outputFile) = ResolveOutputPath(settings.Output);

		var hasConflicts = false;
		var summaryRows = new List<(string Project, int Entries, int Conflicts)>();

		foreach (var projectDir in projectDirs)
		{
			ProjectResult result;

			if (settings.Output is not null && AnsiConsole.Profile.Capabilities.Interactive)
			{
				result = null!;
				AnsiConsole.Status()
					.Spinner(Spinner.Known.Dots)
					.Start($"Scanning [blue]{Markup.Escape(Path.GetFileName(projectDir))}[/]...", _ =>
					{
						result = ProcessProject(projectDir, settings, settings.Verbose);
					});
			}
			else
			{
				result = ProcessProject(projectDir, settings, settings.Verbose);
			}

			if (result.HasConflicts) hasConflicts = true;
			summaryRows.Add((result.Name, result.Entries.Count, result.Conflicts.Count));

			if (settings.Verbose)
				AnsiConsole.MarkupLine($"[grey]{Markup.Escape(result.Name)}: {result.Entries.Count} translation(s)[/]");

			var output = exporter.Export(result.Entries.ToList());

			if (outputDir is not null || outputFile is not null)
			{
				var filePath = outputFile
					?? Path.Combine(outputDir!, $"{result.Name}{ExporterFactory.GetFileExtension(settings.Format)}");
				File.WriteAllText(filePath, output);

				if (settings.Verbose)
					AnsiConsole.MarkupLine($"[green]Wrote {filePath}[/]");

				if (!settings.SourceOnly)
					ExportPerLocaleFiles(result.Entries.ToList(), settings, outputDir ?? Path.GetDirectoryName(outputFile)!, result.Name, exporter);
			}
			else
			{
				Console.Write(output);
			}

			ConflictRenderer.Render(result.Conflicts, result.Name);

			if (settings.Format is ExportFormat.Po)
				PoLimitationRenderer.Render(PoLimitation.Detect(result.Entries.ToList()), result.Name);
		}

		if ((outputDir is not null || outputFile is not null) && summaryRows.Count > 0)
		{
			AnsiConsole.WriteLine();
			AnsiConsole.Write(new Rule("[blue]Summary[/]").LeftJustified());
			AnsiConsole.WriteLine();

			var table = new Table()
				.Border(TableBorder.Rounded)
				.AddColumn("Project")
				.AddColumn("Entries")
				.AddColumn("Conflicts");

			foreach (var (project, entries, conflicts) in summaryRows)
			{
				var conflictMarkup = conflicts > 0
					? $"[yellow]{conflicts}[/]"
					: $"[green]{conflicts}[/]";

				table.AddRow(
					Markup.Escape(project),
					entries.ToString(),
					conflictMarkup);
			}

			AnsiConsole.Write(table);
		}

		if (hasConflicts && settings.ExitOnDuplicateKey)
			return 1;

		return 0;
	}

	private sealed record ProjectResult(
		string Name,
		IReadOnlyList<MergedTranslationEntry> Entries,
		IReadOnlyList<KeyConflict> Conflicts,
		bool HasConflicts);

	private static ProjectResult ProcessProject(string projectDir, ExtractSettings settings, bool verbose)
	{
		var projectName = Path.GetFileName(projectDir);
		var rawEntries = ScanProject(projectDir, verbose);
		var mergeResult = MergedTranslationEntry.FromRaw(rawEntries);

		var entries = mergeResult.Entries;
		var hasConflicts = mergeResult.Conflicts.Count > 0;

		if (hasConflicts && settings.OnDuplicateKey is ConflictStrategy.Skip)
		{
			var conflictKeys = mergeResult.Conflicts.Select(c => c.Key).ToHashSet();
			entries = entries.Where(e => !conflictKeys.Contains(e.Key)).ToList();
		}

		if (settings.PathStyle is PathStyle.Relative)
		{
			entries = entries.Select(e => e.RelativizeSources(projectDir)).ToList();
		}

		return new ProjectResult(projectName, entries, mergeResult.Conflicts, hasConflicts);
	}

	private int ExecuteJson(ExtractSettings settings, IReadOnlyList<string> projectDirs)
	{
		var hasConflicts = false;

		foreach (var projectDir in projectDirs)
		{
			var result = ProcessProject(projectDir, settings, verbose: false);
			if (result.HasConflicts) hasConflicts = true;
			JsonRenderer.RenderExtract(result.Name, result.Entries, result.Conflicts);
		}

		return hasConflicts && settings.ExitOnDuplicateKey ? 1 : 0;
	}

	private static IReadOnlyList<TranslationEntry> ScanProject(string projectDir, bool verbose)
	{
		if (verbose)
			AnsiConsole.MarkupLine($"[grey]Scanning {Path.GetFileName(projectDir)}...[/]");

		var providers = new ISourceProvider[]
		{
			new RazorGeneratedSourceProvider(projectDir),
			new CSharpFileSourceProvider(projectDir)
		};

		var codeEntries = new Scanner(providers).Run().Entries;
		var resxEntries = ResxImporter.ImportFromProject(projectDir);

		if (resxEntries.Count == 0)
			return codeEntries;

		var merged = new List<TranslationEntry>(codeEntries.Count + resxEntries.Count);
		merged.AddRange(codeEntries);
		merged.AddRange(resxEntries);
		return merged;
	}

	/// <summary>
	/// Determines whether the <c>--output</c> value is a file path or a directory path.
	/// Existing directories win, then <see cref="Path.HasExtension(string)"/> decides.
	/// </summary>
	private static (string? Dir, string? File) ResolveOutputPath(string? output)
	{
		if (output is null) return (null, null);

		if (Directory.Exists(output))
			return (output, null);

		if (Path.HasExtension(output))
		{
			var dir = Path.GetDirectoryName(output);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
			return (null, output);
		}

		Directory.CreateDirectory(output);
		return (output, null);
	}



	/// <summary>
	/// Writes per-locale translation files from inline <c>.For()</c> data.
	/// Each locale gets a separate file: <c>{ProjectName}.{locale}{ext}</c>.
	/// The entries are rewritten so the inline locale text becomes the source text for that file.
	/// </summary>
	private static void ExportPerLocaleFiles(
		IReadOnlyList<MergedTranslationEntry> entries,
		ExtractSettings settings,
		string outputDir,
		string projectName,
		ITranslationExporter exporter)
	{
		var locales = entries
			.Where(e => e.InlineTranslations is not null)
			.SelectMany(e => e.InlineTranslations!.Keys)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (settings.Locales is { Length: > 0 })
		{
			var filter = new HashSet<string>(settings.Locales, StringComparer.OrdinalIgnoreCase);
			var matched = locales.Where(l => filter.Contains(l)).ToList();

			foreach (var requested in settings.Locales.Where(r => !locales.Contains(r, StringComparer.OrdinalIgnoreCase)))
				AnsiConsole.MarkupLine($"[yellow]Warning: locale '{Markup.Escape(requested)}' not found in any .For() translation[/]");

			locales = matched;
		}

		if (locales.Count == 0) return;

		var ext = ExporterFactory.GetFileExtension(settings.Format);

		foreach (var locale in locales)
		{
			var localeEntries = entries
				.Where(e => e.InlineTranslations is not null && e.InlineTranslations.ContainsKey(locale))
				.Select(e => new MergedTranslationEntry(e.Key, e.InlineTranslations![locale], e.Sources))
				.ToList();

			var output = exporter.Export(localeEntries);
			var filePath = Path.Combine(outputDir, $"{projectName}.{locale}{ext}");
			File.WriteAllText(filePath, output);

			if (settings.Verbose)
				AnsiConsole.MarkupLine($"[green]Wrote {filePath}[/]");
		}
	}
}
