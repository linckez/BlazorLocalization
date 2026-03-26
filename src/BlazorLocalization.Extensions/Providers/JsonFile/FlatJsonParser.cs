using System.Text.Json;

namespace BlazorLocalization.Extensions.Providers.JsonFile;

/// <summary>
/// Parses flat <c>{"key": "value"}</c> JSON files.
/// Plural suffixes (<c>_one</c>, <c>_other</c>) are preserved as-is for SmartFormat resolution.
/// </summary>
internal static class FlatJsonParser
{
    public static Dictionary<string, string> Parse(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        using var doc = JsonDocument.Parse(content);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String)
                result[property.Name] = property.Value.GetString()!;
        }

        return result;
    }
}
