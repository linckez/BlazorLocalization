using System.ComponentModel;
using BlazorLocalization.Extractor.Domain;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Cli.Commands;

/// <summary>
/// Shared CLI settings for path and project overrides.
/// </summary>
public class SharedSettings : CommandSettings
{
	[Description("Root directory of the solution or project to scan. Defaults to current directory")]
	[CommandArgument(0, "[path]")]
	public string Path { get; init; } = ".";

	[Description("One or more explicit project directories to scan. Repeatable (-p dir1 -p dir2). Skips automatic project discovery")]
	[CommandOption("-p|--project")]
	public string[]? Projects { get; init; }

	[Description("Output raw JSON to stdout instead of rendered tables. Auto-enabled when stdout is piped")]
	[CommandOption("--json")]
	[DefaultValue(false)]
	public bool Json { get; init; }

	[Description("Path style for source references. 'relative' (default) emits paths relative to the project root. 'absolute' preserves full filesystem paths")]
	[CommandOption("--paths")]
	[DefaultValue(PathStyle.Relative)]
	public PathStyle Paths { get; init; } = PathStyle.Relative;

	[Description("Filter to specific locales. Repeatable (-l da -l es-MX). Omit to include all discovered locales")]
	[CommandOption("-l|--locale")]
	public string[]? Locales { get; init; }

	[Description("Suppress inline locale output (.For() per-locale source texts). Show source-language entries only")]
	[CommandOption("--source-only")]
	[DefaultValue(false)]
	public bool SourceOnly { get; init; }

	/// <summary>
	/// Whether output should be JSON — explicitly via <c>--json</c> or auto-detected when stdout is piped.
	/// </summary>
	public bool ShouldOutputJson => Json || Console.IsOutputRedirected;
}
