using System.Globalization;
using BlazorLocalization.Extractor.Application;
using BlazorLocalization.Extractor.Domain;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Adapters.Cli.Rendering;

/// <summary>
/// Renders inspect output: translation entries, .resx overview, anomalies, warnings, and legend.
/// </summary>
public static class TranslationEntryRenderer
{
    // ── Translation Entries table ──

    public static void Render(
        IReadOnlyList<MergedTranslation> entries,
        string? projectName = null,
        PathStyle pathStyle = PathStyle.Relative)
    {
        AnsiConsole.WriteLine();
        var header = projectName is not null
            ? $"[blue]{Markup.Escape(projectName)}[/] [dim]— Translation Entries ({entries.Count})[/]"
            : $"[blue]Translation Entries[/] [dim]({entries.Count})[/]";
        AnsiConsole.Write(new Rule(header).LeftJustified());
        AnsiConsole.WriteLine();

        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No translation entries found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Default)
            .ShowRowSeparators()
            .AddColumn("Key")
            .AddColumn("Usage")
            .AddColumn("Form")
            .AddColumn("Source Text")
            .AddColumn("Source")
            .AddColumn("Status")
            .AddColumn("Locales");

        foreach (var entry in entries)
        {
            var key = entry.IsKeyLiteral
                ? $"[bold]{Markup.Escape(entry.Key)}[/]"
                : $"[dim]≈ {Markup.Escape(entry.Key)}[/]";

            var usage = FormatUsage(entry, pathStyle);
            var form = FormatForm(entry.SourceText);
            var sourceText = FormatSourceText(entry.SourceText);
            var source = FormatSource(entry, pathStyle);
            var status = FormatStatus(entry);
            var locales = FormatLocales(entry);

            table.AddRow(key, usage, form, sourceText, source, status, locales);
        }

        AnsiConsole.Write(table);
    }

    // ── Conflicts ──

    public static void RenderConflicts(IReadOnlyList<KeyConflict> conflicts, PathStyle pathStyle = PathStyle.Relative)
    {
        if (conflicts.Count == 0) return;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[yellow]⚠ Conflicts ({conflicts.Count})[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .AddColumn("Key")
            .AddColumn("Source Text")
            .AddColumn("Locations");

        foreach (var conflict in conflicts)
        {
            for (var i = 0; i < conflict.Values.Count; i++)
            {
                var value = conflict.Values[i];
                var locations = string.Join(", ", value.Sites.Select(s =>
                    s.DisplayMarkup(pathStyle)));

                table.AddRow(
                    i == 0 ? $"[bold]{Markup.Escape(conflict.Key)}[/]" : "",
                    $"[dim]\"{Markup.Escape(FormatSourceTextBrief(value.SourceText))}\"[/]",
                    locations);
            }

            table.AddEmptyRow();
        }

        AnsiConsole.Write(table);
    }

    // ── Invalid Entries ──

    public static void RenderInvalidEntries(IReadOnlyList<InvalidEntry> invalidEntries, PathStyle pathStyle = PathStyle.Relative)
    {
        if (invalidEntries.Count == 0) return;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[red]✗ Invalid Entries ({invalidEntries.Count})[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Red)
            .AddColumn("Location")
            .AddColumn("Reason");

        foreach (var entry in invalidEntries)
        {
            var locations = string.Join(", ", entry.Sites.Select(s =>
                s.DisplayMarkup(pathStyle)));
            table.AddRow(locations, $"[red]{Markup.Escape(entry.Reason)}[/]");
        }

        AnsiConsole.Write(table);
    }

    // ── .resx Overview ──

    public static void RenderResxOverview(
        IReadOnlyList<MergedTranslation> entries,
        HashSet<string>? localeFilter)
    {
        var resxEntries = entries
            .Where(e => e.Definitions.Any(d => d.File.IsResx))
            .ToList();

        if (resxEntries.Count == 0) return;

        var allLocales = LocaleDiscovery.DiscoverLocales(resxEntries, localeFilter);
        var sourceKeyCount = resxEntries.Count;
        var unreferencedCount = resxEntries.Count(e => e.Status == TranslationStatus.Review);

        AnsiConsole.WriteLine();
        var localeCount = allLocales.Count + 1; // +1 for source
        AnsiConsole.Write(new Rule($"[blue].resx Files:[/] [dim]{sourceKeyCount} entries across {localeCount} locales[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Default)
            .AddColumn("Locale")
            .AddColumn(new TableColumn("Entries").RightAligned())
            .AddColumn("Coverage")
            .AddColumn(new TableColumn("Unique").RightAligned())
            .AddColumn(new TableColumn("Unreferenced").RightAligned())
            .AddColumn("Status");

        // Source locale row
        table.AddRow(
            "[bold](source)[/]",
            sourceKeyCount.ToString(),
            "[dim]—[/]",
            "[dim]—[/]",
            unreferencedCount > 0 ? $"[yellow]{unreferencedCount}[/]" : "[dim]—[/]",
            "[dim]Source locale[/]");

        // Per-locale rows
        foreach (var locale in allLocales)
        {
            var count = resxEntries.Count(e => e.InlineTranslations?.ContainsKey(locale) == true);
            var coverage = sourceKeyCount > 0 ? (double)count / sourceKeyCount * 100 : 0;
            var missing = sourceKeyCount - count;

            var coverageStr = $"{coverage:F1}%";
            var coverageColor = coverage >= 100 ? "green" : coverage >= 90 ? "yellow" : "red";

            var statusParts = new List<string>();
            if (missing > 0) statusParts.Add($"{missing} missing");
            var status = statusParts.Count > 0 ? string.Join(", ", statusParts) : "OK";
            var statusColor = status == "OK" ? "green" : "yellow";

            table.AddRow(
                $"[bold]{Markup.Escape(GetLocaleDisplayName(locale))}[/]",
                count.ToString(),
                $"[{coverageColor}]{coverageStr}[/]",
                "[dim]0[/]",
                "[dim]—[/]",
                $"[{statusColor}]{Markup.Escape(status)}[/]");
        }

        AnsiConsole.Write(table);
    }

    // ── Anomalies ──

    public static void RenderAnomalies(
        IReadOnlyList<MergedTranslation> entries,
        HashSet<string>? localeFilter)
    {
        // Find resx-sourced entries where a locale exists but source key might be orphaned
        var resxEntries = entries
            .Where(e => e.Definitions.Any(d => d.File.IsResx))
            .ToList();

        if (resxEntries.Count == 0) return;

        var sourceKeys = resxEntries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allLocales = LocaleDiscovery.DiscoverLocales(resxEntries, localeFilter);

        var anomalies = new List<(string Locale, string Key, string File)>();
        foreach (var locale in allLocales)
        {
            foreach (var entry in resxEntries)
            {
                if (entry.InlineTranslations?.ContainsKey(locale) != true) continue;
                if (sourceKeys.Contains(entry.Key)) continue;

                var file = entry.Definitions
                    .FirstOrDefault(d => d.File.IsResx)
                    ?.File.FileName ?? "—";
                anomalies.Add((locale, entry.Key, file));
            }
        }

        if (anomalies.Count == 0) return;

        var localeCount = anomalies.Select(a => a.Locale).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[yellow]⚠ Anomalies ({anomalies.Count} unique keys across {localeCount} locales)[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .AddColumn("Locale")
            .AddColumn("Key")
            .AddColumn("File");

        foreach (var (locale, key, file) in anomalies)
            table.AddRow(
                $"[bold]{Markup.Escape(locale)}[/]",
                $"[bold]{Markup.Escape(key)}[/]",
                $"[cyan]{Markup.Escape(file)}[/]");

        AnsiConsole.Write(table);
    }

    // ── Per-locale RESX tables (--show-resx-locales) ──

    public static void RenderResxLocales(
        IReadOnlyList<MergedTranslation> entries,
        HashSet<string>? localeFilter)
    {
        var resxEntries = entries
            .Where(e => e.Definitions.Any(d => d.File.IsResx))
            .ToList();

        if (resxEntries.Count == 0) return;

        var unreferencedCount = resxEntries.Count(e => e.Status == TranslationStatus.Review);

        // Source locale table
        AnsiConsole.WriteLine();
        var sourceHeader = $"[blue].resx Files (source locale)[/] [dim]— {resxEntries.Count} entries";
        if (unreferencedCount > 0) sourceHeader += $", {unreferencedCount} unreferenced";
        sourceHeader += "[/]";
        AnsiConsole.Write(new Rule(sourceHeader).LeftJustified());
        AnsiConsole.WriteLine();

        var sourceTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Default)
            .AddColumn("Key")
            .AddColumn("Value")
            .AddColumn("File")
            .AddColumn("Status");

        foreach (var entry in resxEntries)
        {
            var value = FormatSourceTextBrief(entry.SourceText);
            var file = entry.Definitions
                .FirstOrDefault(d => d.File.IsResx)
                ?.File.FileName ?? "—";

            var status = entry.Status == TranslationStatus.Review
                ? "[yellow]Review[/]"
                : "[green]Resolved[/]";

            sourceTable.AddRow(
                $"[bold]{Markup.Escape(entry.Key)}[/]",
                $"[dim]{Markup.Escape(value)}[/]",
                $"[cyan]{Markup.Escape(file)}[/]",
                status);
        }

        AnsiConsole.Write(sourceTable);

        // Per non-source locale tables
        var allLocales = LocaleDiscovery.DiscoverLocales(resxEntries, localeFilter);
        var sourceKeys = resxEntries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var locale in allLocales)
        {
            var localeEntries = resxEntries
                .Where(e => e.InlineTranslations?.ContainsKey(locale) == true)
                .ToList();

            if (localeEntries.Count == 0) continue;

            var missing = resxEntries.Count - localeEntries.Count;
            var uniqueCount = localeEntries.Count(e => !sourceKeys.Contains(e.Key));

            var displayName = GetLocaleDisplayName(locale);
            var localeHeader = $"[blue].resx Files ({Markup.Escape(displayName)})[/] [dim]— {localeEntries.Count} entries";
            if (missing > 0) localeHeader += $", {missing} missing";
            if (uniqueCount > 0) localeHeader += $", {uniqueCount} unique";
            localeHeader += "[/]";

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule(localeHeader).LeftJustified());
            AnsiConsole.WriteLine();

            var localeTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Default)
                .AddColumn("Key")
                .AddColumn("Value")
                .AddColumn("File")
                .AddColumn("Status");

            foreach (var entry in localeEntries)
            {
                var localeText = entry.InlineTranslations![locale];
                var value = FormatSourceTextBrief(localeText);
                var file = entry.Definitions
                    .FirstOrDefault(d => d.File.IsResx)
                    ?.File.FileName ?? "—";

                var isUnique = !sourceKeys.Contains(entry.Key);
                var status = isUnique ? "[yellow]Unique[/]" : "[green]Resolved[/]";

                localeTable.AddRow(
                    $"[bold]{Markup.Escape(entry.Key)}[/]",
                    $"[dim]{Markup.Escape(value)}[/]",
                    $"[cyan]{Markup.Escape(file)}[/]",
                    status);
            }

            AnsiConsole.Write(localeTable);
        }
    }

    // ── Cross-Reference Summary ──

    public static void RenderCrossReferenceSummary(IReadOnlyList<MergedTranslation> entries, string? projectName = null)
    {
        AnsiConsole.WriteLine();

        var resolved = entries.Count(e => e.Status == TranslationStatus.Resolved);
        var review = entries.Count(e => e.Status == TranslationStatus.Review);
        var missing = entries.Count(e => e.Status == TranslationStatus.Missing);

        var parts = new List<string> { $"[green]{resolved} resolved[/]" };
        if (review > 0) parts.Add($"[yellow]{review} review[/]");
        if (missing > 0) parts.Add($"[red]{missing} missing[/]");

        AnsiConsole.MarkupLine($"  Cross-reference: {string.Join(", ", parts)}");
    }

    // ── Legend ──

    public static void RenderLegend(
        IReadOnlyList<MergedTranslation> entries,
        bool hasResxFiles,
        bool showedExtractedCalls,
        bool hasInvalidEntries = false)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[dim]Legend[/]").LeftJustified());
        AnsiConsole.WriteLine();

        // Form — only show forms that actually appear
        var forms = entries.Select(e => TranslationFormExtensions.From(e.SourceText)).ToHashSet();
        AnsiConsole.MarkupLine("  [dim]Form[/]");
        if (forms.Contains(TranslationForm.Simple))
            AnsiConsole.MarkupLine("    [bold]Simple[/]              Single message");
        if (forms.Contains(TranslationForm.Plural))
            AnsiConsole.MarkupLine("    [bold]Plural[/]              Branching by count (.One/.Other/...)");
        if (forms.Contains(TranslationForm.Ordinal))
            AnsiConsole.MarkupLine("    [bold]Ordinal[/]             Branching by ordinal rank (1st, 2nd, ...)");
        if (forms.Contains(TranslationForm.Select))
            AnsiConsole.MarkupLine("    [bold]Select[/]              Branching by category (.When/.Otherwise)");
        if (forms.Contains(TranslationForm.SelectPlural))
            AnsiConsole.MarkupLine("    [bold]Select+Plural[/]       Combined category + count branching");

        // Status — only show states that actually appear
        var hasMissing = entries.Any(e => e.Status == TranslationStatus.Missing);
        var hasReview = entries.Any(e => e.Status == TranslationStatus.Review);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]Status[/]");
        AnsiConsole.MarkupLine("    [green]Resolved[/]            Fully verified — source text and code usage found");
        if (hasReview)
            AnsiConsole.MarkupLine("    [yellow]Review[/]              Needs manual check\n                            [dim]— = definition found but no usage detected (possibly unused)\n                            ≈ = dynamic key expression (actual key determined at runtime)[/]");
        if (hasMissing)
            AnsiConsole.MarkupLine("    [red]Missing[/]             Code references this key but no source text found");

        // Source — only show types that actually appear
        var kinds = entries.SelectMany(e => e.Definitions).Select(d => d.Kind).ToHashSet();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]Source[/]");
        if (kinds.Contains(DefinitionKind.InlineTranslation))
            AnsiConsole.MarkupLine("    [bold].Translation()[/]      Source text defined inline in code");
        if (kinds.Contains(DefinitionKind.ReusableDefinition))
            AnsiConsole.MarkupLine("    [bold]DefineXxx()[/]         Reusable definition (DefineSimple, DefinePlural, ...)");
        if (kinds.Contains(DefinitionKind.EnumAttribute))
            AnsiConsole.MarkupLine("    [bold][[Translation]][/]       Enum member attribute");
        if (kinds.Contains(DefinitionKind.ResourceFile))
            AnsiConsole.MarkupLine("    [bold]<file>.resx[/]         .resx resource file");

        // .resx Files — only when RESX files were found
        if (hasResxFiles)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [dim].resx Files[/]");
            AnsiConsole.MarkupLine("    [bold]Coverage[/]            % of source locale keys present in this locale");
            AnsiConsole.MarkupLine("    [bold]Unique[/]              Keys in this locale that don't exist in the source locale");
            AnsiConsole.MarkupLine("    [bold]Unreferenced[/]        Source .resx keys with no usage found in your code");
        }

        // Extraction — only when the extracted calls table was shown
        if (showedExtractedCalls)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [dim]Extraction[/]");
            AnsiConsole.MarkupLine("    [green]Detected[/]            blazor-loc detected and classified the call");
        }

        // Invalid — only when invalid entries were detected
        if (hasInvalidEntries)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [dim]Invalid[/]");
            AnsiConsole.MarkupLine("    [red]Empty key[/]            Empty Localizer indexer or .Translate(key:\"\") — compiles but does nothing");
        }
    }

    // ── Formatting helpers ──

    private static string FormatUsage(MergedTranslation entry, PathStyle pathStyle)
    {
        var lines = entry.References
            .Select(r => r.DisplayMarkup(pathStyle))
            .ToList();

        return lines.Count > 0 ? string.Join("\n", lines) : "[dim]—[/]";
    }

    private static string FormatForm(TranslationSourceText? sourceText)
    {
        var form = TranslationFormExtensions.From(sourceText);
        return form switch
        {
            TranslationForm.Simple => "[green]Simple[/]",
            TranslationForm.Plural => "[blue]Plural[/]",
            TranslationForm.Ordinal => "[blue]Ordinal[/]",
            TranslationForm.Select => "[magenta]Select[/]",
            TranslationForm.SelectPlural => "[magenta]Select+Plural[/]",
            null => "[dim]—[/]",
            _ => "[dim]?[/]"
        };
    }

    private static string FormatSource(MergedTranslation entry, PathStyle pathStyle)
    {
        if (entry.Definitions.Count == 0) return "[dim]—[/]";

        var def = entry.Definitions[0]; // first-seen wins (matches SourceText resolution)
        var label = def.Kind switch
        {
            DefinitionKind.InlineTranslation => "[dim].Translation()[/]",
            DefinitionKind.ReusableDefinition => $"[dim]{Markup.Escape(FormatDefinitionLabel(def.Context))}[/]",
            DefinitionKind.EnumAttribute => "[dim][[Translation]][/]",
            DefinitionKind.ResourceFile => $"[dim]{Markup.Escape(def.File.FileName)}[/]",
            _ => "[dim]—[/]"
        };

        // ResourceFile: the filename IS the location — no need for a second line
        if (def.Kind == DefinitionKind.ResourceFile)
            return label;

        return $"{label}\n  {def.DisplayMarkup(pathStyle)}";
    }

    private static string FormatDefinitionLabel(string? context)
    {
        // Context is e.g. "TranslationDefinitions.DefineSimple" — extract "DefineSimple()"
        if (context is null) return "Definition()";
        var dot = context.LastIndexOf('.');
        var name = dot >= 0 ? context[(dot + 1)..] : context;
        return $"{name}()";
    }

    private static string FormatStatus(MergedTranslation entry) =>
        entry.Status switch
        {
            TranslationStatus.Review => "[yellow]Review[/]",
            TranslationStatus.Missing => "[red]Missing[/]",
            _ => "[green]Resolved[/]"
        };

    private static string FormatLocales(MergedTranslation entry)
    {
        if (entry.InlineTranslations is not { Count: > 0 })
            return "[dim]—[/]";

        return string.Join(", ", entry.InlineTranslations.Keys.Order(StringComparer.OrdinalIgnoreCase));
    }

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
                return "[dim]—[/]";

            default:
                return "[dim]?[/]";
        }
    }

    private static string FormatSourceTextBrief(TranslationSourceText? text) =>
        text switch
        {
            SingularText s => s.Value,
            PluralText p => p.One ?? p.Other,
            SelectText s => s.Otherwise ?? s.Cases.Values.FirstOrDefault() ?? "—",
            SelectPluralText sp => sp.Otherwise?.One ?? sp.Otherwise?.Other ?? "—",
            null => "—",
            _ => "?"
        };

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

    private static string GetLocaleDisplayName(string locale)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(locale, predefinedOnly: true);
            return culture.EnglishName != locale ? locale : locale;
        }
        catch
        {
            return locale;
        }
    }
}
