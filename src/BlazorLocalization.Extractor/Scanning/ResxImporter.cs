using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;

namespace BlazorLocalization.Extractor.Scanning;

/// <summary>
/// Imports translation entries from .resx files by parsing XML directly into <see cref="TranslationEntry"/> records.
/// Only imports the neutral (culture-invariant) .resx file — culture-specific files like <c>Home.da-DK.resx</c> are skipped.
/// </summary>
public static class ResxImporter
{
	/// <summary>
	/// Imports all neutral .resx files from a project directory.
	/// </summary>
	public static IReadOnlyList<TranslationEntry> ImportFromProject(string projectDir)
	{
		var projectName = Path.GetFileName(projectDir);
		return EnumerateResxFiles(projectDir)
			.Where(IsNeutralResx)
			.SelectMany(path => Import(path, projectName))
			.ToList();
	}

	/// <summary>
	/// Parses a single .resx file into <see cref="TranslationEntry"/> records.
	/// Filters out non-string resource entries (embedded files, images, etc.).
	/// </summary>
	public static IReadOnlyList<TranslationEntry> Import(string resxFilePath, string projectName)
	{
		var doc = XDocument.Load(resxFilePath, LoadOptions.SetLineInfo);
		var entries = new List<TranslationEntry>();

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

			var source = new SourceReference(resxFilePath, line, projectName, comment);
			entries.Add(new TranslationEntry(key, new SingularText(value), source));
		}

		return entries;
	}

	/// <summary>
	/// Returns true if the .resx file is culture-neutral (e.g. <c>Home.resx</c>),
	/// false for culture-specific files (e.g. <c>Home.da-DK.resx</c>).
	/// </summary>
	private static bool IsNeutralResx(string path)
	{
		var fileName = Path.GetFileNameWithoutExtension(path);
		var lastDot = fileName.LastIndexOf('.');
		if (lastDot < 0)
			return true;

		var suffix = fileName[(lastDot + 1)..];
		try
		{
			CultureInfo.GetCultureInfo(suffix);
			return false;
		}
		catch (CultureNotFoundException)
		{
			return true;
		}
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
}
