using System.Collections.Concurrent;
using BlazorLocalization.Extensions.Translation;

namespace BlazorLocalization.Extensions.Providers.PoFile;

/// <summary>
/// Maps PO <c>msgstr[N]</c> indices to CLDR category names for a given locale.
/// Delegates to the auto-generated <see cref="PluralRules"/> for actual category resolution.
/// </summary>
public static class PoPluralMapping
{
    private static readonly string[] CanonicalOrder = ["zero", "one", "two", "few", "many", "other"];
    private static readonly ConcurrentDictionary<string, string[]> CategoriesCache = new();

    /// <summary>
    /// Returns the CLDR plural categories active for the given locale, in canonical order.
    /// Always includes "other" (mandatory CLDR fallback even when unreachable for integers).
    /// </summary>
    public static string[] GetCategories(string locale, bool isOrdinal = false)
    {
        var key = $"{locale}|{isOrdinal}";
        return CategoriesCache.GetOrAdd(key, _ =>
        {
            var active = new HashSet<string>();
            for (var n = 0; n <= 199; n++)
                active.Add(PluralRules.GetCategory(locale, n, isOrdinal));

            // "other" is always a valid CLDR category, even when no integer maps to it.
            active.Add("other");

            return CanonicalOrder.Where(active.Contains).ToArray();
        });
    }

    /// <summary>
    /// Maps a PO <c>msgstr[N]</c> index to the corresponding CLDR category name for the locale.
    /// Returns <c>null</c> if the index is out of range (more forms than the locale has categories).
    /// </summary>
    public static string? MapPoPluralIndex(string locale, int index)
    {
        var categories = GetCategories(locale);
        return index < categories.Length ? categories[index] : null;
    }
}
