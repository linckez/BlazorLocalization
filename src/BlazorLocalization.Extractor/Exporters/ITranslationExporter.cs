using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;

namespace BlazorLocalization.Extractor.Exporters;

/// <summary>
/// Serializes <see cref="MergedTranslationEntry"/> records into a format-specific string.
/// </summary>
public interface ITranslationExporter
{
	string Export(IReadOnlyList<MergedTranslationEntry> entries);
}
