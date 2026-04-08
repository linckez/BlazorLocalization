using BlazorLocalization.Extractor.Adapters.Cli.Rendering;
using BlazorLocalization.Extractor.Adapters.Export;
using BlazorLocalization.Extractor.Application;
using BlazorLocalization.Extractor.Domain;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// Extracts translation entries from Blazor projects and exports them in the specified format.
/// </summary>
internal sealed class ExtractCommand : Command<ExtractSettings>
{
    protected override int Execute(CommandContext context, ExtractSettings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve paths
        var (projectDirs, resolveErrors) = ProjectDiscovery.ResolveAll(settings.Paths);
        foreach (var err in resolveErrors)
            Console.Error.WriteLine(err);
        if (resolveErrors.Count > 0)
            return 1;

        // 2. Build request
        var localeFilter = settings.Locales is { Length: > 0 }
            ? new HashSet<string>(settings.Locales, StringComparer.OrdinalIgnoreCase)
            : null;

        var request = new ExtractRequest(
            ProjectDirs: projectDirs,
            Format: settings.Format,
            Output: OutputTarget.FromRawOutput(settings.Output),
            LocaleFilter: localeFilter,
            SourceOnly: settings.SourceOnly,
            PathStyle: settings.PathStyle,
            Verbose: settings.Verbose,
            ExitOnDuplicateKey: settings.ExitOnDuplicateKey,
            OnDuplicateKey: settings.OnDuplicateKey);

        // 3. Validate
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        // 4. Execute
        return request.Output switch
        {
            OutputTarget.StdoutTarget => ExecuteStdout(request, cancellationToken),
            OutputTarget.FileTarget f => ExecuteFile(request, f.Path, cancellationToken),
            OutputTarget.DirTarget d => ExecuteDir(request, d.Path, cancellationToken),
            _ => 1
        };
    }

    private static int ExecuteStdout(ExtractRequest request, CancellationToken cancellationToken)
    {
        var projectDir = request.ProjectDirs[0];
        var scan = ProjectScanner.Scan(projectDir, cancellationToken);
        var entries = ApplyConflictStrategy(scan.MergeResult, request);
        entries = FilterUnresolvableReferences(entries);

        WarnMissingLocales(entries, request);

        if (request.LocaleFilter is { Count: 1 })
        {
            var locale = request.LocaleFilter.First();
            entries = LocaleDiscovery.EntriesForLocale(entries, locale);
        }

        var exporter = ExporterFactory.Create(request.Format);
        Console.Write(exporter.Export(entries, request.PathStyle));

        foreach (var conflict in scan.MergeResult.Conflicts)
            Console.Error.WriteLine($"Warning: duplicate key '{conflict.Key}' with {conflict.Values.Count} different values");

        foreach (var invalid in scan.MergeResult.InvalidEntries)
            Console.Error.WriteLine($"Warning: invalid translation skipped — {invalid.Reason} at {string.Join(", ", invalid.Sites.Select(s => s.File.FileName))}");

        return request.ExitOnDuplicateKey && scan.MergeResult.Conflicts.Count > 0 ? 1 : 0;
    }

    private static int ExecuteFile(ExtractRequest request, string filePath, CancellationToken cancellationToken)
    {
        var projectDir = request.ProjectDirs[0];
        var scan = ProjectScanner.Scan(projectDir, cancellationToken);
        var entries = ApplyConflictStrategy(scan.MergeResult, request);
        entries = FilterUnresolvableReferences(entries);

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var exporter = ExporterFactory.Create(request.Format);
        System.IO.File.WriteAllText(filePath, exporter.Export(entries, request.PathStyle));

        if (request.Verbose)
            AnsiConsole.MarkupLine($"[green]Wrote {filePath}[/]");

        ReportConflicts(scan.MergeResult.Conflicts, scan.ProjectName);
        ReportInvalidEntries(scan.MergeResult.InvalidEntries, scan.ProjectName);
        return request.ExitOnDuplicateKey && scan.MergeResult.Conflicts.Count > 0 ? 1 : 0;
    }

    private static int ExecuteDir(ExtractRequest request, string outputDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDir);
        var exporter = ExporterFactory.Create(request.Format);
        var ext = ExporterFactory.GetFileExtension(request.Format);
        var hasConflicts = false;
        var summaryRows = new List<(string Project, int Entries, int Conflicts)>();

        foreach (var projectDir in request.ProjectDirs)
        {
            ProjectScanResult scan;

            if (AnsiConsole.Profile.Capabilities.Interactive)
            {
                scan = null!;
                AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .Start($"Scanning [blue]{Markup.Escape(Path.GetFileName(projectDir))}[/]...", _ =>
                    {
                        scan = ProjectScanner.Scan(projectDir, cancellationToken);
                    });
            }
            else
            {
                scan = ProjectScanner.Scan(projectDir, cancellationToken);
            }

            var entries = ApplyConflictStrategy(scan.MergeResult, request);
            entries = FilterUnresolvableReferences(entries);
            if (scan.MergeResult.Conflicts.Count > 0) hasConflicts = true;

            if (request.Verbose)
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(scan.ProjectName)}: {entries.Count} translation(s)[/]");

            var filePath = Path.Combine(outputDir, $"{scan.ProjectName}{ext}");
            System.IO.File.WriteAllText(filePath, exporter.Export(entries, request.PathStyle));
            if (request.Verbose)
                AnsiConsole.MarkupLine($"[green]Wrote {filePath}[/]");

            if (!request.SourceOnly)
                ExportPerLocaleFiles(entries, request, outputDir, scan.ProjectName, exporter, ext);

            summaryRows.Add((scan.ProjectName, entries.Count, scan.MergeResult.Conflicts.Count));
            ReportConflicts(scan.MergeResult.Conflicts, scan.ProjectName);
            ReportInvalidEntries(scan.MergeResult.InvalidEntries, scan.ProjectName);

            if (request.Format is ExportFormat.Po)
                PoLimitationRenderer.Render(PoLimitation.Detect(entries), scan.ProjectName);
        }

        if (summaryRows.Count > 0)
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
                var conflictMarkup = conflicts > 0 ? $"[yellow]{conflicts}[/]" : $"[green]{conflicts}[/]";
                table.AddRow(Markup.Escape(project), entries.ToString(), conflictMarkup);
            }

            AnsiConsole.Write(table);
        }

        return hasConflicts && request.ExitOnDuplicateKey ? 1 : 0;
    }

    private static IReadOnlyList<MergedTranslation> ApplyConflictStrategy(
        MergeResult mergeResult, ExtractRequest request)
    {
        var entries = mergeResult.Entries;
        if (mergeResult.Conflicts.Count > 0 && request.OnDuplicateKey is ConflictStrategy.Skip)
        {
            var conflictKeys = mergeResult.Conflicts.Select(c => c.Key).ToHashSet();
            entries = entries.Where(e => !conflictKeys.Contains(e.Key)).ToList();
        }
        return entries;
    }

    private static IReadOnlyList<MergedTranslation> FilterUnresolvableReferences(
        IReadOnlyList<MergedTranslation> entries)
    {
        return entries.Where(e => e.IsKeyLiteral || e.SourceText is not null).ToList();
    }

    private static void ExportPerLocaleFiles(
        IReadOnlyList<MergedTranslation> entries,
        ExtractRequest request,
        string outputDir,
        string projectName,
        ITranslationExporter exporter,
        string ext)
    {
        var locales = LocaleDiscovery.DiscoverLocales(entries, request.LocaleFilter);

        if (request.LocaleFilter is not null)
            WarnMissingLocales(entries, request);

        foreach (var locale in locales)
        {
            var localeEntries = LocaleDiscovery.EntriesForLocale(entries, locale);
            var filePath = Path.Combine(outputDir, $"{projectName}.{locale}{ext}");
            System.IO.File.WriteAllText(filePath, exporter.Export(localeEntries, request.PathStyle));
            if (request.Verbose)
                AnsiConsole.MarkupLine($"[green]Wrote {filePath}[/]");
        }
    }

    private static void WarnMissingLocales(IReadOnlyList<MergedTranslation> entries, ExtractRequest request)
    {
        if (request.LocaleFilter is null) return;

        var discovered = LocaleDiscovery.DiscoverLocales(entries);
        foreach (var requested in request.LocaleFilter.Where(r =>
            !discovered.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine($"Warning: locale '{requested}' not found in any translation");
        }
    }

    private static void ReportConflicts(IReadOnlyList<KeyConflict> conflicts, string projectName)
    {
        ConflictRenderer.Render(conflicts, projectName);
    }

    private static void ReportInvalidEntries(IReadOnlyList<InvalidEntry> invalidEntries, string projectName)
    {
        if (invalidEntries.Count == 0) return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[red]✗ {invalidEntries.Count} invalid translation(s) skipped in {Markup.Escape(projectName)}[/]");
        foreach (var entry in invalidEntries)
        {
            var locations = string.Join(", ", entry.Sites.Select(s => s.File.FileName));
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(entry.Reason)}[/] at [dim]{Markup.Escape(locations)}[/]");
        }
    }
}
