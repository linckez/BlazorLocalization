namespace BlazorLocalization.Extractor.Adapters.Cli.Commands;

/// <summary>
/// Validated request for the <c>migrate</c> command.
/// </summary>
public sealed record MigrateRequest(
    string ProjectDir,
    HashSet<string> LocaleFilter,
    bool Apply)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ProjectDir))
            errors.Add("No project to scan.");

        return errors;
    }
}
