using System.Text;
using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Export;

/// <summary>
/// Exports translations as a GNU Gettext PO template (.pot).
/// <c>#:</c> reference comments carry file path and line number from definition sites.
/// <c>#.</c> extracted comments carry the context (e.g. resx comment or containing type).
/// </summary>
internal sealed class PoExporter : ITranslationExporter
{
    public string Export(IReadOnlyList<MergedTranslation> entries, PathStyle pathStyle)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);

        foreach (var entry in entries)
        {
            WriteReferenceComments(sb, entry, pathStyle);
            WriteEntry(sb, entry);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine("msgid \"\"");
        sb.AppendLine("msgstr \"\"");
        sb.AppendLine("\"MIME-Version: 1.0\\n\"");
        sb.AppendLine("\"Content-Type: text/plain; charset=UTF-8\\n\"");
        sb.AppendLine("\"Content-Transfer-Encoding: 8bit\\n\"");
        sb.AppendLine("\"Plural-Forms: nplurals=2; plural=(n != 1);\\n\"");
        sb.AppendLine("\"X-Crowdin-SourceKey: msgstr\\n\"");
        sb.AppendLine();
    }

    private static void WriteReferenceComments(StringBuilder sb, MergedTranslation entry, PathStyle pathStyle)
    {
        foreach (var site in entry.Definitions)
        {
            sb.AppendLine($"#: {site.File.Display(pathStyle)}:{site.Line}");
            if (site.Context is not null)
                sb.AppendLine($"#. {site.Context}");
        }

        // For reference-only entries (no definitions), include reference sites
        if (entry.Definitions.Count == 0)
        {
            foreach (var site in entry.References)
            {
                sb.AppendLine($"#: {site.File.Display(pathStyle)}:{site.Line}");
                if (site.Context is not null)
                    sb.AppendLine($"#. {site.Context}");
            }
        }
    }

    private static void WriteEntry(StringBuilder sb, MergedTranslation entry)
    {
        switch (entry.SourceText)
        {
            case PluralText p:
                WritePluralEntries(sb, entry.Key, p);
                break;

            case SelectText s:
                foreach (var (caseValue, message) in s.Cases)
                {
                    sb.AppendLine($"msgid \"{Escape(entry.Key + "_" + caseValue)}\"");
                    sb.AppendLine($"msgstr \"{Escape(message)}\"");
                    sb.AppendLine();
                }
                if (s.Otherwise is not null)
                {
                    sb.AppendLine($"msgid \"{Escape(entry.Key)}\"");
                    sb.AppendLine($"msgstr \"{Escape(s.Otherwise)}\"");
                }
                else
                {
                    sb.AppendLine($"msgid \"{Escape(entry.Key)}\"");
                    sb.AppendLine("msgstr \"\"");
                }
                break;

            case SelectPluralText sp:
                foreach (var (caseValue, plural) in sp.Cases)
                {
                    WritePluralEntries(sb, $"{entry.Key}_{caseValue}", plural);
                    sb.AppendLine();
                }
                if (sp.Otherwise is not null)
                    WritePluralEntries(sb, entry.Key, sp.Otherwise);
                break;

            case SingularText s:
                sb.AppendLine($"msgid \"{Escape(entry.Key)}\"");
                sb.AppendLine($"msgstr \"{Escape(s.Value)}\"");
                break;

            default:
                sb.AppendLine($"msgid \"{Escape(entry.Key)}\"");
                sb.AppendLine("msgstr \"\"");
                break;
        }
    }

    private static void WritePluralEntries(StringBuilder sb, string key, PluralText p)
    {
        if (p.IsOrdinal)
            sb.AppendLine("#. ⚠️ ORDINAL — use ordinal forms (1st, 2nd, 3rd), not cardinal (1, 2, 3)");

        sb.AppendLine($"msgid \"{Escape(key)}\"");
        sb.AppendLine($"msgid_plural \"{Escape(key)}\"");
        sb.AppendLine($"msgstr[0] \"{Escape(p.One ?? p.Other)}\"");
        sb.AppendLine($"msgstr[1] \"{Escape(p.Other)}\"");

        if (p.ExactMatches is not null)
        {
            foreach (var (value, message) in p.ExactMatches)
            {
                sb.AppendLine();
                sb.AppendLine($"msgid \"{Escape($"{key}_exactly_{value}")}\"");
                sb.AppendLine($"msgstr \"{Escape(message)}\"");
            }
        }
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
}
