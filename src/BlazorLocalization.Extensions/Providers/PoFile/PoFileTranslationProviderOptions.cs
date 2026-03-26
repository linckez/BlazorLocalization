namespace BlazorLocalization.Extensions.Providers.PoFile;

/// <summary>
/// Configuration for the PO file translation provider.
/// </summary>
public sealed class PoFileTranslationProviderOptions
{
    /// <summary>
    /// Directory containing translation PO files.
    /// Can be absolute or relative to the application's content root.
    /// </summary>
    public required string TranslationsPath { get; set; }

    /// <summary>
    /// File naming pattern. <c>{culture}</c> is replaced with the requested culture name
    /// (e.g. <c>da</c>, <c>es-MX</c>).
    /// </summary>
    public string FilePattern { get; set; } = "{culture}.po";
}
