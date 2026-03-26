using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class MergeTests
{
	private static SourceReference Source(string file, int line) =>
		new(file, line, "TestProject", null);

	[Fact]
	public void SameKey_SameText_MergesSources_NoConflict()
	{
		var entries = new List<TranslationEntry>
		{
			new("Key1", new SingularText("Hello"), Source("A.cs", 1)),
			new("Key1", new SingularText("Hello"), Source("B.cs", 5))
		};

		var result = MergedTranslationEntry.FromRaw(entries);

		result.Entries.Should().ContainSingle()
			.Which.Sources.Should().HaveCount(2);
		result.Conflicts.Should().BeEmpty();
	}

	[Fact]
	public void SameKey_DifferentText_DetectsConflict()
	{
		var entries = new List<TranslationEntry>
		{
			new("Key1", new SingularText("Hello"), Source("A.cs", 1)),
			new("Key1", new SingularText("Goodbye"), Source("B.cs", 5))
		};

		var result = MergedTranslationEntry.FromRaw(entries);

		result.Entries.Should().ContainSingle();
		result.Conflicts.Should().ContainSingle()
			.Which.Key.Should().Be("Key1");
		result.Conflicts[0].Values.Should().HaveCount(2);
	}

	[Fact]
	public void Definition_PlusReference_DefinitionWins()
	{
		var entries = new List<TranslationEntry>
		{
			new("Key1", null, Source("Indexer.cs", 1)),
			new("Key1", new SingularText("Real text"), Source("Translation.cs", 10))
		};

		var result = MergedTranslationEntry.FromRaw(entries);

		result.Entries.Should().ContainSingle()
			.Which.SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Real text");
		result.Conflicts.Should().BeEmpty();
	}
}
