using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Requests;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class InspectRequestTests
{
	private static InspectRequest MakeRequest(
		IReadOnlyList<string>? projectDirs = null,
		HashSet<string>? localeFilter = null,
		bool sourceOnly = false) =>
		new(
			ProjectDirs: projectDirs ?? ["/proj"],
			JsonOutput: false,
			LocaleFilter: localeFilter,
			SourceOnly: sourceOnly,
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
			.Which.Should().Contain("No .csproj projects found");
	}

	[Fact]
	public void SourceOnly_WithLocale_ReturnsError()
	{
		var request = MakeRequest(
			sourceOnly: true,
			localeFilter: new(StringComparer.OrdinalIgnoreCase) { "da" });

		request.Validate().Should().ContainSingle()
			.Which.Should().Contain("--source-only").And.Contain("--locale");
	}

	[Fact]
	public void MultipleProjects_Valid()
	{
		var request = MakeRequest(projectDirs: ["/proj1", "/proj2"]);

		request.Validate().Should().BeEmpty();
	}
}
