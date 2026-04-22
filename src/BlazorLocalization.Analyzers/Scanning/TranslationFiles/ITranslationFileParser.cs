using Microsoft.CodeAnalysis.Text;

namespace BlazorLocalization.Analyzers.Scanning.TranslationFiles;

/// <summary>
/// Parses a single translation file into culture + key→value pairs.
/// One implementation per format (resx, PO, JSON). No cross-file awareness —
/// grouping and merging is <see cref="TranslationFileLookup"/>'s responsibility.
/// </summary>
internal interface ITranslationFileParser
{
    bool CanHandle(string path);
    FileParseResult Parse(string path, SourceText text, CancellationToken cancellationToken);
}

/// <summary>
/// The result of parsing a single translation file.
/// <see cref="Culture"/>: the culture code (e.g. "da", "es-MX"), or null for the neutral/source file.
/// <see cref="Entries"/>: key → translated text pairs found in the file.
/// </summary>
internal sealed class FileParseResult
{
    public string? Culture { get; }
    public IReadOnlyDictionary<string, string> Entries { get; }

    public FileParseResult(string? culture, IReadOnlyDictionary<string, string> entries)
    {
        Culture = culture;
        Entries = entries;
    }
}
