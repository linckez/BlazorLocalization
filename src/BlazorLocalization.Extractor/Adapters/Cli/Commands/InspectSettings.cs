using System.ComponentModel;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// CLI settings for the <c>inspect</c> command.
/// </summary>
public sealed class InspectSettings : SharedSettings
{
    [Description("Output raw JSON to stdout instead of rendered tables. Auto-enabled when stdout is piped")]
    [CommandOption("--json")]
    [DefaultValue(false)]
    public bool Json { get; init; }

    [Description("Show full .resx tables for each language (default: summary only)")]
    [CommandOption("--show-resx-locales")]
    [DefaultValue(false)]
    public bool ShowResxLocales { get; init; }

    [Description("Show every line of code where a translation was found (default: warnings only)")]
    [CommandOption("--show-extracted-calls")]
    [DefaultValue(false)]
    public bool ShowExtractedCalls { get; init; }

    /// <summary>
    /// Whether output should be JSON — explicitly via <c>--json</c> or auto-detected when stdout is piped.
    /// </summary>
    public bool ShouldOutputJson => Json || Console.IsOutputRedirected;
}
