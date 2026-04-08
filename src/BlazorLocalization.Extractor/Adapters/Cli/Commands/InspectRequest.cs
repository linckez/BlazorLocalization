using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// Validated request for the <c>inspect</c> command.
/// </summary>
public sealed record InspectRequest(
    IReadOnlyList<string> ProjectDirs,
    bool JsonOutput,
    HashSet<string>? LocaleFilter,
    bool ShowResxLocales,
    bool ShowExtractedCalls,
    PathStyle PathStyle)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (ProjectDirs.Count == 0)
            errors.Add("No projects to scan.");

        return errors;
    }
}
