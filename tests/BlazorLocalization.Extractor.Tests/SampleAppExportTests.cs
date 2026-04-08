using BlazorLocalization.Extractor.Adapters.Export;
using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Full E2E export snapshots — runs each exporter on the real SampleBlazorApp merged entries.
/// Any change in scanning, merging, or formatting breaks these snapshots.
/// </summary>
public class SampleAppExportTests(SampleAppFixture fixture) : IClassFixture<SampleAppFixture>
{
	[Fact]
	public Task I18NextJson_FullOutput() =>
		Verify(new I18NextJsonExporter().Export(fixture.MergeResult.Entries.ToList(), PathStyle.Relative));

	[Fact]
	public Task Po_FullOutput() =>
		Verify(new PoExporter().Export(fixture.MergeResult.Entries.ToList(), PathStyle.Relative));

	/// <summary>
	/// Per-locale files should contain only entries that have a .For() translation for that locale —
	/// no fallback to the source text.
	/// </summary>
	[Fact]
	public Task I18NextJson_PerLocale_Da() =>
		Verify(ExportLocale("da"));

	[Fact]
	public Task I18NextJson_PerLocale_EsMx() =>
		Verify(ExportLocale("es-MX"));

	private string ExportLocale(string locale)
	{
		var entries = fixture.MergeResult.Entries
			.Where(e => e.InlineTranslations is not null && e.InlineTranslations.ContainsKey(locale))
			.Select(e => new MergedTranslation(e.Key, e.InlineTranslations![locale], e.Definitions, e.References))
			.ToList();

		return new I18NextJsonExporter().Export(entries, PathStyle.Relative);
	}
}
