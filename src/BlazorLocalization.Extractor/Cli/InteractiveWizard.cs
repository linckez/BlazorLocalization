using System.ComponentModel;
using System.Reflection;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Scanning;
using BlazorLocalization.Extractor.Exporters;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Cli;

/// <summary>
/// Interactive wizard that gathers CLI arguments via Spectre.Console prompts
/// when the tool is invoked with no arguments.
/// </summary>
public static class InteractiveWizard
{
	public static string[] Run()
	{
		WriteBanner();
		AnsiConsole.WriteLine();

		var command = PromptWithDescriptions("What would you like to do?", new Dictionary<string, string>
		{
			["extract"] = "Scan projects, extract source strings, export to translation file",
			["inspect"] = "Show all detected IStringLocalizer calls (debug view)"
		});

		var path = AnsiConsole.Prompt(
			new TextPrompt<string>("Project/solution [green]root path[/]:")
				.DefaultValue("."));

		var discovered = ProjectDiscovery.Discover(path);
		if (discovered.Count == 0)
		{
			AnsiConsole.MarkupLine("[red]No projects found at that path.[/]");
			return [command, path];
		}

		var selectedPaths = SelectProjects(discovered, path);

		var args = new List<string> { command };
		foreach (var proj in selectedPaths)
			args.Add(proj);

		AddSharedOptions(args);

		if (command == "extract")
			AddExtractOptions(args);

		AnsiConsole.WriteLine();
		return args.ToArray();
	}

	private static void WriteBanner()
	{
		var blazorPurple = new Color(81, 43, 212);
		var lightPurple = new Color(140, 100, 240);
		var faintPurple = new Color(180, 150, 255);

		AnsiConsole.Write(new FigletText("Blazor") { Color = blazorPurple, Justification = Justify.Center });
		AnsiConsole.Write(new FigletText("Localization") { Color = lightPurple, Justification = Justify.Center });
		var version = typeof(InteractiveWizard).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion.Split('+')[0] ?? "unknown";
		AnsiConsole.Write(new Text($"v{version}", new Style(faintPurple, decoration: Decoration.Dim)) { Justification = Justify.Center });
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Text("Extract and export your Blazor source strings to any external translation provider.", new Style(Color.Grey)) { Justification = Justify.Center });
		AnsiConsole.WriteLine();
	}

	private static IReadOnlyList<string> SelectProjects(IReadOnlyList<string> discovered, string root)
	{
		if (discovered.Count == 1)
		{
			AnsiConsole.MarkupLine($"Found project: [green]{Path.GetFileName(discovered[0])}[/]");
			return discovered;
		}

		var rootFull = Path.GetFullPath(root);
		var displayToPath = discovered.ToDictionary(
			d => Path.GetRelativePath(rootFull, d),
			d => d);

		var prompt = new MultiSelectionPrompt<string>()
			.Title("Select [green]projects[/] to scan:")
			.PageSize(15)
			.InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
			.AddChoices(displayToPath.Keys);

		foreach (var name in displayToPath.Keys)
			prompt.Select(name);

		var selected = AnsiConsole.Prompt(prompt);
		return selected.Select(name => displayToPath[name]).ToList();
	}

	private static void AddSharedOptions(List<string> args)
	{
		var paths = PromptEnum<PathStyle>("Path style in [green]output[/]:");
		if (paths is not PathStyle.Relative)
		{
			args.Add("--paths");
			args.Add(paths.ToString());
		}
	}

	private static void AddExtractOptions(List<string> args)
	{
		var format = PromptEnum<ExportFormat>("Output [green]format[/]:");
		if (format is ExportFormat.Po)
			AnsiConsole.MarkupLine("[yellow]ℹ PO format cannot group exact matches (=0, =42) or ordinal plurals. Warnings will appear if affected entries are found.[/]");
		args.Add("-f");
		args.Add(format.ToString());

		var outputDir = AnsiConsole.Prompt(
			new TextPrompt<string>("Output [green]directory[/]:")
				.DefaultValue("./output"));
		args.Add("-o");
		args.Add(outputDir);

		var ext = ExporterFactory.GetFileExtension(format);
		if (!AnsiConsole.Confirm($"Include per-locale source texts from .For() calls? (e.g. MyApp.da[green]{ext}[/], MyApp.es-MX[green]{ext}[/])", true))
			args.Add("--source-only");

		var onDuplicateKey = PromptEnum<ConflictStrategy>("Duplicate translation key [green]strategy[/]:");
		if (onDuplicateKey is not ConflictStrategy.First)
		{
			args.Add("--on-duplicate-key");
			args.Add(onDuplicateKey.ToString());
		}

		if (AnsiConsole.Confirm("Verbose output?", false))
			args.Add("--verbose");
	}

	private static T PromptEnum<T>(string title) where T : struct, Enum
	{
		var values = Enum.GetValues<T>();
		var prompt = new SelectionPrompt<T>()
			.Title(title)
			.UseConverter(v =>
			{
				var desc = typeof(T).GetField(v.ToString())?
					.GetCustomAttribute<DescriptionAttribute>()?.Description;
				return desc is not null ? $"{v} [dim]— {desc}[/]" : v.ToString();
			})
			.AddChoices(values);

		return AnsiConsole.Prompt(prompt);
	}

	private static string PromptWithDescriptions(string title, Dictionary<string, string> choices)
	{
		var prompt = new SelectionPrompt<string>()
			.Title(title)
			.UseConverter(key => choices.TryGetValue(key, out var desc) ? $"{key} [dim]— {desc}[/]" : key)
			.AddChoices(choices.Keys);

		return AnsiConsole.Prompt(prompt);
	}
}
