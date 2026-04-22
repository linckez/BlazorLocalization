using System.Globalization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Text;

namespace BlazorLocalization.Analyzers.Scanning.TranslationFiles.Parsers;

/// <summary>
/// Parses .resx XML files into key→value pairs with culture extraction from the filename.
/// XML parsing logic adapted from the Extractor's ResxFileParser — proven, minimal, no dependencies.
/// </summary>
internal sealed class ResxTranslationFileParser : ITranslationFileParser
{
    public bool CanHandle(string path) =>
        path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase);

    public FileParseResult Parse(string path, SourceText text, CancellationToken cancellationToken)
    {
        var culture = ExtractCulture(path);
        var entries = ParseEntries(text);
        return new FileParseResult(culture, entries);
    }

    /// <summary>
    /// Parses resx XML content into key→value pairs.
    /// Filters out non-string entries (embedded files, images) that have a "type" attribute.
    /// Adapted from Extractor's ResxFileParser.ParseResx — uses SourceText instead of file path.
    /// </summary>
    private static Dictionary<string, string> ParseEntries(SourceText text)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var doc = XDocument.Parse(text.ToString());

        foreach (var data in doc.Descendants("data"))
        {
            // Entries with a "type" attribute are embedded resources, not strings
            if (data.Attribute("type") is not null)
                continue;

            var key = data.Attribute("name")?.Value;
            if (key is null)
                continue;

            var value = data.Element("value")?.Value;
            if (string.IsNullOrEmpty(value))
                continue;

            entries[key] = value!;
        }

        return entries;
    }

    /// <summary>
    /// Extracts the culture code from a .resx filename suffix.
    /// <c>Home.resx</c> → null (neutral); <c>Home.da.resx</c> → "da"; <c>Home.es-MX.resx</c> → "es-MX".
    /// Adapted from Extractor's ResxFileParser.ParseResxFileName.
    /// </summary>
    internal static string? ExtractCulture(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var lastDot = fileName.LastIndexOf('.');
        if (lastDot < 0)
            return null;

        var suffix = fileName.Substring(lastDot + 1);
        try
        {
            var culture = CultureInfo.GetCultureInfo(suffix);
            // CultureInfo.GetCultureInfo can return custom cultures for any string on .NET 6+.
            // Filter to real cultures: must have a non-empty Name and a known LCID or parent chain.
            if (string.IsNullOrEmpty(culture.Name))
                return null;
            // Custom cultures created on-the-fly have LCID 4096 (LOCALE_CUSTOM_UNSPECIFIED)
            // and no recognized parent. Reject them.
            if (culture.LCID == 4096 && culture.Parent == CultureInfo.InvariantCulture)
                return null;
            return suffix;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
