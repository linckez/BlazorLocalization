using System.ComponentModel;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// CLI settings for the <c>migrate</c> command.
/// </summary>
public sealed class MigrateSettings : SharedSettings
{
	[Description("Write changes to your .razor files. Without this flag, changes are previewed only")]
	[CommandOption("--apply")]
	[DefaultValue(false)]
	public bool Apply { get; init; }
}
