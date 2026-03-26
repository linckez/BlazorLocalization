using BlazorLocalization.Extractor.Domain.Entries;

namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// A PO format limitation detected for a specific translation key.
/// </summary>
public sealed record PoLimitation(string Key, string Limitation)
{
	/// <summary>
	/// Scans merged entries for features that PO cannot fully represent.
	/// </summary>
	public static IReadOnlyList<PoLimitation> Detect(IReadOnlyList<MergedTranslationEntry> entries)
	{
		var limitations = new List<PoLimitation>();

		foreach (var entry in entries)
			DetectInSourceText(limitations, entry.Key, entry.SourceText);

		return limitations;
	}

	private static void DetectInSourceText(List<PoLimitation> limitations, string key, TranslationSourceText? sourceText)
	{
		switch (sourceText)
		{
			case PluralText p:
				DetectInPlural(limitations, key, p);
				break;
			case SelectPluralText sp:
				foreach (var (caseValue, plural) in sp.Cases)
					DetectInPlural(limitations, $"{key}_{caseValue}", plural);
				if (sp.Otherwise is not null)
					DetectInPlural(limitations, key, sp.Otherwise);
				break;
		}
	}

	private static void DetectInPlural(List<PoLimitation> limitations, string key, PluralText p)
	{
		if (p.ExactMatches is { Count: > 0 })
			limitations.Add(new(key, $"Exact matches ({string.Join(", ", p.ExactMatches.Keys.Select(k => $"={k}"))}) exported as separate _exactly_N keys"));

		if (p.IsOrdinal)
			limitations.Add(new(key, "Ordinal flag exported as comment only — translators may overlook it"));
	}
}
