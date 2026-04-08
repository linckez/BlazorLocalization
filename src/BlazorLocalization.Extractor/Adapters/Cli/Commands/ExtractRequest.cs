using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// Validated request for the <c>extract</c> command.
/// Maps CLI settings into domain types with validation.
/// </summary>
public sealed record ExtractRequest(
    IReadOnlyList<string> ProjectDirs,
    ExportFormat Format,
    OutputTarget Output,
    HashSet<string>? LocaleFilter,
    bool SourceOnly,
    PathStyle PathStyle,
    bool Verbose,
    bool ExitOnDuplicateKey,
    ConflictStrategy OnDuplicateKey)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (ProjectDirs.Count == 0)
            errors.Add("No projects to scan.");

        if (SourceOnly && LocaleFilter is { Count: > 0 })
            errors.Add("--source-only and --locale cannot be used together.");

        if (Output is OutputTarget.StdoutTarget)
        {
            if (ProjectDirs.Count > 1)
                errors.Add("Stdout output supports only a single project. Use -o <dir> for multiple projects.");
        }

        if (Output is OutputTarget.FileTarget)
        {
            if (ProjectDirs.Count > 1)
                errors.Add("File output supports only a single project. Use -o <dir> for multiple projects.");
        }

        return errors;
    }
}

/// <summary>
/// Where extract output goes. Classified from the raw <c>-o</c> value.
/// </summary>
public abstract record OutputTarget
{
    public sealed record StdoutTarget : OutputTarget;
    public sealed record FileTarget(string Path) : OutputTarget;
    public sealed record DirTarget(string Path) : OutputTarget;

    /// <summary>
    /// Classifies the raw <c>-o</c> value: null → stdout, has extension → file, else → directory.
    /// </summary>
    public static OutputTarget FromRawOutput(string? raw)
    {
        if (raw is null)
            return new StdoutTarget();

        return Path.HasExtension(raw)
            ? new FileTarget(raw)
            : new DirTarget(raw);
    }
}
