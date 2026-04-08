namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Controls how source file paths are written in export output.
/// </summary>
public enum PathStyle
{
    /// <summary>Paths relative to the project root directory.</summary>
    Relative,

    /// <summary>Full absolute filesystem paths.</summary>
    Absolute
}
