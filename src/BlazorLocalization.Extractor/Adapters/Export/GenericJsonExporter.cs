using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Export;

/// <summary>
/// Exports <see cref="MergedTranslation"/> records as a JSON array — a 1:1 serialization
/// of the domain model, useful for debugging and downstream tooling that needs full fidelity.
/// </summary>
internal sealed class GenericJsonExporter : ITranslationExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string Export(IReadOnlyList<MergedTranslation> entries, PathStyle pathStyle)
    {
        var dto = entries.Select(e => ToDto(e, pathStyle)).ToList();
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static EntryDto ToDto(MergedTranslation entry, PathStyle pathStyle) => new()
    {
        Key = entry.Key,
        SourceText = MapSourceText(entry.SourceText),
        InlineTranslations = entry.InlineTranslations?.ToDictionary(
            kvp => kvp.Key,
            kvp => MapSourceText(kvp.Value)),
        Definitions = entry.Definitions.Select(d => new DefinitionDto
        {
            FilePath = d.File.Display(pathStyle),
            Line = d.Line,
            ProjectName = d.File.ProjectName,
            Context = d.Context
        }).ToList(),
        References = entry.References.Select(r => new ReferenceDto
        {
            FilePath = r.File.Display(pathStyle),
            Line = r.Line,
            ProjectName = r.File.ProjectName
        }).ToList()
    };

    private static SourceTextDto? MapSourceText(TranslationSourceText? text) => text switch
    {
        SingularText s => new SourceTextDto
        {
            Type = "singular",
            Value = s.Value
        },
        PluralText p => new SourceTextDto
        {
            Type = "plural",
            Other = p.Other,
            Zero = p.Zero,
            One = p.One,
            Two = p.Two,
            Few = p.Few,
            Many = p.Many,
            ExactMatches = p.ExactMatches?.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
            IsOrdinal = p.IsOrdinal ? true : null
        },
        SelectText s => new SourceTextDto
        {
            Type = "select",
            Cases = s.Cases.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Otherwise = s.Otherwise
        },
        SelectPluralText sp => new SourceTextDto
        {
            Type = "selectPlural",
            PluralCases = sp.Cases.ToDictionary(
                kvp => kvp.Key,
                kvp => MapSourceText(kvp.Value)),
            OtherwisePlural = sp.Otherwise is not null ? MapSourceText(sp.Otherwise) : null
        },
        _ => null
    };

    private sealed class EntryDto
    {
        public required string Key { get; init; }
        public SourceTextDto? SourceText { get; init; }
        public Dictionary<string, SourceTextDto?>? InlineTranslations { get; init; }
        public required List<DefinitionDto> Definitions { get; init; }
        public required List<ReferenceDto> References { get; init; }
    }

    private sealed class SourceTextDto
    {
        public required string Type { get; init; }
        public string? Value { get; init; }
        public string? Other { get; init; }
        public string? Zero { get; init; }
        public string? One { get; init; }
        public string? Two { get; init; }
        public string? Few { get; init; }
        public string? Many { get; init; }
        public Dictionary<string, string>? ExactMatches { get; init; }
        public bool? IsOrdinal { get; init; }
        public Dictionary<string, string>? Cases { get; init; }
        public string? Otherwise { get; init; }
        public Dictionary<string, SourceTextDto?>? PluralCases { get; init; }
        public SourceTextDto? OtherwisePlural { get; init; }
    }

    private sealed class DefinitionDto
    {
        public required string FilePath { get; init; }
        public int Line { get; init; }
        public required string ProjectName { get; init; }
        public string? Context { get; init; }
    }

    private sealed class ReferenceDto
    {
        public required string FilePath { get; init; }
        public int Line { get; init; }
        public required string ProjectName { get; init; }
    }
}
