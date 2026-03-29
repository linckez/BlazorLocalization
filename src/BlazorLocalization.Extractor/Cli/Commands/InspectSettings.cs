using System.ComponentModel;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Cli.Commands;

/// <summary>
/// CLI settings for the <c>inspect</c> command.
/// </summary>
public sealed class InspectSettings : SharedSettings
{
	[Description("Output raw JSON to stdout instead of rendered tables. Auto-enabled when stdout is piped")]
	[CommandOption("--json")]
	[DefaultValue(false)]
	public bool Json { get; init; }

	/// <summary>
	/// Whether output should be JSON — explicitly via <c>--json</c> or auto-detected when stdout is piped.
	/// </summary>
	public bool ShouldOutputJson => Json || Console.IsOutputRedirected;
}
