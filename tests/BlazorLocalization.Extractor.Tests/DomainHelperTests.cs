using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class LocaleDiscoveryTests
{
	private static SourceReference Source(string file, int line) =>
		new(file, line, "TestProject", null);

	private static MergedTranslationEntry Entry(
		string key,
		IReadOnlyDictionary<string, TranslationSourceText>? inlineTranslations = null) =>
		new(key, new SingularText("Hello"), [Source("A.cs", 1)], inlineTranslations);

	[Fact]
	public void DiscoverLocales_ReturnsEmpty_WhenNoInlineTranslations()
	{
		var entries = new[] { Entry("k1"), Entry("k2") };

		var result = LocaleDiscovery.DiscoverLocales(entries);

		result.Should().BeEmpty();
	}

	[Fact]
	public void DiscoverLocales_ReturnsSorted_Deduplicated()
	{
		var entries = new[]
		{
			Entry("k1", new Dictionary<string, TranslationSourceText>
			{
				["es-MX"] = new SingularText("Hola"),
				["da"] = new SingularText("Hej")
			}),
			Entry("k2", new Dictionary<string, TranslationSourceText>
			{
				["da"] = new SingularText("Hej igen"),
				["fr"] = new SingularText("Bonjour")
			})
		};

		var result = LocaleDiscovery.DiscoverLocales(entries);

		result.Should().Equal("da", "es-MX", "fr");
	}

	[Fact]
	public void DiscoverLocales_AppliesFilter()
	{
		var entries = new[]
		{
			Entry("k1", new Dictionary<string, TranslationSourceText>
			{
				["es-MX"] = new SingularText("Hola"),
				["da"] = new SingularText("Hej"),
				["fr"] = new SingularText("Bonjour")
			})
		};

		var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "da", "FR" };
		var result = LocaleDiscovery.DiscoverLocales(entries, filter);

		result.Should().Equal("da", "fr");
	}

	[Fact]
	public void DiscoverLocales_IsCaseInsensitive()
	{
		var entries = new[]
		{
			Entry("k1", new Dictionary<string, TranslationSourceText>
			{
				["DA"] = new SingularText("Hej")
			}),
			Entry("k2", new Dictionary<string, TranslationSourceText>
			{
				["da"] = new SingularText("Hej igen")
			})
		};

		var result = LocaleDiscovery.DiscoverLocales(entries);

		result.Should().HaveCount(1);
	}
}

public class RelativizeTests
{
	[Fact]
	public void SourceReference_Relativize_ProducesForwardSlashes()
	{
		var source = new SourceReference("/project/src/File.cs", 10, "MyProject", null);

		var result = source.Relativize("/project");

		result.FilePath.Should().Be("src/File.cs");
		result.Line.Should().Be(10);
		result.ProjectName.Should().Be("MyProject");
	}

	[Fact]
	public void SourceLocation_Relativize_ProducesForwardSlashes()
	{
		var loc = new SourceLocation("/project/src/File.cs", 10, "MyProject");

		var result = loc.Relativize("/project");

		result.FilePath.Should().Be("src/File.cs");
		result.Line.Should().Be(10);
	}

	[Fact]
	public void MergedTranslationEntry_RelativizeSources_AllSourcesRelativized()
	{
		var entry = new MergedTranslationEntry(
			"key1",
			new SingularText("Hello"),
			[
				new SourceReference("/project/src/A.cs", 1, "P", null),
				new SourceReference("/project/src/B.cs", 5, "P", null)
			]);

		var result = entry.RelativizeSources("/project");

		result.Sources.Select(s => s.FilePath).Should().Equal("src/A.cs", "src/B.cs");
		result.Key.Should().Be("key1");
	}

	[Fact]
	public void ExtractedCall_RelativizeLocation()
	{
		var call = new ExtractedCall(
			"MyClass",
			"GetString",
			CallKind.MethodInvocation,
			new SourceLocation("/project/src/File.cs", 42, "P"),
			OverloadResolutionStatus.Resolved,
			[]);

		var result = call.RelativizeLocation("/project");

		result.Location.FilePath.Should().Be("src/File.cs");
		result.Location.Line.Should().Be(42);
	}
}
