using SmartFormat;
using SmartFormat.Core.Settings;

namespace BlazorLocalization.Extensions.Translation;

/// <summary>
/// Resolves named <c>{placeholders}</c> in a message using the properties of
/// the <c>replaceWith</c> object. Unresolvable placeholders stay as literal text
/// (e.g. <c>{MissingName}</c>) instead of throwing.
/// </summary>
internal static class TranslationFormatter
{
    private static readonly SmartFormatter Formatter = Smart.CreateDefaultSmartFormat(new SmartSettings
    {
        Formatter = new FormatterSettings { ErrorAction = FormatErrorAction.MaintainTokens },
        Parser = new ParserSettings { ErrorAction = ParseErrorAction.MaintainTokens }
    });

    public static string Format(string message, object? replaceWith)
    {
        if (replaceWith is null)
            return message;

        try
        {
            return Formatter.Format(message, replaceWith);
        }
        catch
        {
            return message;
        }
    }
}
