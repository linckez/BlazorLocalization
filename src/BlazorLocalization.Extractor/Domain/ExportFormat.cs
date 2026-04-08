namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// The export format for serializing translation data.
/// </summary>
public enum ExportFormat
{
    /// <summary>Full-fidelity JSON array with all metadata.</summary>
    Json,

    /// <summary>Flat i18next JSON for Crowdin upload.</summary>
    I18Next,

    /// <summary>GNU Gettext PO template (.pot).</summary>
    Po
}
