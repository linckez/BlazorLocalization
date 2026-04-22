namespace BlazorLocalization.Analyzers.Scanning;

/// <summary>
/// Converts a translation key string (e.g. "Obs.Save", "save_button") into a
/// valid PascalCase C# field name suitable for a static definition field.
/// </summary>
internal static class KeyToIdentifier
{
    private static readonly char[] Separators = { '.', '-', '_', ' ' };

    /// <summary>
    /// Converts a translation key to a PascalCase C# identifier.
    /// <example>"Obs.Save" → "ObsSave"</example>
    /// <example>"save_button" → "SaveButton"</example>
    /// <example>"123" → "Translation123"</example>
    /// </summary>
    public static string ToFieldName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "TranslationDefinition";

        // Strip invalid chars first (replacing them with separator to preserve word boundaries)
        var sanitized = new System.Text.StringBuilder(key.Length);
        foreach (var ch in key)
        {
            if (char.IsLetterOrDigit(ch))
                sanitized.Append(ch);
            else if (IsSeparator(ch))
                sanitized.Append(ch);
            else
                sanitized.Append('.'); // treat invalid chars as word boundaries
        }

        var segments = sanitized.ToString().Split(Separators, System.StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return "TranslationDefinition";

        var builder = new System.Text.StringBuilder();

        foreach (var segment in segments)
        {
            AppendPascalCased(builder, segment);
        }

        var result = builder.ToString();

        if (result.Length == 0)
            return "TranslationDefinition";

        // C# identifiers can't start with a digit
        if (char.IsDigit(result[0]))
            result = "Translation" + result;

        return result;
    }

    private static bool IsSeparator(char ch)
    {
        for (var i = 0; i < Separators.Length; i++)
        {
            if (Separators[i] == ch)
                return true;
        }

        return false;
    }

    private static void AppendPascalCased(System.Text.StringBuilder builder, string segment)
    {
        if (segment.Length == 0)
            return;

        // If the segment is already PascalCase or camelCase, split on casing boundaries
        var i = 0;
        while (i < segment.Length)
        {
            // Find the start of a word
            if (i == 0 || char.IsUpper(segment[i]) || (i > 0 && char.IsDigit(segment[i]) && !char.IsDigit(segment[i - 1])))
            {
                builder.Append(char.ToUpperInvariant(segment[i]));
                i++;

                // Consume lowercase/digit run
                while (i < segment.Length && !char.IsUpper(segment[i])
                       && !(char.IsDigit(segment[i]) && i > 0 && !char.IsDigit(segment[i - 1])))
                {
                    builder.Append(segment[i]);
                    i++;
                }
            }
            else
            {
                builder.Append(segment[i]);
                i++;
            }
        }
    }
}
