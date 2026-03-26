namespace BlazorLocalization.Extensions.Providers.PoFile;

/// <summary>
/// Parses GNU gettext PO files into a flat key/value dictionary.
/// Handles singular <c>msgid</c>/<c>msgstr</c> pairs and plural forms
/// (<c>msgid_plural</c>/<c>msgstr[0]</c>/<c>msgstr[1]</c>/…).
/// </summary>
/// <remarks>
/// Plural entries are emitted with CLDR category suffixes (<c>_one</c>, <c>_few</c>, <c>_many</c>, <c>_other</c>, …)
/// derived from the locale via <see cref="PoPluralMapping.MapPoPluralIndex"/>.
/// </remarks>
internal static class PoFileParser
{
    public static Dictionary<string, string> Parse(string content, string locale)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string? currentMsgId = null;
        string? currentMsgStr = null;
        string? currentMsgIdPlural = null;
        Dictionary<int, string>? currentPluralForms = null;
        var section = Section.None;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith('#'))
                continue;

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushEntry(locale, result, ref currentMsgId, ref currentMsgStr, ref currentMsgIdPlural, ref currentPluralForms);
                section = Section.None;
                continue;
            }

            if (line.StartsWith("msgid_plural "))
            {
                currentMsgIdPlural = ExtractQuotedString(line, "msgid_plural ");
                section = Section.MsgIdPlural;
            }
            else if (line.StartsWith("msgid "))
            {
                FlushEntry(locale, result, ref currentMsgId, ref currentMsgStr, ref currentMsgIdPlural, ref currentPluralForms);
                currentMsgId = ExtractQuotedString(line, "msgid ");
                section = Section.MsgId;
            }
            else if (TryParseMsgStrIndex(line, out var index, out var value))
            {
                currentPluralForms ??= new Dictionary<int, string>();
                currentPluralForms[index] = value;
                section = Section.MsgStrN;
            }
            else if (line.StartsWith("msgstr "))
            {
                currentMsgStr = ExtractQuotedString(line, "msgstr ");
                section = Section.MsgStr;
            }
            else if (line.StartsWith('"') && line.EndsWith('"'))
            {
                var continuation = Unquote(line);
                switch (section)
                {
                    case Section.MsgId:
                        currentMsgId += continuation;
                        break;
                    case Section.MsgIdPlural:
                        currentMsgIdPlural += continuation;
                        break;
                    case Section.MsgStr:
                        currentMsgStr += continuation;
                        break;
                    case Section.MsgStrN when currentPluralForms is not null:
                        var lastIndex = currentPluralForms.Keys.Max();
                        currentPluralForms[lastIndex] += continuation;
                        break;
                }
            }
        }

        FlushEntry(locale, result, ref currentMsgId, ref currentMsgStr, ref currentMsgIdPlural, ref currentPluralForms);
        return result;
    }

    private static void FlushEntry(
        string locale,
        Dictionary<string, string> result,
        ref string? msgId,
        ref string? msgStr,
        ref string? msgIdPlural,
        ref Dictionary<int, string>? pluralForms)
    {
        if (msgId is { Length: > 0 })
        {
            if (msgIdPlural is not null && pluralForms is { Count: > 0 })
            {
                foreach (var (index, value) in pluralForms)
                {
                    if (value.Length == 0) continue;
                    var category = PoPluralMapping.MapPoPluralIndex(locale, index);
                    if (category is not null)
                        result[msgId + "_" + category] = value;
                }
            }
            else if (msgStr is { Length: > 0 })
            {
                result[msgId] = msgStr;
            }
        }

        msgId = null;
        msgStr = null;
        msgIdPlural = null;
        pluralForms = null;
    }

    private static bool TryParseMsgStrIndex(string line, out int index, out string value)
    {
        if (line.StartsWith("msgstr["))
        {
            var closeBracket = line.IndexOf(']', 7);
            if (closeBracket > 7
                && int.TryParse(line.AsSpan(7, closeBracket - 7), out index)
                && closeBracket + 2 < line.Length)
            {
                value = Unquote(line[(closeBracket + 2)..]);
                return true;
            }
        }

        index = 0;
        value = string.Empty;
        return false;
    }

    private static string ExtractQuotedString(string line, string prefix)
        => Unquote(line[prefix.Length..]);

    private static string Unquote(string s)
        => s.Length >= 2 && s[0] == '"' && s[^1] == '"'
            ? s[1..^1]
            : s;

    private enum Section { None, MsgId, MsgIdPlural, MsgStr, MsgStrN }
}
