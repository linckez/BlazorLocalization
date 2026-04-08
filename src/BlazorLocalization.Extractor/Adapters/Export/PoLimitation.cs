using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Export;

/// <summary>
/// A PO format limitation detected for a specific key.
/// PO cannot natively represent exact plural matches or ordinal forms.
/// </summary>
public sealed record PoLimitation(string Key, string Limitation)
{
    /// <summary>
    /// Scans entries for patterns that PO format cannot represent faithfully.
    /// </summary>
    public static IReadOnlyList<PoLimitation> Detect(IReadOnlyList<MergedTranslation> entries)
    {
        var results = new List<PoLimitation>();

        foreach (var entry in entries)
        {
            CheckSourceText(entry.Key, entry.SourceText, results);
        }

        return results;
    }

    private static void CheckSourceText(string key, TranslationSourceText? text, List<PoLimitation> results)
    {
        switch (text)
        {
            case PluralText p:
                CheckPlural(key, p, results);
                break;

            case SelectPluralText sp:
                foreach (var (caseValue, plural) in sp.Cases)
                    CheckPlural($"{key}_{caseValue}", plural, results);
                if (sp.Otherwise is not null)
                    CheckPlural(key, sp.Otherwise, results);
                break;
        }
    }

    private static void CheckPlural(string key, PluralText p, List<PoLimitation> results)
    {
        if (p.ExactMatches is { Count: > 0 })
            results.Add(new PoLimitation(key,
                "Exact plural matches (=0, =1, etc.) are exported as separate keys (_exactly_N). " +
                "PO translators won't see them as part of the plural form."));

        if (p.IsOrdinal)
            results.Add(new PoLimitation(key,
                "Ordinal forms (1st, 2nd, 3rd) are exported with a comment only. " +
                "PO has no native ordinal concept."));
    }
}
