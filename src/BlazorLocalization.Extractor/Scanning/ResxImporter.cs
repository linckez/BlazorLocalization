using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;

namespace BlazorLocalization.Extractor.Scanning;

/// <summary>
/// Imports translation entries from .resx files by parsing XML directly into <see cref="TranslationEntry"/> records.
/// Groups neutral and culture-specific .resx files by base name (e.g. <c>Home.resx</c>, <c>Home.da.resx</c>,
/// <c>Home.es-MX.resx</c>) and produces entries with <see cref="TranslationEntry.InlineTranslations"/> populated
/// from the culture-specific files.
/// </summary>
public static class ResxImporter
{
	/// <summary>
	/// Imports all .resx files from a project directory, grouping neutral and culture-specific files by base name.
	/// Culture-specific values are mapped into <see cref="TranslationEntry.InlineTranslations"/>.
	/// </summary>
	public static IReadOnlyList<TranslationEntry> ImportFromProject(string projectDir)
	{
		var projectName = Path.GetFileName(projectDir);
		var groups = GroupResxFilesByBaseName(EnumerateResxFiles(projectDir));
		return groups
			.OrderBy(g => g.Key, StringComparer.Ordinal)
			.SelectMany(g => ImportGroup(g.Value, projectName))
			.ToList();
	}

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
			if (!IsStringEntry(data))
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
	/// Imports a group of .resx files sharing a base name into <see cref="TranslationEntry"/> records.
	/// The neutral file provides <see cref="TranslationEntry.SourceText"/>; culture-specific files
	/// provide <see cref="TranslationEntry.InlineTranslations"/>.
	/// </summary>
	private static IEnumerable<TranslationEntry> ImportGroup(
		ResxFileGroup group,
		string projectName)
	{
		var neutralEntries = group.NeutralPath is not null
			? ParseResx(group.NeutralPath)
			: new Dictionary<string, (string Value, string? Comment, int Line)>();

		var cultureEntries = new Dictionary<string, IReadOnlyDictionary<string, (string Value, string? Comment, int Line)>>();
		foreach (var (culture, path) in group.CulturePaths)
			cultureEntries[culture] = ParseResx(path);

		// Collect all keys across neutral and all culture files
		var allKeys = new HashSet<string>(neutralEntries.Keys);
		foreach (var entries in cultureEntries.Values)
			allKeys.UnionWith(entries.Keys);

		foreach (var key in allKeys.Order(StringComparer.Ordinal))
		{
			// Neutral → SourceText
			TranslationSourceText? sourceText = null;
			string? sourceFile = null;
			var sourceLine = 0;
			string? comment = null;

			if (neutralEntries.TryGetValue(key, out var neutral))
			{
				sourceText = new SingularText(neutral.Value);
				sourceFile = group.NeutralPath;
				sourceLine = neutral.Line;
				comment = neutral.Comment;
			}

			// Culture files → InlineTranslations
			Dictionary<string, TranslationSourceText>? inlineTranslations = null;
			foreach (var (culture, entries) in cultureEntries)
			{
				if (entries.TryGetValue(key, out var cultureEntry))
				{
					inlineTranslations ??= new Dictionary<string, TranslationSourceText>();
					inlineTranslations[culture] = new SingularText(cultureEntry.Value);
				}
			}

			// For culture-only keys, use the first culture file that has the key as the source reference
			if (sourceFile is null && inlineTranslations is not null)
			{
				var firstCulture = inlineTranslations.Keys.Order(StringComparer.Ordinal).First();
				sourceFile = group.CulturePaths[firstCulture];
				var firstEntries = cultureEntries[firstCulture];
				if (firstEntries.TryGetValue(key, out var firstEntry))
				{
					sourceLine = firstEntry.Line;
					comment = firstEntry.Comment;
				}
			}

			var source = new SourceReference(sourceFile!, sourceLine, projectName, comment);
			yield return new TranslationEntry(key, sourceText, source, inlineTranslations);
		}
	}

	/// <summary>
	/// Groups .resx file paths by base name. For example, <c>Home.resx</c>, <c>Home.da.resx</c>,
	/// and <c>Home.es-MX.resx</c> all share the base name <c>Home</c>.
	/// </summary>
	private static Dictionary<string, ResxFileGroup> GroupResxFilesByBaseName(IEnumerable<string> paths)
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
	/// Parses a .resx file path into its base name (full path without culture suffix and extension)
	/// and optional culture code. For example:
	/// <c>/path/Home.resx</c> → (<c>/path/Home</c>, <c>null</c>)
	/// <c>/path/Home.da.resx</c> → (<c>/path/Home</c>, <c>"da"</c>)
	/// <c>/path/Home.es-MX.resx</c> → (<c>/path/Home</c>, <c>"es-MX"</c>)
	/// </summary>
	private static (string BaseName, string? Culture) ParseResxFileName(string path)
	{
		var dir = Path.GetDirectoryName(path);
		var fileNameNoResx = Path.GetFileNameWithoutExtension(path); // strips .resx
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
	/// Returns true if the <c>&lt;data&gt;</c> element represents a string resource.
	/// Entries with a <c>type</c> attribute (e.g. <c>System.Resources.ResXFileRef</c>) are embedded resources.
	/// </summary>
	private static bool IsStringEntry(XElement data) =>
		data.Attribute("type") is null;

	private static IEnumerable<string> EnumerateResxFiles(string root) =>
		Directory.EnumerateFiles(root, "*.resx", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
						&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
			.Order();

	/// <summary>
	/// Mutable accumulator for grouping neutral and culture-specific .resx files by base name.
	/// </summary>
	private sealed class ResxFileGroup
	{
		public string? NeutralPath { get; set; }
		public Dictionary<string, string> CulturePaths { get; } = new();
	}
}
