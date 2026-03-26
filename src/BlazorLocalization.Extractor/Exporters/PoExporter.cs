using System.Text;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;

namespace BlazorLocalization.Extractor.Exporters;

/// <summary>
/// Exports <see cref="MergedTranslationEntry"/> records as a GNU Gettext PO template (.pot).
/// </summary>
/// <remarks>
/// Crowdin API type: <c>gettext</c>.
/// <c>#:</c> reference comments carry file path and line number.
/// <c>#.</c> extracted comments carry the calling method context.
/// Crowdin GNU Gettext PO format: https://store.crowdin.com/gnu-gettext
/// </remarks>
public sealed class PoExporter : ITranslationExporter
{
	public string Export(IReadOnlyList<MergedTranslationEntry> entries)
	{
		var sb = new StringBuilder();
		WriteHeader(sb);

		foreach (var entry in entries)
		{
			WriteReferenceComments(sb, entry.Sources);
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

	private static void WriteReferenceComments(StringBuilder sb, IReadOnlyList<SourceReference> sources)
	{
		foreach (var src in sources)
		{
			sb.AppendLine($"#: {src.FilePath}:{src.Line}");
			if (src.Context is not null)
				sb.AppendLine($"#. {src.Context}");
		}
	}

	private static void WriteEntry(StringBuilder sb, MergedTranslationEntry entry)
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

	/// <summary>
	/// Emits a plural entry as native PO one/other, plus separate suffixed keys for exact value
	/// matches. Rare CLDR categories (zero, two, few, many) are handled by Crowdin per target language.
	/// </summary>
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
