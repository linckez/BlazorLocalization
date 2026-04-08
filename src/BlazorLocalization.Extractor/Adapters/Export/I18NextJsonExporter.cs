using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Export;

/// <summary>
/// Exports translations as i18next JSON for Crowdin upload.
/// Plural keys use the <c>_one</c> / <c>_other</c> suffix convention.
/// Key-only entries (no source text) emit an empty string value.
/// </summary>
internal sealed class I18NextJsonExporter : ITranslationExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string Export(IReadOnlyList<MergedTranslation> entries, PathStyle pathStyle)
    {
        var dict = new Dictionary<string, string>();

        foreach (var entry in entries)
            EmitEntry(dict, entry.Key, entry.SourceText);

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static void EmitEntry(Dictionary<string, string> dict, string key, TranslationSourceText? sourceText)
    {
        switch (sourceText)
        {
            case PluralText p:
                EmitPluralText(dict, key, p);
                break;

            case SelectText s:
                foreach (var (caseValue, message) in s.Cases)
                    dict[$"{key}_{caseValue}"] = message;
                if (s.Otherwise is not null)
                    dict[key] = s.Otherwise;
                break;

            case SelectPluralText sp:
                foreach (var (caseValue, plural) in sp.Cases)
                    EmitPluralText(dict, $"{key}_{caseValue}", plural);
                if (sp.Otherwise is not null)
                    EmitPluralText(dict, key, sp.Otherwise);
                break;

            case SingularText s:
                dict[key] = s.Value;
                break;

            default:
                dict[key] = "";
                break;
        }
    }

    private static void EmitPluralText(Dictionary<string, string> dict, string key, PluralText p)
    {
        if (p.Zero is not null)  dict[$"{key}_zero"] = p.Zero;
        if (p.One is not null)   dict[$"{key}_one"] = p.One;
        if (p.Two is not null)   dict[$"{key}_two"] = p.Two;
        if (p.Few is not null)   dict[$"{key}_few"] = p.Few;
        if (p.Many is not null)  dict[$"{key}_many"] = p.Many;
        dict[$"{key}_other"] = p.Other;

        if (p.ExactMatches is not null)
            foreach (var (value, message) in p.ExactMatches)
                dict[$"{key}_exactly_{value}"] = message;
    }
}
