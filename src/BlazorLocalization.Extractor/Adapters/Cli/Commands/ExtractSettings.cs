using System.ComponentModel;
using BlazorLocalization.Extractor.Domain;
using Spectre.Console.Cli;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// CLI settings for the <c>extract</c> command.
/// </summary>
public sealed class ExtractSettings : SharedSettings
{
    [Description("Export format: i18next (default, Crowdin i18next JSON), po (GNU Gettext with source refs), json (full-fidelity debug)")]
    [CommandOption("-f|--format")]
    [DefaultValue(ExportFormat.I18Next)]
    public ExportFormat Format { get; init; } = ExportFormat.I18Next;

    [Description("Output path: a file (e.g. out.po) or directory (e.g. ./translations). Auto-detected via extension. Omit to write to stdout")]
    [CommandOption("-o|--output")]
    public string? Output { get; init; }

    [Description("Print per-project scanning progress to stderr")]
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    [Description("Exit with error code 1 when duplicate translation keys are found (useful for CI pipelines)")]
    [CommandOption("--exit-on-duplicate-key")]
    [DefaultValue(false)]
    public bool ExitOnDuplicateKey { get; init; }

    [Description("Strategy for duplicate translation keys (same key, different source text). 'first' (default) keeps the first-seen source text. 'skip' omits the key entirely")]
    [CommandOption("--on-duplicate-key")]
    [DefaultValue(ConflictStrategy.First)]
    public ConflictStrategy OnDuplicateKey { get; init; } = ConflictStrategy.First;
}
