using System.Text.Json;
using BlazorLocalization.Extractor.Cli.Commands;
using FluentAssertions;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Runs the real CLI commands in-process against SampleBlazorApp.
/// Under dotnet test, <c>Console.IsOutputRedirected</c> is true — the CLI auto-switches
/// to JSON stdout when no <c>--output</c> is set. File-output tests pass <c>--output</c>,
/// which takes precedence over pipe detection.
///
/// Tests in this class must NOT run in parallel because <see cref="RunCapturingStdout"/>
/// redirects <c>Console.Out</c>, which is global state.
/// </summary>
[Collection("CliSmokeTests")]
public class CliSmokeTests : IDisposable
{
	private static readonly string SampleAppDir = ResolveSampleAppDir();
	private readonly string _tempDir = Directory.CreateTempSubdirectory("cli-smoke-").FullName;

	public void Dispose() => Directory.Delete(_tempDir, recursive: true);

	private static CommandApp BuildApp()
	{
		var app = new CommandApp();
		app.Configure(config =>
		{
			config.AddCommand<ExtractCommand>("extract");
			config.AddCommand<InspectCommand>("inspect");
		});
		return app;
	}

	/// <summary>
	/// Captures stdout by temporarily redirecting Console.Out.
	/// Note: the StringWriter is intentionally NOT disposed because Spectre.Console's
	/// static AnsiConsole may cache a reference to it. Disposing would cause
	/// ObjectDisposedException in later tests.
	/// </summary>
	private static (string Output, int ExitCode) RunCapturingStdout(string[] args)
	{
		var originalOut = Console.Out;
		var writer = new StringWriter();
		Console.SetOut(writer);
		try
		{
			var exitCode = BuildApp().Run(args);
			return (writer.ToString(), exitCode);
		}
		finally
		{
			Console.SetOut(originalOut);
		}
	}

	[Fact]
	public void Extract_ToStdout_ProducesValidJson()
	{
		var (output, exitCode) = RunCapturingStdout(["extract", SampleAppDir]);

		exitCode.Should().Be(0);
		output.Should().NotBeNullOrWhiteSpace();

		var action = () => JsonDocument.Parse(output);
		action.Should().NotThrow();
	}

	[Fact]
	public void Extract_WithOutputDir_WritesFiles()
	{
		// --output takes precedence over piped-stdout JSON mode
		var exitCode = BuildApp().Run(["extract", SampleAppDir, "-f", "po", "-o", _tempDir]);

		exitCode.Should().Be(0);

		var poFiles = Directory.GetFiles(_tempDir, "*.po");
		poFiles.Should().NotBeEmpty();
	}

	[Fact]
	public void Inspect_ProducesJsonWithCallsAndEntries()
	{
		var (output, exitCode) = RunCapturingStdout(["inspect", SampleAppDir]);

		exitCode.Should().Be(0);

		using var doc = JsonDocument.Parse(output);
		doc.RootElement.TryGetProperty("calls", out _).Should().BeTrue();
		doc.RootElement.TryGetProperty("entries", out _).Should().BeTrue();
	}

	[Fact]
	public void Extract_NonexistentPath_ReturnsError()
	{
		var (_, exitCode) = RunCapturingStdout(["extract", "/nonexistent/path/that/does/not/exist"]);

		exitCode.Should().Be(1);
	}

	[Fact]
	public void Extract_DirectoryOutput_WritesPerLocaleFiles()
	{
		// Per-locale files are now exported by default with directory output
		var exitCode = BuildApp().Run(
			["extract", SampleAppDir, "-f", "i18next", "-o", _tempDir]);

		exitCode.Should().Be(0);

		var files = Directory.GetFiles(_tempDir, "*.i18next.json");

		// At minimum: main file + da + es-MX locale files
		files.Should().HaveCountGreaterThanOrEqualTo(3);
		files.Should().Contain(f => Path.GetFileName(f).Contains(".da."));
		files.Should().Contain(f => Path.GetFileName(f).Contains(".es-MX."));
	}

	[Fact]
	public void Extract_SourceOnly_SuppressesPerLocaleFiles()
	{
		var exitCode = BuildApp().Run(
			["extract", SampleAppDir, "-f", "i18next", "-o", _tempDir, "--source-only"]);

		exitCode.Should().Be(0);

		var files = Directory.GetFiles(_tempDir, "*.i18next.json");

		// Only main project file(s), no locale-specific files
		files.Should().NotContain(f => Path.GetFileName(f).Contains(".da."));
		files.Should().NotContain(f => Path.GetFileName(f).Contains(".es-MX."));
	}

	[Fact]
	public void Extract_SourceOnlyWithLocale_ReturnsError()
	{
		var (_, exitCode) = RunCapturingStdout(
			["extract", SampleAppDir, "-f", "i18next", "-o", _tempDir, "--source-only", "-l", "da"]);

		exitCode.Should().Be(1);
	}

	[Fact]
	public void Extract_ToStdout_Po_ProducesPoOutput()
	{
		var (output, exitCode) = RunCapturingStdout(["extract", SampleAppDir, "-f", "po"]);

		exitCode.Should().Be(0);
		output.Should().Contain("msgid ");
		output.Should().Contain("msgstr ");
	}

	[Fact]
	public void Extract_ToStdout_SingleLocale_ProducesLocaleData()
	{
		var (output, exitCode) = RunCapturingStdout(["extract", SampleAppDir, "-l", "da"]);

		exitCode.Should().Be(0);
		output.Should().NotBeNullOrWhiteSpace();
		// i18next format: should be valid JSON with Danish translations
		var action = () => JsonDocument.Parse(output);
		action.Should().NotThrow();
	}

	[Fact]
	public void Extract_ToStdout_MultipleLocales_ReturnsError()
	{
		var (_, exitCode) = RunCapturingStdout(["extract", SampleAppDir, "-l", "da", "-l", "es-MX"]);

		exitCode.Should().Be(1);
	}

	[Fact]
	public void Extract_CsprojPath_Works()
	{
		var csproj = Directory.GetFiles(SampleAppDir, "*.csproj").First();
		var (output, exitCode) = RunCapturingStdout(["extract", csproj]);

		exitCode.Should().Be(0);
		output.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Extract_MultiplePositionalPaths()
	{
		var exitCode = BuildApp().Run(["extract", SampleAppDir, SampleAppDir, "-o", _tempDir]);

		exitCode.Should().Be(0);
		// Same path deduplicated — should produce files for one project
		Directory.GetFiles(_tempDir).Should().NotBeEmpty();
	}

	private static string ResolveSampleAppDir()
	{
		var dir = AppContext.BaseDirectory;
		while (dir is not null && !File.Exists(Path.Combine(dir, "BlazorLocalization.sln")))
			dir = Directory.GetParent(dir)?.FullName;

		return dir is not null
			? Path.Combine(dir, "tests", "SampleBlazorApp")
			: throw new InvalidOperationException("Cannot find repo root.");
	}
}
