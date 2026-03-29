using System.ComponentModel;
using BlazorLocalization.Extractor.Domain;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Cli.Commands;

/// <summary>
/// Shared CLI settings for path discovery and output options.
/// </summary>
public class SharedSettings : CommandSettings
{
	[Description("Directories or .csproj files to scan. Repeatable. Defaults to current directory")]
	[CommandArgument(0, "[paths]")]
	public string[] Paths { get; init; } = ["."];

	[Description("Path style for source references. 'relative' (default) emits paths relative to the project root. 'absolute' preserves full filesystem paths")]
	[CommandOption("--path-style")]
	[DefaultValue(PathStyle.Relative)]
	public PathStyle PathStyle { get; init; } = PathStyle.Relative;

	[Description("Filter to specific locales. Repeatable (-l da -l es-MX). Omit to include all discovered locales")]
	[CommandOption("-l|--locale")]
	public string[]? Locales { get; init; }

	[Description("Export source-language entries only; skip per-locale translations")]
	[CommandOption("--source-only")]
	[DefaultValue(false)]
	public bool SourceOnly { get; init; }
}
