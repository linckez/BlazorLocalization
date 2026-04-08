using BlazorLocalization.Extractor.Adapters.Cli.Rendering;
using BlazorLocalization.Extractor.Adapters.Roslyn;
using BlazorLocalization.Extractor.Application;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// Translation health audit: shows code references, .resx entries, cross-references, and locale coverage.
/// </summary>
internal sealed class InspectCommand : Command<InspectSettings>
{
    protected override int Execute(CommandContext context, InspectSettings settings, CancellationToken cancellationToken)
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

        var request = new InspectRequest(
            ProjectDirs: projectDirs,
            JsonOutput: settings.ShouldOutputJson,
            LocaleFilter: localeFilter,
            ShowResxLocales: settings.ShowResxLocales,
            ShowExtractedCalls: settings.ShowExtractedCalls,
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
            var scan = ProjectScanner.Scan(projectDir, cancellationToken);

            if (request.LocaleFilter is not null)
            {
                var discovered = LocaleDiscovery.DiscoverLocales(scan.MergeResult.Entries);
                foreach (var requested in request.LocaleFilter.Where(r =>
                    !discovered.Contains(r, StringComparer.OrdinalIgnoreCase)))
                {
                    Console.Error.WriteLine($"Warning: locale '{requested}' not found in any translation");
                }
            }

            if (request.JsonOutput)
            {
                JsonRenderer.RenderInspect(
                    scan.ProjectName,
                    scan.MergeResult.Entries,
                    scan.MergeResult.Conflicts,
                    scan.MergeResult.InvalidEntries,
                    request.LocaleFilter,
                    request.PathStyle);
            }
            else
            {
                // 1. Extracted calls (opt-in) — downcast to adapter type
                if (request.ShowExtractedCalls)
                {
                    var roslynOutput = scan.ScannerOutputs.OfType<RoslynScannerOutput>().FirstOrDefault();
                    if (roslynOutput is not null)
                        ExtractedCallRenderer.Render(roslynOutput.RawCalls, scan.ProjectName, request.PathStyle);
                }

                // 2. Translation Entries
                TranslationEntryRenderer.Render(scan.MergeResult.Entries, scan.ProjectName, request.PathStyle);

                // 3. Conflicts
                TranslationEntryRenderer.RenderConflicts(scan.MergeResult.Conflicts, request.PathStyle);

                // 4. Invalid entries
                TranslationEntryRenderer.RenderInvalidEntries(scan.MergeResult.InvalidEntries, request.PathStyle);

                // 5. .resx Files
                if (request.ShowResxLocales)
                    TranslationEntryRenderer.RenderResxLocales(scan.MergeResult.Entries, request.LocaleFilter);
                else
                    TranslationEntryRenderer.RenderResxOverview(scan.MergeResult.Entries, request.LocaleFilter);

                // 6. Anomalies
                TranslationEntryRenderer.RenderAnomalies(scan.MergeResult.Entries, request.LocaleFilter);

                // 7. Cross-reference summary
                TranslationEntryRenderer.RenderCrossReferenceSummary(scan.MergeResult.Entries, scan.ProjectName);

                // 8. Legend
                var hasResxFiles = scan.MergeResult.Entries.Any(e =>
                    e.Definitions.Any(d => d.File.IsResx));
                TranslationEntryRenderer.RenderLegend(
                    scan.MergeResult.Entries, hasResxFiles, request.ShowExtractedCalls,
                    scan.MergeResult.InvalidEntries.Count > 0);
            }
        }

        return 0;
    }
}
