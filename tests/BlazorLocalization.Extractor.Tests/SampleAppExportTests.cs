using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Exporters;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Full E2E export snapshots — runs each exporter on the real SampleBlazorApp merged entries.
/// Any change in scanning, merging, or formatting breaks these snapshots.
/// </summary>
public class SampleAppExportTests(SampleAppFixture fixture) : IClassFixture<SampleAppFixture>
{
	[Fact]
	public Task I18NextJson_FullOutput() =>
		Verify(new I18NextJsonExporter().Export(fixture.MergeResult.Entries.ToList()));

	[Fact]
	public Task Po_FullOutput() =>
		Verify(new PoExporter().Export(RelativizePaths(fixture.MergeResult.Entries)));

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
			.Select(e => new MergedTranslationEntry(e.Key, e.InlineTranslations![locale], e.Sources))
			.ToList();

		return new I18NextJsonExporter().Export(entries);
	}

	/// <summary>
	/// Converts absolute source paths to solution-relative forward-slash paths,
	/// mirroring what the CLI does with <c>PathStyle.Relative</c>.
	/// This avoids platform-dependent Verify path scrubbing issues.
	/// </summary>
	private List<MergedTranslationEntry> RelativizePaths(IReadOnlyList<MergedTranslationEntry> entries) =>
		entries.Select(e => e with
		{
			Sources = e.Sources.Select(s => s with
			{
				FilePath = Path.GetRelativePath(fixture.SolutionDirectory, s.FilePath).Replace('\\', '/')
			}).ToList()
		}).ToList();
}
