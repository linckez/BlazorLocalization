using System.Text;
using System.Text.RegularExpressions;
using BlazorLocalization.Extractor.Adapters.Roslyn;
using BlazorLocalization.Extractor.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BlazorLocalization.Extractor.Application;

/// <summary>
/// Migrates legacy <c>Localizer["key"]</c> / <c>Localizer.GetString("key")</c> calls
/// in <c>.razor</c> files to <c>Localizer.Translation(key: "…", sourceMessage: "…")</c>.
/// </summary>
internal static class RazorMigrator
{
    /// <summary>
    /// Analyses the Roslyn scan output and produces per-file migration results.
    /// Does NOT write files — the caller decides whether to apply.
    /// </summary>
    internal static RazorMigrationResult Migrate(
        RoslynScannerOutput roslynOutput,
        MergeResult mergeResult,
        IReadOnlySet<string> localeFilter)
    {
        var legacyCalls = roslynOutput.RawCalls
            .Where(IsLegacyLocalizerCall)
            .ToList();

        var skippedCalls = roslynOutput.RawCalls
            .Where(c => c.File.AbsolutePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        && c.CallKind is CallKind.Indexer or CallKind.MethodCall
                        && IsGetStringOrIndexer(c)
                        && !IsLegacyLocalizerCall(c))
            .ToList();

        if (legacyCalls.Count == 0 && skippedCalls.Count == 0)
            return RazorMigrationResult.Empty;

        var translationsByKey = mergeResult.Entries
            .ToDictionary(e => e.Key, e => e, StringComparer.Ordinal);

        var fileGroups = legacyCalls.GroupBy(c => c.File.AbsolutePath).OrderBy(g => g.Key);
        var skippedByFile = skippedCalls.GroupBy(c => c.File.AbsolutePath)
            .ToDictionary(g => g.Key, g => g.ToList());

        var fileResults = new List<MigrationFileResult>();

        foreach (var group in fileGroups)
        {
            var filePath = group.Key;
            var fileText = File.ReadAllText(filePath);
            var result = MigrateFile(fileText, filePath, group.ToList(), skippedByFile.GetValueOrDefault(filePath),
                translationsByKey, localeFilter);
            fileResults.Add(result);
            skippedByFile.Remove(filePath);
        }

        // Files that only have skipped calls (no replaceable calls)
        foreach (var (filePath, skipped) in skippedByFile.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            fileResults.Add(new MigrationFileResult
            {
                FilePath = filePath,
                Skipped = skipped.Select(c => new MigrationHit(
                    c.Arguments.Count > 0 ? c.Arguments[0].Value.Display() : c.MethodName, c.File, c.Line)).ToList()
            });
        }

        return new RazorMigrationResult(legacyCalls.Count, skippedCalls.Count, fileResults);
    }

    // ── Call classification ─────────────────────────────────────

    private static bool IsGetStringOrIndexer(ScannedCallSite call) =>
        call.CallKind == CallKind.Indexer || call.MethodName == "GetString";

    private static bool IsLegacyLocalizerCall(ScannedCallSite call) =>
        call.File.AbsolutePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
        && call.CallKind is CallKind.Indexer or CallKind.MethodCall
        && IsGetStringOrIndexer(call)
        && call.Arguments.Count == 1
        && call.Arguments[0].Value.IsLiteral;

    // ── Per-file transformation ─────────────────────────────────

    private static MigrationFileResult MigrateFile(
        string fileText,
        string filePath,
        List<ScannedCallSite> calls,
        List<ScannedCallSite>? skippedCalls,
        Dictionary<string, MergedTranslation> translationsByKey,
        IReadOnlySet<string> localeFilter)
    {
        var result = new MigrationFileResult { FilePath = filePath };

        if (skippedCalls is not null)
        {
            foreach (var call in skippedCalls)
                result.Skipped.Add(new MigrationHit(
                    call.Arguments.Count > 0 ? call.Arguments[0].Value.Display() : call.MethodName, call.File, call.Line));
        }

        var replacements = new List<(int Start, int End, string Replacement, MigrationHit Hit)>();

        foreach (var call in calls)
        {
            if (!call.Arguments[0].Value.TryGetString(out var key) || key is null)
                continue;

            var span = FindSpanOnLine(fileText, call.Line, key, call.CallKind);
            if (span is null)
                continue;

            var (start, end) = span.Value;
            var originalText = fileText[start..end];

            var dotOrBracket = originalText.IndexOfAny(['.', '[']);
            if (dotOrBracket < 0)
                continue;
            var localizerName = originalText[..dotOrBracket];

            var sourceText = "";
            var keyFound = false;
            Dictionary<string, string>? translations = null;

            if (translationsByKey.TryGetValue(key, out var merged))
            {
                keyFound = true;
                if (merged.SourceText is SingularText singular)
                    sourceText = singular.Value;

                if (merged.InlineTranslations is not null && localeFilter.Count > 0)
                {
                    translations = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (locale, text) in merged.InlineTranslations
                        .Where(kv => localeFilter.Contains(kv.Key))
                        .OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    {
                        if (text is SingularText st)
                            translations[locale] = st.Value;
                    }
                    if (translations.Count == 0)
                        translations = null;
                }
            }

            var replacement = GenerateReplacement(localizerName, key, sourceText, translations);

            if (!IsValidExpression(replacement))
            {
                result.ValidationFailed.Add(new MigrationHit(key, call.File, call.Line));
                continue;
            }

            replacements.Add((start, end, replacement, new MigrationHit(key, call.File, call.Line)));

            if (keyFound)
                result.Replaced.Add(new MigrationHit(key, call.File, call.Line));
            else
                result.NotFound.Add(new MigrationHit(key, call.File, call.Line));
        }

        if (replacements.Count > 0)
        {
            var sb = new StringBuilder(fileText);
            foreach (var (start, end, replacement, _) in replacements.OrderByDescending(r => r.Start))
            {
                sb.Remove(start, end - start);
                sb.Insert(start, replacement);
            }
            result.TransformedText = sb.ToString();
        }

        return result;
    }

    // ── Offset finding ──────────────────────────────────────────

    /// <summary>
    /// Finds the exact character span of a localizer call on the given line.
    /// The scanner gives us file + line + key; this finds the offset in the raw .razor text.
    /// </summary>
    internal static (int Start, int End)? FindSpanOnLine(string fileText, int targetLine, string key, CallKind callKind)
    {
        var lineStart = 0;
        var currentLine = 1;
        for (var i = 0; i < fileText.Length && currentLine < targetLine; i++)
        {
            if (fileText[i] == '\n')
            {
                currentLine++;
                lineStart = i + 1;
            }
        }

        if (currentLine != targetLine)
            return null;

        var lineEnd = fileText.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = fileText.Length;
        var lineText = fileText[lineStart..lineEnd];

        var escapedKey = Regex.Escape(key);
        var pattern = callKind == CallKind.Indexer
            ? $@"(\w+)\[""{escapedKey}""\]"
            : $@"(\w+)\.GetString\(""{escapedKey}""\)";

        var match = Regex.Match(lineText, pattern);
        if (!match.Success)
            return null;

        return (lineStart + match.Index, lineStart + match.Index + match.Length);
    }

    // ── Code generation ─────────────────────────────────────────

    internal static string GenerateReplacement(
        string localizerName, string key, string sourceText, Dictionary<string, string>? translations)
    {
        var sb = new StringBuilder();
        sb.Append(localizerName);
        sb.Append(".Translation(key: \"");
        sb.Append(EscapeString(key));
        sb.Append("\", sourceMessage: \"");
        sb.Append(EscapeString(sourceText));
        sb.Append("\")");

        if (translations is not null)
        {
            foreach (var (culture, text) in translations.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                sb.Append(".For(locale: \"");
                sb.Append(EscapeString(culture));
                sb.Append("\", message: \"");
                sb.Append(EscapeString(text));
                sb.Append("\")");
            }
        }

        return sb.ToString();
    }

    internal static bool IsValidExpression(string expression)
    {
        var wrapper = $"class X {{ object Y = {expression}; }}";
        var tree = CSharpSyntaxTree.ParseText(wrapper);
        return !tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    internal static string EscapeString(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\"': sb.Append("\\\""); break;
                case '\0': sb.Append("\\0"); break;
                case '\a': sb.Append("\\a"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\v': sb.Append("\\v"); break;
                default:
                    if (char.IsControl(ch))
                        sb.Append($"\\u{(int)ch:x4}");
                    else
                        sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }
}

// ── Result types ────────────────────────────────────────────

/// <summary>
/// Top-level result from <see cref="RazorMigrator.Migrate"/>.
/// </summary>
internal sealed record RazorMigrationResult(
    int ReplaceableCount,
    int SkippedCount,
    IReadOnlyList<MigrationFileResult> FileResults)
{
    internal static readonly RazorMigrationResult Empty = new(0, 0, []);
    internal bool IsEmpty => ReplaceableCount == 0 && SkippedCount == 0;
}

/// <summary>
/// A single migration hit — a localizer call that was processed (replaced, skipped, or failed).
/// </summary>
internal sealed record MigrationHit(string Key, SourceFilePath File, int Line);

/// <summary>
/// Per-file result of the migration.
/// </summary>
internal sealed class MigrationFileResult
{
    public required string FilePath { get; init; }
    public List<MigrationHit> Replaced { get; set; } = [];
    public List<MigrationHit> Skipped { get; set; } = [];
    public List<MigrationHit> NotFound { get; set; } = [];
    public List<MigrationHit> ValidationFailed { get; set; } = [];
    public string? TransformedText { get; set; }
}
