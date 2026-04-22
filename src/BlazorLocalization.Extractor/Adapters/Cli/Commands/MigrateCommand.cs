using BlazorLocalization.Extractor.Adapters.Cli.Rendering;
using BlazorLocalization.Extractor.Adapters.Roslyn;
using BlazorLocalization.Extractor.Application;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// Replaces <c>Localizer["key"]</c> and <c>Localizer.GetString("key")</c> in <c>.razor</c> files
/// with <c>Localizer.Translation(key: "…", sourceMessage: "…").For(…)</c>.
/// </summary>
internal sealed class MigrateCommand : Command<MigrateSettings>
{
    protected override int Execute(CommandContext context, MigrateSettings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve paths — migrate targets exactly one project
        var (projectDirs, resolveErrors) = ProjectDiscovery.ResolveAll(settings.Paths);
        foreach (var err in resolveErrors)
            Console.Error.WriteLine(err);
        if (resolveErrors.Count > 0)
            return 1;

        if (projectDirs.Count != 1)
        {
            Console.Error.WriteLine($"migrate targets exactly one project — got {projectDirs.Count}. Pass a single project directory or .csproj path.");
            return 1;
        }

        // 2. Build request
        var localeFilter = settings.SourceOnly || settings.Locales is not { Length: > 0 }
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(settings.Locales!, StringComparer.OrdinalIgnoreCase);

        var request = new MigrateRequest(
            ProjectDir: projectDirs[0],
            LocaleFilter: localeFilter,
            Apply: settings.Apply);

        // 3. Validate
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        // 4. Execute
        ExecuteProject(request.ProjectDir, request.LocaleFilter, request.Apply, cancellationToken);
        return 0;
    }

    private static void ExecuteProject(
        string projectDir,
        IReadOnlySet<string> localeFilter,
        bool apply,
        CancellationToken cancellationToken)
    {
        var projectLabel = ProjectDiscovery.GetProjectName(projectDir);
        AnsiConsole.MarkupLine($"Scanning [blue]{Markup.Escape(projectLabel)}[/]...");

        // Scan the project (same Roslyn scanner as extract/inspect)
        var scan = ProjectScanner.Scan(projectDir, cancellationToken);
        var roslynOutput = scan.ScannerOutputs.OfType<RoslynScannerOutput>().FirstOrDefault();
        if (roslynOutput is null)
        {
            AnsiConsole.MarkupLine("[dim]No Roslyn scan output available.[/]");
            return;
        }

        // Delegate to Application layer for migration logic
        var migration = RazorMigrator.Migrate(roslynOutput, scan.MergeResult, localeFilter);

        if (migration.IsEmpty)
        {
            AnsiConsole.MarkupLine("[dim]No legacy localizer usages found in .razor files.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"  Found [green]{migration.ReplaceableCount}[/] replaceable calls, [yellow]{migration.SkippedCount}[/] skipped (non-literal key or extra arguments)");

        // Apply file writes if requested
        if (apply)
        {
            foreach (var result in migration.FileResults)
            {
                if (result.TransformedText is not null)
                    File.WriteAllText(result.FilePath, result.TransformedText);
            }
        }

        MigrationRenderer.Render(migration.FileResults, apply, projectDir);
    }
}
