namespace BlazorLocalization.Extractor.Domain.Entries;

/// <summary>
/// Discovers available locales from inline <c>.For()</c> translations across entries.
/// Single source of truth for locale enumeration — used by commands, renderers, and exporters.
/// </summary>
public static class LocaleDiscovery
{
	/// <summary>
	/// Returns a sorted, deduplicated list of locale codes present in <paramref name="entries"/>.
	/// When <paramref name="localeFilter"/> is provided, only matching locales are returned.
	/// </summary>
	public static IReadOnlyList<string> DiscoverLocales(
		IReadOnlyList<MergedTranslationEntry> entries,
		HashSet<string>? localeFilter = null)
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

	/// <summary>
	/// Rewrites entries so that the inline translation for <paramref name="locale"/>
	/// becomes the source text. Entries without a translation for that locale are excluded.
	/// Used for per-locale file export and single-locale stdout output.
	/// </summary>
	public static IReadOnlyList<MergedTranslationEntry> EntriesForLocale(
		IReadOnlyList<MergedTranslationEntry> entries,
		string locale)
	{
		return entries
			.Where(e => e.InlineTranslations is not null && e.InlineTranslations.ContainsKey(locale))
			.Select(e => new MergedTranslationEntry(e.Key, e.InlineTranslations![locale], e.Sources))
			.ToList();
	}
}
