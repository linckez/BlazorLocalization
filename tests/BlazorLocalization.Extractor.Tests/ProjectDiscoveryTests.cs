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

	[Fact]
	public void ResolveAll_CsprojFile_ReturnsParentDir()
	{
		Touch("src/WebApp/WebApp.csproj");

		var (dirs, errors) = ProjectDiscovery.ResolveAll([Path.Combine(_root, "src/WebApp/WebApp.csproj")]);

		errors.Should().BeEmpty();
		dirs.Should().ContainSingle()
			.Which.Should().EndWith(Path.Combine("src", "WebApp"));
	}

	[Fact]
	public void ResolveAll_MissingCsproj_ReturnsError()
	{
		var (dirs, errors) = ProjectDiscovery.ResolveAll([Path.Combine(_root, "Missing.csproj")]);

		dirs.Should().BeEmpty();
		errors.Should().ContainSingle()
			.Which.Should().Contain("File not found");
	}

	[Fact]
	public void ResolveAll_MixedInputs_DirAndCsproj()
	{
		Touch("src/WebApp/WebApp.csproj");
		Touch("other/Api/Api.csproj");

		var (dirs, errors) = ProjectDiscovery.ResolveAll(
		[
			Path.Combine(_root, "src"),
			Path.Combine(_root, "other/Api/Api.csproj")
		]);

		errors.Should().BeEmpty();
		dirs.Should().HaveCount(2);
	}

	[Fact]
	public void ResolveAll_DeduplicatesSameProject()
	{
		Touch("src/WebApp/WebApp.csproj");

		var (dirs, _) = ProjectDiscovery.ResolveAll(
		[
			_root,
			Path.Combine(_root, "src/WebApp/WebApp.csproj")
		]);

		dirs.Should().ContainSingle();
	}

	[Fact]
	public void ResolveAll_NonExistentDir_ReturnsError()
	{
		var (dirs, errors) = ProjectDiscovery.ResolveAll(["/nonexistent/path"]);

		dirs.Should().BeEmpty();
		errors.Should().ContainSingle()
			.Which.Should().Contain("Not found");
	}

	[Fact]
	public void ResolveAll_EmptyDir_ReturnsError()
	{
		var (dirs, errors) = ProjectDiscovery.ResolveAll([_root]);

		dirs.Should().BeEmpty();
		errors.Should().ContainSingle()
			.Which.Should().Contain("No projects found");
	}
}
