using BlazorLocalization.Extractor.Adapters.Cli.Commands;
using BlazorLocalization.Extractor.Domain;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class InspectRequestTests
{
	private static InspectRequest MakeRequest(
		IReadOnlyList<string>? projectDirs = null,
		HashSet<string>? localeFilter = null) =>
		new(
			ProjectDirs: projectDirs ?? ["/proj"],
			JsonOutput: false,
			LocaleFilter: localeFilter,
			ShowResxLocales: false,
			ShowExtractedCalls: false,
			PathStyle: PathStyle.Relative);

	[Fact]
	public void Valid_SingleProject_ReturnsNoErrors()
	{
		var request = MakeRequest();

		request.Validate().Should().BeEmpty();
	}

	[Fact]
	public void NoProjects_ReturnsError()
	{
		var request = MakeRequest(projectDirs: []);

		request.Validate().Should().ContainSingle()
			.Which.Should().Contain("No projects to scan");
	}

	[Fact]
	public void MultipleProjects_Valid()
	{
		var request = MakeRequest(projectDirs: ["/proj1", "/proj2"]);

		request.Validate().Should().BeEmpty();
	}
}
