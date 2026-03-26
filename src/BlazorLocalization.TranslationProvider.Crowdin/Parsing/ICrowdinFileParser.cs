namespace BlazorLocalization.TranslationProvider.Crowdin.Parsing;

/// <summary>
/// Parses a Crowdin CDN Distribution export file into a flat key/value dictionary.
/// </summary>
public interface ICrowdinFileParser
{
    /// <summary>
    /// Parses the raw file content returned by the Crowdin CDN.
    /// </summary>
    /// <param name="content">The raw file body (Android XML).</param>
    /// <param name="locale">The target locale (e.g. <c>pl</c>). Reserved for future format-specific needs; currently unused.</param>
    /// <returns>A dictionary mapping translation keys to their translated values.</returns>
    Dictionary<string, string> Parse(string content, string? locale = null);
}
