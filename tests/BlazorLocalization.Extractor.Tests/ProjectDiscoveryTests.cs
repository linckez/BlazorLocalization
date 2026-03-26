using BlazorLocalization.Extractor.Scanning;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class ProjectDiscoveryTests : IDisposable
{
	private readonly string _root = Directory.CreateTempSubdirectory("discovery-test-").FullName;

	public void Dispose() => Directory.Delete(_root, recursive: true);

	private void Touch(string relativePath)
	{
		var full = Path.Combine(_root, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(full)!);
		File.WriteAllText(full, "");
	}

	[Fact]
	public void Discover_FindsNestedCsproj()
	{
		Touch("src/WebApp/WebApp.csproj");

		var result = ProjectDiscovery.Discover(_root);

		result.Should().ContainSingle()
			.Which.Should().EndWith(Path.Combine("src", "WebApp"));
	}

	[Fact]
	public void Discover_SkipsObjAndBinDirs()
	{
		Touch("src/Real/Real.csproj");
		Touch("obj/Fake/Fake.csproj");
		Touch("src/Real/bin/Debug/Copied.csproj");

		var result = ProjectDiscovery.Discover(_root);

		result.Should().ContainSingle()
			.Which.Should().EndWith(Path.Combine("src", "Real"));
	}

	[Fact]
	public void Discover_MultipleCsproj_DistinctDirs()
	{
		Touch("src/ProjectA/A.csproj");
		Touch("src/ProjectB/B.csproj");

		var result = ProjectDiscovery.Discover(_root);

		result.Should().HaveCount(2);
		result.Should().Contain(d => d.EndsWith("ProjectA"));
		result.Should().Contain(d => d.EndsWith("ProjectB"));
	}
}
