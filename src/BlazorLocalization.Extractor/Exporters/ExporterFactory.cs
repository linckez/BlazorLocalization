using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Exporters;

/// <summary>
/// Maps <see cref="ExportFormat"/> to the corresponding <see cref="ITranslationExporter"/> implementation and file extension.
/// </summary>
public static class ExporterFactory
{
	private static readonly Dictionary<ExportFormat, string> FileExtensions = new()
	{
		[ExportFormat.Json] = ".json",
		[ExportFormat.I18Next] = ".i18next.json",
		[ExportFormat.Po] = ".po"
	};

	/// <summary>
	/// Creates the exporter implementation for the given <paramref name="format"/>.
	/// </summary>
	public static ITranslationExporter Create(ExportFormat format) =>
		format switch
		{
			ExportFormat.Json => new GenericJsonExporter(),
			ExportFormat.I18Next => new I18NextJsonExporter(),
			ExportFormat.Po => new PoExporter(),
			_ => throw new InvalidOperationException($"Unknown format: {format}")
		};

	/// <summary>
	/// Returns the file extension (including leading dot) for the given <paramref name="format"/>.
	/// </summary>
	public static string GetFileExtension(ExportFormat format) =>
		FileExtensions[format];
}
