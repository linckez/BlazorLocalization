using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace BlazorLocalization.Extractor.Adapters.Resx;

/// <summary>
/// Parses .resx XML files and groups neutral + culture-specific files by base name.
/// Ported from the existing Extractor's ResxImporter — proven XML parsing logic.
/// </summary>
internal static class ResxFileParser
{
    /// <summary>
    /// Parses a single .resx file into a dictionary of key → (value, comment, line).
    /// Filters out non-string resource entries (embedded files, images, etc.).
    /// </summary>
    public static IReadOnlyDictionary<string, (string Value, string? Comment, int Line)> ParseResx(string resxFilePath)
    {
        var doc = XDocument.Load(resxFilePath, LoadOptions.SetLineInfo);
        var entries = new Dictionary<string, (string Value, string? Comment, int Line)>();

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

            var comment = data.Element("comment")?.Value;
            var line = (data as IXmlLineInfo)?.LineNumber ?? 0;

            entries[key] = (value, comment, line);
        }

        return entries;
    }

    /// <summary>
    /// Enumerates .resx files in a directory, excluding bin/ and obj/ folders.
    /// </summary>
    public static IEnumerable<string> EnumerateResxFiles(string root) =>
        Directory.EnumerateFiles(root, "*.resx", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Order();

    /// <summary>
    /// Groups .resx file paths by base name. For example, <c>Home.resx</c>, <c>Home.da.resx</c>,
    /// and <c>Home.es-MX.resx</c> all share the base name <c>Home</c>.
    /// </summary>
    public static Dictionary<string, ResxFileGroup> GroupByBaseName(IEnumerable<string> paths)
    {
        var groups = new Dictionary<string, ResxFileGroup>();

        foreach (var path in paths)
        {
            var (baseName, culture) = ParseResxFileName(path);
            if (!groups.TryGetValue(baseName, out var group))
            {
                group = new ResxFileGroup();
                groups[baseName] = group;
            }

            if (culture is null)
                group.NeutralPath = path;
            else
                group.CulturePaths[culture] = path;
        }

        return groups;
    }

    /// <summary>
    /// Parses a .resx file path into its base name and optional culture code.
    /// <c>/path/Home.resx</c> → (<c>/path/Home</c>, <c>null</c>);
    /// <c>/path/Home.da.resx</c> → (<c>/path/Home</c>, <c>"da"</c>).
    /// </summary>
    private static (string BaseName, string? Culture) ParseResxFileName(string path)
    {
        var dir = Path.GetDirectoryName(path);
        var fileNameNoResx = Path.GetFileNameWithoutExtension(path);
        var lastDot = fileNameNoResx.LastIndexOf('.');
        if (lastDot < 0)
            return (Combine(dir, fileNameNoResx), null);

        var suffix = fileNameNoResx[(lastDot + 1)..];
        try
        {
            CultureInfo.GetCultureInfo(suffix, predefinedOnly: true);
            return (Combine(dir, fileNameNoResx[..lastDot]), suffix);
        }
        catch
        {
            return (Combine(dir, fileNameNoResx), null);
        }

        static string Combine(string? dir, string name) =>
            dir is null ? name : Path.Combine(dir, name);
    }

    /// <summary>
    /// Mutable accumulator for grouping neutral and culture-specific .resx files by base name.
    /// </summary>
    internal sealed class ResxFileGroup
    {
        public string? NeutralPath { get; set; }
        public Dictionary<string, string> CulturePaths { get; } = new();
    }
}
