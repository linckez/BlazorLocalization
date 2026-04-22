using BlazorLocalization.Extractor.Application;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Adapters.Cli.Rendering;

/// <summary>
/// Renders <see cref="MigrationFileResult"/>s as a Spectre.Console summary table with warning sections.
/// </summary>
internal static class MigrationRenderer
{
    public static void Render(IReadOnlyList<MigrationFileResult> results, bool isApply, string projectRoot)
    {
        var totalReplaced = results.Sum(r => r.Replaced.Count);
        var totalSkipped = results.Sum(r => r.Skipped.Count);
        var totalNotFound = results.Sum(r => r.NotFound.Count);
        var totalValidationFailed = results.Sum(r => r.ValidationFailed.Count);
        var totalApplied = totalReplaced + totalNotFound;
        var filesChanged = results.Count(r => r.TransformedText is not null);

        if (totalApplied == 0 && totalSkipped == 0 && totalValidationFailed == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]No Localizer[[\"key\"]] or Localizer.GetString(\"key\") usages found in your .razor files.[/]");
            return;
        }

        // Summary table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Default)
            .AddColumn("File")
            .AddColumn(new TableColumn("Migrated").RightAligned())
            .AddColumn(new TableColumn("Skipped").RightAligned())
            .AddColumn(new TableColumn("No Match").RightAligned());

        foreach (var r in results.Where(r => r.Replaced.Count > 0 || r.Skipped.Count > 0 || r.NotFound.Count > 0))
        {
            var relativePath = Path.GetRelativePath(projectRoot, r.FilePath);
            table.AddRow(
                Markup.Escape(relativePath),
                r.Replaced.Count > 0 ? $"[green]{r.Replaced.Count}[/]" : "[dim]0[/]",
                r.Skipped.Count > 0 ? $"[yellow]{r.Skipped.Count}[/]" : "[dim]0[/]",
                r.NotFound.Count > 0 ? $"[yellow]{r.NotFound.Count}[/]" : "[dim]0[/]");
        }

        table.AddEmptyRow();
        table.AddRow(
            "[bold]Total[/]",
            $"[bold green]{totalReplaced}[/]",
            totalSkipped > 0 ? $"[bold yellow]{totalSkipped}[/]" : $"[bold dim]0[/]",
            totalNotFound > 0 ? $"[bold yellow]{totalNotFound}[/]" : $"[bold dim]0[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        // Warning: skipped usages
        if (totalSkipped > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]⚠ {totalSkipped} skipped — non-literal key or extra arguments:[/]");
            foreach (var r in results)
            {
                var relativePath = Path.GetRelativePath(projectRoot, r.FilePath);
                foreach (var m in r.Skipped)
                    AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(relativePath)}:{m.Line}[/]    {Markup.Escape(m.Key)}");
            }
        }

        // Warning: keys not found
        if (totalNotFound > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]⚠ {totalNotFound} keys not found in your .resx files — replaced with an empty source text:[/]");
            foreach (var r in results)
            {
                var relativePath = Path.GetRelativePath(projectRoot, r.FilePath);
                foreach (var m in r.NotFound)
                    AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(relativePath)}:{m.Line}[/]    \"{Markup.Escape(m.Key)}\"");
            }
        }

        // Warning: validation failed
        if (totalValidationFailed > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]⚠ {totalValidationFailed} replacements failed syntax validation and were skipped:[/]");
            foreach (var r in results)
            {
                var relativePath = Path.GetRelativePath(projectRoot, r.FilePath);
                foreach (var m in r.ValidationFailed)
                    AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(relativePath)}:{m.Line}[/]    \"{Markup.Escape(m.Key)}\"");
            }
        }

        // Footer
        AnsiConsole.WriteLine();
        if (isApply)
        {
            var footer = $"[green]✓ {totalReplaced} replacements across {filesChanged} files[/]";
            if (totalNotFound > 0)
                footer += $" [dim]({totalNotFound} with empty source text)[/]";
            AnsiConsole.MarkupLine(footer);
        }
        else
            AnsiConsole.MarkupLine("[dim]Preview only — no files written. Run with --apply to write changes.[/]");
    }
}
