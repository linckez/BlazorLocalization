using System.ComponentModel;

namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Strategy for handling duplicate translation keys (same key, different source text).
/// <c>[Description]</c> attributes serve as single source of truth for both <c>--help</c> and interactive wizard prompts.
/// </summary>
public enum ConflictStrategy
{
    /// <summary>Keep the first-seen source text for the duplicate key.</summary>
    [Description("Keep the first-seen source text for the key")]
    First,

    /// <summary>Omit the duplicate key from the export entirely.</summary>
    [Description("Omit the duplicate key from the export")]
    Skip
}
