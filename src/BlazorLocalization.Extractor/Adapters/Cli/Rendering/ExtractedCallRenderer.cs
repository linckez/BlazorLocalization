using BlazorLocalization.Extractor.Adapters.Roslyn;
using BlazorLocalization.Extractor.Domain;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Adapters.Cli.Rendering;

/// <summary>
/// Renders <see cref="ScannedCallSite"/> results as a Spectre.Console table.
/// Opt-in section of the inspect command (<c>--show-extracted-calls</c>).
/// </summary>
internal static class ExtractedCallRenderer
{
    public static void Render(IReadOnlyList<ScannedCallSite> calls, string? projectName = null, PathStyle pathStyle = PathStyle.Relative)
    {
        var header = projectName is not null
            ? $"[blue]{Markup.Escape(projectName)}[/] [dim]— Extracted Calls ({calls.Count})[/]"
            : $"[blue]Extracted Calls[/] [dim]({calls.Count})[/]";
        AnsiConsole.Write(new Rule(header).LeftJustified());
        AnsiConsole.WriteLine();

        if (calls.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No IStringLocalizer usage detected.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Default)
            .AddColumn(new TableColumn("#").RightAligned())
            .AddColumn("Type.Method")
            .AddColumn("Kind")
            .AddColumn("Location")
            .AddColumn("Arguments");

        for (var i = 0; i < calls.Count; i++)
        {
            var call = calls[i];
            var method = $"{Markup.Escape(call.ContainingTypeName)}.{Markup.Escape(call.MethodName)}";
            var kind = call.CallKind.ToString();

            var location = call.File.DisplayMarkup(pathStyle, call.Line);

            var argsText = FormatArguments(call.Arguments);

            if (call.Chain is { Count: > 0 })
                argsText += "\n" + FormatChain(call.Chain);

            table.AddRow(
                $"[dim]{i + 1}[/]",
                $"[bold]{method}[/]",
                $"[dim]{Markup.Escape(kind)}[/]",
                location,
                argsText);
        }

        AnsiConsole.Write(table);
    }

    private static string FormatArguments(IReadOnlyList<ScannedArgument> args)
    {
        var parts = new List<string>();
        foreach (var arg in args)
        {
            var name = arg.ParameterName ?? $"arg{arg.Position}";
            var value = arg.Value.Display(40);
            parts.Add($"[dim]{Markup.Escape(name)}[/]=[bold]{Markup.Escape(value)}[/]");
        }
        return string.Join("\n", parts);
    }

    private static string FormatChain(IReadOnlyList<FluentChainWalker.ChainLink> chain)
    {
        var lines = new List<string>();
        foreach (var link in chain)
        {
            var argCount = link.Arguments.Count;
            var argText = argCount > 0 ? $"{argCount} arg(s)" : "";
            lines.Add($"[dim]  .{Markup.Escape(link.MethodName)}({Markup.Escape(argText)})[/]");
        }
        return string.Join("\n", lines);
    }
}
