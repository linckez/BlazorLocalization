using System.Globalization;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Cli.Rendering;

/// <summary>
/// Renders <see cref="MergedTranslationEntry"/> results as a Spectre.Console table.
/// </summary>
public static class TranslationEntryRenderer
{
	public static void Render(IReadOnlyList<MergedTranslationEntry> entries, string? projectName = null)
	{
		AnsiConsole.WriteLine();
		var header = projectName is not null
			? $"[blue]{Markup.Escape(projectName)}[/] [grey]— Translation Entries ({entries.Count})[/]"
			: $"[blue]Translation Entries[/] [grey]({entries.Count})[/]";
		AnsiConsole.Write(new Rule(header).LeftJustified());
		AnsiConsole.WriteLine();

		if (entries.Count == 0)
		{
			AnsiConsole.MarkupLine("[grey]No translation entries found.[/]");
			return;
		}

		var table = new Table()
			.Border(TableBorder.Rounded)
			.AddColumn("Key")
			.AddColumn("Type")
			.AddColumn("Text")
			.AddColumn("Locales")
			.AddColumn("Sources");

		foreach (var entry in entries)
		{
			var (typeMarkup, typeLabel) = GetTypeLabel(entry.SourceText);
			var text = FormatSourceText(entry.SourceText);

			var locales = entry.InlineTranslations is { Count: > 0 }
				? string.Join(", ", entry.InlineTranslations.Keys.Order(StringComparer.OrdinalIgnoreCase))
				: "[grey]\u2014[/]";

			var sources = string.Join("\n", entry.Sources.Select(s =>
			{
				var file = Path.GetFileName(s.FilePath);
				var line = $"[cyan]{Markup.Escape(file)}:{s.Line}[/]";
				return s.Context is not null
					? $"{line} [dim]({Markup.Escape(s.Context)})[/]"
					: line;
			}));

			table.AddRow(
				$"[white]{Markup.Escape(entry.Key)}[/]",
				$"{typeMarkup}{typeLabel}[/]",
				text,
				locales,
				sources);
		}

		AnsiConsole.Write(table);
	}

	/// <summary>
	/// Renders a locale coverage summary showing how many keys have <c>.For()</c> translations per locale.
	/// </summary>
	public static void RenderLocaleSummary(
		IReadOnlyList<MergedTranslationEntry> entries,
		HashSet<string>? localeFilter)
	{
		var allLocales = DiscoverLocales(entries, localeFilter);
		if (allLocales.Count == 0) return;

		var totalKeys = entries.Count;

		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Rule("[blue]Locale Coverage[/]").LeftJustified());
		AnsiConsole.WriteLine();

		AnsiConsole.MarkupLine($"  [white]Source:[/] [grey]{totalKeys} keys[/]");

		foreach (var locale in allLocales)
		{
			var count = entries.Count(e => e.InlineTranslations?.ContainsKey(locale) == true);
			var displayName = GetLocaleDisplayName(locale);
			var color = count == totalKeys ? "green" : "yellow";
			AnsiConsole.MarkupLine($"  [{color}]{Markup.Escape(displayName)}:[/] [grey]{count} of {totalKeys} keys[/]");
		}
	}

	/// <summary>
	/// Renders one table per discovered locale showing actual <c>.For()</c> translated text.
	/// </summary>
	public static void RenderLocales(
		IReadOnlyList<MergedTranslationEntry> entries,
		HashSet<string>? localeFilter)
	{
		var allLocales = DiscoverLocales(entries, localeFilter);

		foreach (var locale in allLocales)
		{
			var localeEntries = entries
				.Where(e => e.InlineTranslations?.ContainsKey(locale) == true)
				.ToList();

			if (localeEntries.Count == 0) continue;

			var displayName = GetLocaleDisplayName(locale);
			var header = $"[blue]{Markup.Escape(displayName)}[/] [grey]— {localeEntries.Count} of {entries.Count} keys[/]";

			AnsiConsole.WriteLine();
			AnsiConsole.Write(new Rule(header).LeftJustified());
			AnsiConsole.WriteLine();

			var table = new Table()
				.Border(TableBorder.Rounded)
				.AddColumn("Key")
				.AddColumn("Type")
				.AddColumn("Text");

			foreach (var entry in localeEntries)
			{
				var localeText = entry.InlineTranslations![locale];
				var (typeMarkup, typeLabel) = GetTypeLabel(localeText);
				var text = FormatSourceText(localeText);

				table.AddRow(
					$"[white]{Markup.Escape(entry.Key)}[/]",
					$"{typeMarkup}{typeLabel}[/]",
					text);
			}

			AnsiConsole.Write(table);
		}
	}

	private static (string Markup, string Label) GetTypeLabel(TranslationSourceText? sourceText) =>
		sourceText switch
		{
			SingularText => ("[green]", "Singular"),
			PluralText p => ("[blue]", p.IsOrdinal ? "Ordinal" : "Plural"),
			SelectText => ("[magenta]", "Select"),
			SelectPluralText => ("[magenta]", "Select+Plural"),
			null => ("[grey]", "Key-only"),
			_ => ("[grey]", "Unknown")
		};

	private static string FormatSourceText(TranslationSourceText? text)
	{
		switch (text)
		{
			case SingularText s:
				return $"\"{Markup.Escape(s.Value)}\"";

			case PluralText p:
				var parts = new List<string>();
				if (p.Zero is not null)  parts.Add($"zero: \"{Markup.Escape(p.Zero)}\"");
				if (p.One is not null)   parts.Add($"one: \"{Markup.Escape(p.One)}\"");
				if (p.Two is not null)   parts.Add($"two: \"{Markup.Escape(p.Two)}\"");
				if (p.Few is not null)   parts.Add($"few: \"{Markup.Escape(p.Few)}\"");
				if (p.Many is not null)  parts.Add($"many: \"{Markup.Escape(p.Many)}\"");
				parts.Add($"other: \"{Markup.Escape(p.Other)}\"");
				if (p.ExactMatches is { Count: > 0 })
					foreach (var (v, m) in p.ExactMatches)
						parts.Add($"exactly({v}): \"{Markup.Escape(m)}\"");
				return string.Join("\n", parts);

			case SelectText s:
				var selectParts = s.Cases.Select(c => $"{Markup.Escape(c.Key)}: \"{Markup.Escape(c.Value)}\"").ToList();
				if (s.Otherwise is not null)
					selectParts.Add($"otherwise: \"{Markup.Escape(s.Otherwise)}\"");
				return string.Join("\n", selectParts);

			case SelectPluralText sp:
				var spParts = new List<string>();
				foreach (var (caseValue, plural) in sp.Cases)
				{
					spParts.Add($"[magenta]{Markup.Escape(caseValue)}:[/]");
					spParts.Add(FormatPluralInline(plural));
				}
				if (sp.Otherwise is not null)
				{
					spParts.Add("[magenta]otherwise:[/]");
					spParts.Add(FormatPluralInline(sp.Otherwise));
				}
				return string.Join("\n", spParts);

			case null:
				return "[grey]\u2014[/]";

			default:
				return "[grey]?[/]";
		}
	}

	private static string FormatPluralInline(PluralText p)
	{
		var parts = new List<string>();
		if (p.Zero is not null)  parts.Add($"  zero: \"{Markup.Escape(p.Zero)}\"");
		if (p.One is not null)   parts.Add($"  one: \"{Markup.Escape(p.One)}\"");
		if (p.Two is not null)   parts.Add($"  two: \"{Markup.Escape(p.Two)}\"");
		if (p.Few is not null)   parts.Add($"  few: \"{Markup.Escape(p.Few)}\"");
		if (p.Many is not null)  parts.Add($"  many: \"{Markup.Escape(p.Many)}\"");
		parts.Add($"  other: \"{Markup.Escape(p.Other)}\"");
		if (p.ExactMatches is { Count: > 0 })
			foreach (var (v, m) in p.ExactMatches)
				parts.Add($"  exactly({v}): \"{Markup.Escape(m)}\"");
		return string.Join("\n", parts);
	}

	private static IReadOnlyList<string> DiscoverLocales(
		IReadOnlyList<MergedTranslationEntry> entries,
		HashSet<string>? localeFilter)
	{
		var locales = entries
			.Where(e => e.InlineTranslations is not null)
			.SelectMany(e => e.InlineTranslations!.Keys)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Order(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (localeFilter is not null)
			locales = locales.Where(l => localeFilter.Contains(l)).ToList();

		return locales;
	}

	private static string GetLocaleDisplayName(string locale)
	{
		try
		{
			var culture = CultureInfo.GetCultureInfo(locale);
			return culture.EnglishName != locale ? $"{culture.EnglishName} ({locale})" : locale;
		}
		catch (CultureNotFoundException)
		{
			return locale;
		}
	}
}
