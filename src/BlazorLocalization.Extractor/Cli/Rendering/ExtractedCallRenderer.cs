using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Cli.Rendering;

/// <summary>
/// Renders <see cref="ExtractedCall"/> results as a Spectre.Console table.
/// </summary>
public static class ExtractedCallRenderer
{
	public static void Render(IReadOnlyList<ExtractedCall> calls, string? projectName = null)
	{
		var header = projectName is not null
			? $"[blue]{Markup.Escape(projectName)}[/] [grey]— Extracted Calls ({calls.Count})[/]"
			: $"[blue]Extracted Calls[/] [grey]({calls.Count})[/]";
		AnsiConsole.Write(new Rule(header).LeftJustified());
		AnsiConsole.WriteLine();

		if (calls.Count == 0)
		{
			AnsiConsole.MarkupLine("[grey]No IStringLocalizer usage detected.[/]");
			return;
		}

		var table = new Table()
			.Border(TableBorder.Rounded)
			.AddColumn(new TableColumn("#").RightAligned())
			.AddColumn("Type.Method")
			.AddColumn("Kind")
			.AddColumn("Location")
			.AddColumn("Overload")
			.AddColumn("Arguments");

		for (var i = 0; i < calls.Count; i++)
		{
			var call = calls[i];
			var method = $"{Markup.Escape(call.ContainingTypeName)}.{Markup.Escape(call.MethodName)}";
			var kind = call.CallKind.ToString();
			var file = Path.GetFileName(call.Location.FilePath);
			var location = $"{Markup.Escape(file)}:{call.Location.Line}";

			var (statusMarkup, statusText) = call.OverloadResolution switch
			{
				OverloadResolutionStatus.Resolved => ("[green]", "Resolved"),
				OverloadResolutionStatus.Ambiguous => ("[yellow]", "Ambiguous"),
				OverloadResolutionStatus.BestCandidate => ("[red]", "Best-candidate"),
				_ => ("[grey]", "Unknown")
			};

			var argsText = FormatArguments(call.Arguments);

			if (call.FluentChain is { Count: > 0 })
				argsText += "\n" + FormatChain(call.FluentChain);

			table.AddRow(
				$"[grey]{i + 1}[/]",
				$"[white]{method}[/]",
				$"[grey]{Markup.Escape(kind)}[/]",
				$"[cyan]{location}[/]",
				$"{statusMarkup}{Markup.Escape(statusText)}[/]",
				argsText);
		}

		AnsiConsole.Write(table);
	}

	private static string FormatArguments(IReadOnlyList<ResolvedArgument> args)
	{
		var parts = new List<string>();
		foreach (var arg in args)
		{
			var name = arg.ParameterName ?? $"arg{arg.Position}";
			var value = arg.Value.Length > 40 ? arg.Value[..37] + "..." : arg.Value;
			parts.Add($"[grey]{Markup.Escape(name)}[/]=[white]\"{Markup.Escape(value)}\"[/]");

			if (arg.ObjectCreation is { } oc)
			{
				var typeName = oc.TypeName.Contains('.') ? oc.TypeName[(oc.TypeName.LastIndexOf('.') + 1)..] : oc.TypeName;
				var ctorArgs = string.Join(", ", oc.ConstructorArguments.Select(a =>
				{
					var v = a.Value.Length > 30 ? a.Value[..27] + "..." : a.Value;
					return $"\"{Markup.Escape(v)}\"";
				}));
				parts.Add($"  [dim]→ new {Markup.Escape(typeName)}({ctorArgs})[/]");
			}
		}

		return string.Join("\n", parts);
	}

	private static string FormatChain(IReadOnlyList<ChainedMethodCall> chain)
	{
		var lines = new List<string>();
		foreach (var call in chain)
		{
			var args = call.Arguments.Count > 0
				? string.Join(", ", call.Arguments.Select(a =>
				{
					var v = a.Value.Length > 30 ? a.Value[..27] + "..." : a.Value;
					return a.IsLiteral ? $"\"{Markup.Escape(v)}\"" : Markup.Escape(v);
				}))
				: "";
			lines.Add($"[dim]  .{Markup.Escape(call.MethodName)}({args})[/]");
		}
		return string.Join("\n", lines);
	}
}
