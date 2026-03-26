using System.ComponentModel;

namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// Outcome of Roslyn overload resolution for a method call.
/// </summary>
public enum OverloadResolutionStatus
{
    [Description("Roslyn resolved the symbol directly")]
    Resolved,

    [Description("Multiple overload candidates matched — picked by argument count")]
    Ambiguous,

    [Description("Resolution failed (missing types) — guessed by argument count")]
    BestCandidate
}
