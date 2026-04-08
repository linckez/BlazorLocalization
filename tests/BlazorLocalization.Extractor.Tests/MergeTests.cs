using BlazorLocalization.Extractor.Application;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class MergeTests
{
	private static DefinitionSite DefSite(string file, int line, DefinitionKind kind = DefinitionKind.InlineTranslation) =>
		new(new SourceFilePath(file, "/test/TestProject"), line, kind);

	private static ReferenceSite RefSite(string file, int line) =>
		new(new SourceFilePath(file, "/test/TestProject"), line);

	private record TestScannerOutput(
		IReadOnlyList<TranslationDefinition> Definitions,
		IReadOnlyList<TranslationReference> References,
		IReadOnlyList<ScanDiagnostic> Diagnostics) : IScannerOutput;

	private static IScannerOutput Output(
		IReadOnlyList<TranslationDefinition>? defs = null,
		IReadOnlyList<TranslationReference>? refs = null) =>
		new TestScannerOutput(defs ?? [], refs ?? [], []);

	[Fact]
	public void SameKey_SameText_MergesDefinitions_NoConflict()
	{
		var output = Output(defs:
		[
			new("Key1", new SingularText("Hello"), DefSite("A.cs", 1)),
			new("Key1", new SingularText("Hello"), DefSite("B.cs", 5))
		]);

		var result = TranslationPipeline.Run([output]);

		result.Entries.Should().ContainSingle()
			.Which.Definitions.Should().HaveCount(2);
		result.Conflicts.Should().BeEmpty();
	}

	[Fact]
	public void SameKey_DifferentText_DetectsConflict()
	{
		var output = Output(defs:
		[
			new("Key1", new SingularText("Hello"), DefSite("A.cs", 1)),
			new("Key1", new SingularText("Goodbye"), DefSite("B.cs", 5))
		]);

		var result = TranslationPipeline.Run([output]);

		result.Entries.Should().ContainSingle();
		result.Conflicts.Should().ContainSingle()
			.Which.Key.Should().Be("Key1");
		result.Conflicts[0].Values.Should().HaveCount(2);
	}

	[Fact]
	public void Definition_PlusReference_DefinitionWins()
	{
		var output = Output(
			defs: [new("Key1", new SingularText("Real text"), DefSite("Translation.cs", 10))],
			refs: [new("Key1", true, RefSite("Indexer.cs", 1))]);

		var result = TranslationPipeline.Run([output]);

		result.Entries.Should().ContainSingle()
			.Which.SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Real text");
		result.Conflicts.Should().BeEmpty();
	}
}
