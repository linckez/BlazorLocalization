using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Cli.Rendering;

/// <summary>
/// Renders <see cref="KeyConflict"/> results as a Spectre.Console panel containing a table.
/// </summary>
public static class ConflictRenderer
{
	public static void Render(IReadOnlyList<KeyConflict> conflicts, string projectName)
	{
		if (conflicts.Count == 0)
			return;

		var table = new Table()
			.Border(TableBorder.Rounded)
			.AddColumn("Key")
			.AddColumn("Value")
			.AddColumn("Locations");

		foreach (var conflict in conflicts)
		{
			for (var i = 0; i < conflict.Values.Count; i++)
			{
				var value = conflict.Values[i];
				var locations = string.Join(", ", value.Sources.Select(FormatLocation));

				table.AddRow(
					i == 0 ? $"[white]{Markup.Escape(conflict.Key)}[/]" : "",
					$"[dim]\"{Markup.Escape(FormatSourceText(value.SourceText))}\"[/]",
					locations);
			}

			table.AddEmptyRow();
		}

		var panel = new Panel(table)
			.Header($"[yellow]⚠ {conflicts.Count} conflicting key(s) in {Markup.Escape(projectName)}[/]")
			.BorderColor(Color.Yellow)
			.Padding(1, 0);

		AnsiConsole.WriteLine();
		AnsiConsole.Write(panel);
	}

	private static string FormatSourceText(TranslationSourceText text) => text switch
	{
		SingularText s => s.Value,
		PluralText p => string.Join(" | ",
			new[] { ("zero", p.Zero), ("one", p.One), ("two", p.Two), ("few", p.Few), ("many", p.Many), ("other", (string?)p.Other) }
				.Where(t => t.Item2 is not null)
				.Select(t => $"{t.Item1}: {t.Item2}")),
		SelectText s => string.Join(" | ", s.Cases.Select(c => $"{c.Key}: {c.Value}")),
		_ => "(unknown)"
	};

	private static string FormatLocation(SourceReference source)
	{
		var file = Path.GetFileName(source.FilePath);
		return $"[cyan]{Markup.Escape(file)}:{source.Line}[/]";
	}
}
