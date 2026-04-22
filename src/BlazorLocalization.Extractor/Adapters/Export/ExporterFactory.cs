using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;

namespace BlazorLocalization.Extractor.Adapters.Export;

/// <summary>
/// Routes an <see cref="ExportFormat"/> to the matching <see cref="ITranslationExporter"/> implementation.
/// </summary>
internal static class ExporterFactory
{
    public static ITranslationExporter Create(ExportFormat format) => format switch
    {
        ExportFormat.I18Next => new I18NextJsonExporter(),
        ExportFormat.Json => new GenericJsonExporter(),
        ExportFormat.Po => new PoExporter(),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown export format")
    };

    public static string GetFileExtension(ExportFormat format) => format switch
    {
        ExportFormat.I18Next => ".i18next.json",
        ExportFormat.Json => ".json",
        ExportFormat.Po => ".po",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown export format")
    };
}
