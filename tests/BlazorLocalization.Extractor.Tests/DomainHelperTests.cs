using BlazorLocalization.Extractor.Application;
using BlazorLocalization.Extractor.Domain;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class LocaleDiscoveryTests
{
	private static MergedTranslation Entry(
		string key,
		IReadOnlyDictionary<string, TranslationSourceText>? inlineTranslations = null) =>
		new(key, new SingularText("Hello"),
			[new DefinitionSite(new SourceFilePath("/test/TestProject/A.cs", "/test/TestProject"), 1, DefinitionKind.InlineTranslation)],
			[], inlineTranslations);

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

public class SourceFilePathTests
{
	[Fact]
	public void RelativePath_ProducesForwardSlashes()
	{
		var sfp = new SourceFilePath("/project/src/File.cs", "/project");

		sfp.RelativePath.Should().Be("src/File.cs");
	}

	[Fact]
	public void Display_Relative_ReturnsRelativePath()
	{
		var sfp = new SourceFilePath("/project/src/File.cs", "/project");

		sfp.Display(PathStyle.Relative).Should().Be("src/File.cs");
	}

	[Fact]
	public void Display_Absolute_ReturnsAbsolutePath()
	{
		var sfp = new SourceFilePath("/project/src/File.cs", "/project");

		sfp.Display(PathStyle.Absolute).Should().Be("/project/src/File.cs");
	}

	[Fact]
	public void IsResx_TrueForResxFiles()
	{
		new SourceFilePath("/p/Home.resx", "/p").IsResx.Should().BeTrue();
		new SourceFilePath("/p/Home.da.resx", "/p").IsResx.Should().BeTrue();
	}

	[Fact]
	public void IsResx_FalseForNonResxFiles()
	{
		new SourceFilePath("/p/Home.razor", "/p").IsResx.Should().BeFalse();
		new SourceFilePath("/p/Home.cs", "/p").IsResx.Should().BeFalse();
	}


	[Fact]
	public void ProjectName_DerivedFromProjectDir()
	{
		var sfp = new SourceFilePath("/home/user/MyApp/File.cs", "/home/user/MyApp");

		sfp.ProjectName.Should().Be("MyApp");
	}

	[Fact]
	public void FileName_ReturnsJustFileName()
	{
		var sfp = new SourceFilePath("/project/src/Components/Home.razor", "/project");

		sfp.FileName.Should().Be("Home.razor");
	}
}
