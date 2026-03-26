using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;

namespace BlazorLocalization.Extractor.Cli.Rendering;

/// <summary>
/// Renders scan results as JSON to stdout for piped/machine-readable output.
/// </summary>
public static class JsonRenderer
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static void RenderInspect(
		string projectName,
		IReadOnlyList<ExtractedCall> calls,
		IReadOnlyList<MergedTranslationEntry> entries,
		IReadOnlyList<KeyConflict> conflicts,
		IReadOnlyList<PoLimitation> poLimitations,
		HashSet<string>? localeFilter)
	{
		var allLocales = DiscoverLocales(entries, localeFilter);
		var totalKeys = entries.Count;

		Console.WriteLine(JsonSerializer.Serialize(new
		{
			project = projectName,
			calls = calls.Select(MapCall),
			entries = entries.Select(MapEntry),
			conflicts = conflicts.Select(MapConflict),
			poLimitations = poLimitations.Count > 0 ? poLimitations.Select(l => new { key = l.Key, limitation = l.Limitation }) : null,
			localeCoverage = allLocales.Count > 0 ? allLocales.Select(locale =>
			{
				var count = entries.Count(e => e.InlineTranslations?.ContainsKey(locale) == true);
				return new { locale, keys = count, totalKeys };
			}) : null,
			localeEntries = allLocales.Count > 0 ? allLocales.ToDictionary(
				locale => locale,
				locale => entries
					.Where(e => e.InlineTranslations?.ContainsKey(locale) == true)
					.Select(e => new
					{
						key = e.Key,
						sourceText = MapSourceText(e.InlineTranslations![locale])
					})) : null
		}, Options));
	}

	public static void RenderExtract(
		string projectName,
		IReadOnlyList<MergedTranslationEntry> entries,
		IReadOnlyList<KeyConflict> conflicts)
	{
		Console.WriteLine(JsonSerializer.Serialize(new
		{
			project = projectName,
			entries = entries.Select(MapEntry),
			conflicts = conflicts.Select(MapConflict)
		}, Options));
	}

	private static object MapCall(ExtractedCall call) => new
	{
		type = call.ContainingTypeName,
		method = call.MethodName,
		kind = call.CallKind.ToString(),
		file = call.Location.FilePath,
		line = call.Location.Line,
		overloadResolution = call.OverloadResolution.ToString(),
		arguments = call.Arguments.Select(a => new
		{
			position = a.Position,
			name = a.ParameterName,
			value = a.Value
		}),
		fluentChain = call.FluentChain?.Select(c => new
		{
			method = c.MethodName,
			arguments = c.Arguments.Select(a => new
			{
				position = a.Position,
				name = a.ParameterName,
				value = a.Value,
				isLiteral = a.IsLiteral
			})
		})
	};

	private static object MapEntry(MergedTranslationEntry entry) => new
	{
		key = entry.Key,
		sourceText = MapSourceText(entry.SourceText),
		inlineTranslations = entry.InlineTranslations?.ToDictionary(
			kvp => kvp.Key,
			kvp => MapSourceText(kvp.Value)),
		sources = entry.Sources.Select(MapSource)
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

	private static object MapSource(SourceReference source) => new
	{
		filePath = source.FilePath,
		line = source.Line,
		projectName = source.ProjectName,
		context = source.Context
	};

	private static object MapConflict(KeyConflict conflict) => new
	{
		key = conflict.Key,
		values = conflict.Values.Select(v => new
		{
			sourceText = MapSourceText(v.SourceText),
			sources = v.Sources.Select(MapSource)
		})
	};

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
}
