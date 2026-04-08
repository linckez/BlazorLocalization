using BlazorLocalization.Extractor.Adapters.Export;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Adapters.Cli.Rendering;

/// <summary>
/// Renders PO format limitations as a Spectre.Console panel containing a table.
/// </summary>
public static class PoLimitationRenderer
{
    public static void Render(IReadOnlyList<PoLimitation> limitations, string projectName)
    {
        if (limitations.Count == 0)
            return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Default)
            .AddColumn("Key")
            .AddColumn("Limitation");

        foreach (var limitation in limitations)
            table.AddRow(
                $"[bold]{Markup.Escape(limitation.Key)}[/]",
                $"[dim]{Markup.Escape(limitation.Limitation)}[/]");

        var panel = new Panel(table)
            .Header($"[yellow]⚠ {limitations.Count} PO format limitation(s) in {Markup.Escape(projectName)}[/]")
            .BorderColor(Color.Yellow)
            .Padding(1, 0);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[dim]  💡 Consider i18next JSON or generic JSON for full plural fidelity.[/]");
    }
}
