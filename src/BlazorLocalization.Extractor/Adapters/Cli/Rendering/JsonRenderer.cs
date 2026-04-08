using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using BlazorLocalization.Extractor.Application;
using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Cli.Rendering;

/// <summary>
/// Renders scan results as JSON to stdout for piped/machine-readable output.
/// </summary>
internal static class JsonRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static void RenderInspect(
        string projectName,
        IReadOnlyList<MergedTranslation> entries,
        IReadOnlyList<KeyConflict> conflicts,
        IReadOnlyList<InvalidEntry> invalidEntries,
        HashSet<string>? localeFilter,
        PathStyle pathStyle = PathStyle.Relative)
    {
        var resxEntries = entries
            .Where(e => e.Definitions.Any(d => d.File.IsResx))
            .ToList();
        var allLocales = LocaleDiscovery.DiscoverLocales(resxEntries, localeFilter);
        var sourceKeys = resxEntries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            project = projectName,

            // 1. Translation Entries
            translationEntries = entries.Select(e => new
            {
                key = e.IsKeyLiteral ? e.Key : null,
                isDynamic = !e.IsKeyLiteral ? true : (bool?)null,
                usage = e.References
                    .Select(r => new { file = r.File.Display(pathStyle), line = r.Line })
                    .ToList() is { Count: > 0 } refs ? refs : null,
                form = TranslationFormExtensions.From(e.SourceText)?.ToString(),
                sourceText = MapSourceText(e.SourceText),
                source = GetSource(e, pathStyle),
                status = e.Status.ToString(),
                locales = e.InlineTranslations is { Count: > 0 }
                    ? e.InlineTranslations.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList()
                    : null
            }),

            // 2. Conflicts
            conflicts = conflicts.Count > 0 ? conflicts.Select(MapConflict) : null,

            // 3. .resx Files
            resxFiles = resxEntries.Count > 0 ? new
            {
                sourceEntries = resxEntries.Count,
                localeCount = allLocales.Count + 1,
                unreferenced = resxEntries.Count(e => e.Status == TranslationStatus.Review),
                locales = allLocales.Select(locale =>
                {
                    var count = resxEntries.Count(e => e.InlineTranslations?.ContainsKey(locale) == true);
                    var missing = resxEntries.Count - count;
                    var unique = resxEntries
                        .Where(e => e.InlineTranslations?.ContainsKey(locale) == true)
                        .Count(e => !sourceKeys.Contains(e.Key));
                    return new
                    {
                        locale,
                        entries = count,
                        coverage = resxEntries.Count > 0 ? Math.Round((double)count / resxEntries.Count * 100, 1) : 0,
                        missing = missing > 0 ? missing : (int?)null,
                        unique = unique > 0 ? unique : (int?)null
                    };
                })
            } : null,

            // 4. Invalid entries
            invalidEntries = invalidEntries.Count > 0 ? invalidEntries.Select(e => new
            {
                key = e.Key,
                reason = e.Reason,
                locations = e.Sites.Select(s => new { file = s.File.Display(pathStyle), line = s.Line })
            }) : null,

            // 5. Cross-reference
            crossReference = new
            {
                resolved = entries.Count(e => e.Status == TranslationStatus.Resolved),
                review = entries.Count(e => e.Status == TranslationStatus.Review),
                missing = entries.Count(e => e.Status == TranslationStatus.Missing),
                entries = entries.Select(e => new
                {
                    key = e.Key,
                    status = e.Status.ToString()
                })
            }
        }, Options));
    }

    public static void RenderExtract(
        string projectName,
        IReadOnlyList<MergedTranslation> entries,
        IReadOnlyList<KeyConflict> conflicts)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            project = projectName,
            entries = entries.Select(MapEntry),
            conflicts = conflicts.Select(MapConflict)
        }, Options));
    }

    private static object MapEntry(MergedTranslation entry) => new
    {
        key = entry.Key,
        sourceText = MapSourceText(entry.SourceText),
        isKeyLiteral = entry.IsKeyLiteral,
        inlineTranslations = entry.InlineTranslations?.ToDictionary(
            kvp => kvp.Key,
            kvp => MapSourceText(kvp.Value)),
        definitions = entry.Definitions.Select(d => new
        {
            filePath = d.File.Display(PathStyle.Relative),
            line = d.Line,
            projectName = d.File.ProjectName
        }),
        references = entry.References.Select(r => new
        {
            filePath = r.File.Display(PathStyle.Relative),
            line = r.Line,
            projectName = r.File.ProjectName
        })
    };

    private static object? MapSourceText(TranslationSourceText? text) => text switch
    {
        SingularText s => new { type = "singular", value = s.Value },
        PluralText p => new
        {
            type = "plural",
            other = p.Other,
            zero = p.Zero,
            one = p.One,
            two = p.Two,
            few = p.Few,
            many = p.Many,
            exactMatches = p.ExactMatches?.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
            isOrdinal = p.IsOrdinal ? true : (bool?)null
        },
        SelectText s => new
        {
            type = "select",
            cases = s.Cases,
            otherwise = s.Otherwise
        },
        SelectPluralText sp => new
        {
            type = "selectPlural",
            cases = sp.Cases.ToDictionary(kvp => kvp.Key, kvp => MapSourceText(kvp.Value)),
            otherwise = sp.Otherwise is not null ? MapSourceText(sp.Otherwise) : null
        },
        _ => null
    };

    private static object MapConflict(KeyConflict conflict) => new
    {
        key = conflict.Key,
        values = conflict.Values.Select(v => new
        {
            sourceText = MapSourceText(v.SourceText),
            sites = v.Sites.Select(s => new
            {
                filePath = s.File.Display(PathStyle.Relative),
                line = s.Line
            })
        })
    };

    private static object? GetSource(MergedTranslation entry, PathStyle pathStyle)
    {
        if (entry.Definitions.Count == 0) return null;

        var def = entry.Definitions[0];
        var kind = def.Kind switch
        {
            DefinitionKind.InlineTranslation => ".Translation()",
            DefinitionKind.ReusableDefinition => def.Context?.Split('.').LastOrDefault() is { } name ? $"{name}()" : "Definition()",
            DefinitionKind.EnumAttribute => "[Translation]",
            DefinitionKind.ResourceFile => def.File.FileName,
            _ => null
        };

        return new
        {
            kind,
            file = def.File.Display(pathStyle),
            line = def.Line
        };
    }
}
