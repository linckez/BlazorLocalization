using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Export;

/// <summary>
/// Serializes <see cref="MergedTranslation"/> records into a format-specific string.
/// </summary>
internal interface ITranslationExporter
{
    string Export(IReadOnlyList<MergedTranslation> entries, PathStyle pathStyle);
}
