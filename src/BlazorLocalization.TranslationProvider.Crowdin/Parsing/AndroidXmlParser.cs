using System.Xml.Linq;

namespace BlazorLocalization.TranslationProvider.Crowdin.Parsing;

/// <summary>
/// Parses Android XML (<c>&lt;resources&gt;</c>) as exported by Crowdin CDN Distributions.
/// Crowdin wraps dotted keys in literal <c>"</c> quotes (e.g. <c>&amp;quot;RC.Title&amp;quot;</c>) — these are stripped.
/// </summary>
internal sealed class AndroidXmlParser : ICrowdinFileParser
{
    public Dictionary<string, string> Parse(string content, string? locale = null)
    {
        var doc = XDocument.Parse(content);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var el in doc.Descendants("string"))
        {
            var name = el.Attribute("name")?.Value;
            if (name is null) continue;

            // Crowdin wraps dotted keys in literal " quotes: "RC.Title" → RC.Title
            if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
                name = name[1..^1];

            result[name] = el.Value;
        }

        return result;
    }
}
