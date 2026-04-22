using System.ComponentModel;

namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// The export format for serializing translation data.
/// <c>[Description]</c> attributes serve as single source of truth for both <c>--help</c> and interactive wizard prompts.
/// </summary>
public enum ExportFormat
{
    /// <summary>Full-fidelity JSON array with all metadata.</summary>
    [Description("Generic JSON (full-fidelity debug export with all metadata)")]
    Json,

    /// <summary>Flat i18next JSON for Crowdin upload.</summary>
    [Description("Crowdin i18next JSON (flat key/value, plurals via _one/_other)")]
    I18Next,

    /// <summary>GNU Gettext PO template (.pot).</summary>
    [Description("GNU Gettext PO (with source references and translator comments)")]
    Po
}
