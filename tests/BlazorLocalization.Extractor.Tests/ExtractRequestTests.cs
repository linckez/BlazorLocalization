using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Requests;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class ExtractRequestTests
{
	private static ExtractRequest MakeRequest(
		IReadOnlyList<string>? projectDirs = null,
		OutputTarget? output = null,
		HashSet<string>? localeFilter = null,
		bool sourceOnly = false) =>
		new(
			ProjectDirs: projectDirs ?? ["/proj"],
			Format: ExportFormat.I18Next,
			Output: output ?? OutputTarget.Stdout,
			LocaleFilter: localeFilter,
			SourceOnly: sourceOnly,
			PathStyle: PathStyle.Relative,
			Verbose: false,
			ExitOnDuplicateKey: false,
			OnDuplicateKey: ConflictStrategy.First);

	[Fact]
	public void Valid_SingleProject_Stdout_NoLocale_ReturnsNoErrors()
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
	public void Stdout_MultipleProjects_ReturnsError()
	{
		var request = MakeRequest(projectDirs: ["/proj1", "/proj2"]);

		request.Validate().Should().ContainSingle()
			.Which.Should().Contain("Multiple projects");
	}

	[Fact]
	public void Stdout_MultipleLocales_ReturnsError()
	{
		var request = MakeRequest(
			localeFilter: new(StringComparer.OrdinalIgnoreCase) { "da", "es-MX" });

		request.Validate().Should().ContainSingle()
			.Which.Should().Contain("one locale at a time");
	}

	[Fact]
	public void Stdout_SingleLocale_Valid()
	{
		var request = MakeRequest(
			localeFilter: new(StringComparer.OrdinalIgnoreCase) { "da" });

		request.Validate().Should().BeEmpty();
	}

	[Fact]
	public void FileOutput_MultipleProjects_ReturnsError()
	{
		var request = MakeRequest(
			projectDirs: ["/proj1", "/proj2"],
			output: OutputTarget.File("out.po"));

		request.Validate().Should().ContainSingle()
			.Which.Should().Contain("Multiple projects require -o <dir>");
	}

	[Fact]
	public void DirOutput_MultipleProjects_Valid()
	{
		var request = MakeRequest(
			projectDirs: ["/proj1", "/proj2"],
			output: OutputTarget.Dir("./out"));

		request.Validate().Should().BeEmpty();
	}

	[Fact]
	public void MultipleErrors_AllReported()
	{
		var request = MakeRequest(
			projectDirs: [],
			sourceOnly: true,
			localeFilter: new(StringComparer.OrdinalIgnoreCase) { "da" });

		request.Validate().Should().HaveCount(2);
	}
}

public class OutputTargetTests
{
	[Fact]
	public void Null_ReturnsStdout()
	{
		OutputTarget.FromRawOutput(null).Should().Be(OutputTarget.Stdout);
	}

	[Fact]
	public void PathWithExtension_ReturnsFile()
	{
		var result = OutputTarget.FromRawOutput("out.po");

		result.Should().BeOfType<OutputTarget.FileTarget>()
			.Which.Path.Should().Be("out.po");
	}

	[Fact]
	public void PathWithoutExtension_ReturnsDir()
	{
		var result = OutputTarget.FromRawOutput("./translations");

		result.Should().BeOfType<OutputTarget.DirTarget>()
			.Which.Path.Should().Be("./translations");
	}
}
