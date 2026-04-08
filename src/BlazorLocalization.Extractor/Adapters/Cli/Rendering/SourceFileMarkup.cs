using BlazorLocalization.Extractor.Domain;
using Spectre.Console;

namespace BlazorLocalization.Extractor.Adapters.Cli.Rendering;

/// <summary>
/// Spectre.Console markup helpers for source file paths.
/// Generates OSC 8 clickable <c>file:///</c> links for terminals that support them.
/// </summary>
internal static class SourceFileMarkup
{
	public static string DisplayMarkup(this SourceFilePath file, PathStyle style, int? line = null)
	{
		var display = file.Display(style);
		var displayWithLine = line is not null ? $"{display}:{line}" : display;
		var uri = $"file:///{file.AbsolutePath.TrimStart('/')}";
		return $"[link={Markup.Escape(uri)}][cyan]{Markup.Escape(displayWithLine)}[/][/]";
	}

	public static string DisplayMarkup(this DefinitionSite site, PathStyle style) =>
		site.File.DisplayMarkup(style, site.Line);

	public static string DisplayMarkup(this ReferenceSite site, PathStyle style) =>
		site.File.DisplayMarkup(style, site.Line);
}
