using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions.Translation;

/// <summary>
/// Resolution helpers shared by all fluent translation builders.
/// </summary>
internal static class TranslationResolver
{
    /// <summary>
    /// Looks up <paramref name="suffixedKey"/> in the localizer's provider/cache chain.
    /// Returns the translated value, or <c>null</c> if the key was not found.
    /// Swallows any exception from the localizer so the builder can fall through
    /// to inline translations or the source text instead of crashing the render tree.
    /// </summary>
    public static string? TryLocalizer(IStringLocalizer localizer, string suffixedKey)
    {
        try
        {
            var result = localizer[suffixedKey];
            return result.ResourceNotFound ? null : result.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Walks the current UI culture chain against inline translations.
    /// Returns the best match, or <c>null</c> if none match.
    /// </summary>
    public static string? TryInline(Dictionary<string, string>? inlineMessages)
    {
        if (inlineMessages is null) return null;

        var culture = CultureInfo.CurrentUICulture;
        while (!string.IsNullOrEmpty(culture.Name))
        {
            if (inlineMessages.TryGetValue(culture.Name, out var msg))
                return msg;
            culture = culture.Parent;
        }

        return null;
    }
}
