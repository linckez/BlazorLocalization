using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using BlazorLocalization.Extractor.Adapters.Export;
using BlazorLocalization.Extractor.Domain;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Adapters.Cli;

/// <summary>
/// Interactive wizard that gathers CLI arguments via Spectre.Console prompts
/// when the tool is invoked with no arguments.
/// </summary>
internal static class InteractiveWizard
{
    public static string[] Run()
    {
        WriteBanner();
        AnsiConsole.WriteLine();

        var command = PromptWithDescriptions("What would you like to do?", new Dictionary<string, string>
        {
            ["extract"] = "Scan your projects and export source strings to translation files",
            ["inspect"] = "Check your translation setup — find missing, conflicting, or unused keys"
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

        if (command == "extract")
            AddExtractOptions(args);
        else if (command == "inspect")
            AddInspectOptions(args);

        AnsiConsole.WriteLine();
        return args.ToArray();
    }

    private static void WriteBanner()
    {
        var blazorPurple = new Color(81, 43, 212);
        var lightPurple = new Color(140, 100, 240);

        AnsiConsole.Write(new FigletText("Blazor") { Color = blazorPurple, Justification = Justify.Center });
        AnsiConsole.Write(new FigletText("Localization") { Color = lightPurple, Justification = Justify.Center });
        var version = typeof(InteractiveWizard).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "unknown";
        AnsiConsole.Write(new Text($"v{version}", new Style(blazorPurple, decoration: Decoration.Dim)) { Justification = Justify.Center });
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Text("Scan your projects for translation strings — extract, inspect, and export.", new Style(Color.Default, decoration: Decoration.Dim)) { Justification = Justify.Center });
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

        var sortedKeys = displayToPath.Keys
            .OrderBy(k => k, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering))
            .ToList();

        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select [green]projects[/] to scan:")
            .PageSize(15)
            .InstructionsText("[dim](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
            .AddChoices(sortedKeys);

        foreach (var name in displayToPath.Keys)
            prompt.Select(name);

        var selected = AnsiConsole.Prompt(prompt);
        return selected.Select(name => displayToPath[name]).ToList();
    }

    private static void AddExtractOptions(List<string> args)
    {
        var format = PromptEnum<ExportFormat>("Output [green]format[/]:");
        if (format is ExportFormat.Po)
            AnsiConsole.MarkupLine("[yellow]ℹ PO format has limitations: it cannot represent exact value matches (e.g. 'exactly 0' or 'exactly 42') or ordinal forms (1st, 2nd, 3rd). Affected entries will be flagged below.[/]");
        args.Add("-f");
        args.Add(format.ToString());

        var outputDir = AnsiConsole.Prompt(
            new TextPrompt<string>("Output [green]directory[/]:")
                .DefaultValue("./output"));
        args.Add("-o");
        args.Add(outputDir);

        var ext = ExporterFactory.GetFileExtension(format);
        if (!AnsiConsole.Confirm($"Include per-locale translations? (e.g. MyApp.da[green]{ext}[/], MyApp.es-MX[green]{ext}[/])", true))
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

    private static void AddInspectOptions(List<string> args)
    {
        var detail = PromptWithDescriptions("How much [green]detail[/]?", new Dictionary<string, string>
        {
            ["Standard"] = "Translation health check — problems highlighted (recommended)",
            ["Everything"] = "Every .resx entry per language and every code location found"
        });

        if (detail == "Everything")
        {
            args.Add("--show-resx-locales");
            args.Add("--show-extracted-calls");
        }
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
