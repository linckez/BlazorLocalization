using BlazorLocalization.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace SampleBlazorApp.Components.Pages;

public partial class EdgeCasePage
{
    // ── BL0004 — [Inject] property injection ────────────────────
    [Inject] private IStringLocalizer<EdgeCasePage> LocProp { get; set; } = default!;

    private string? _codeBehindGreeting;

    // ── BL0003 sub-case B — conflicting inline messages ─────────
    private string _conflictA = string.Empty;
    private string _conflictB = string.Empty;

    // ── BL0003 — same key, same message → no diagnostic (reuse) ─
    private string _reuseA = string.Empty;
    private string _reuseB = string.Empty;

    // ── BL0001 — Empty key ──────────────────────────────────────
    private string EmptyViaGetString => Loc.GetString("");                                   // BL0001 + BL0002
    private string EmptyViaIndexer => Loc[""];                                               // BL0001 + BL0002
    private string EmptyViaTranslation => Loc.Translation(key: "", sourceMessage: "").ToString();  // BL0001 only

    // ── BL0002 — GetString/indexer code fix targets ─────────────
    private string ViaGetString => Loc.GetString("Edge.CodeBehind.Title");                   // BL0002
    private string ViaGetStringArgs => Loc.GetString("Edge.CodeBehind.Welcome", "World");    // BL0002 (HasArgs)
    private string ViaIndexer => Loc["Edge.CodeBehind.Subtitle"];                            // BL0002
    private string ViaIndexerArgs => Loc["Edge.CodeBehind.Greeting", "World"];               // BL0002 (HasArgs)

    // Correct — no BL0002
    private string AlreadyCorrect => Loc.Translation(key: "Edge.CodeBehind.Correct", sourceMessage: "OK").ToString();

    // ── Extract Translation Definition refactoring targets ──────
    private void TranslationsReadyForExtraction()
    {
        // Simple extraction
        var save = Loc.Translation(key: "Extract.Save", sourceMessage: "Save").ToString();

        // With replaceWith — preserved during extraction
        var greeting = Loc.Translation(
            key: "Extract.Greeting",
            sourceMessage: "Hello {name}!",
            replaceWith: new { name = "World" }).ToString();

        // Named args in reversed order
        var cancel = Loc.Translation(sourceMessage: "Cancel", key: "Extract.Cancel").ToString();
    }

    protected override void OnInitialized()
    {
        _codeBehindGreeting = Loc.Translation("CB.CodeBehind", "From code-behind").ToString();

        // BL0003 sub-case B — same key, different message
        _conflictA = Loc.Translation(key: "Edge.Inline.Conflict", sourceMessage: "Version A").ToString();  // BL0003
        _conflictB = Loc.Translation(key: "Edge.Inline.Conflict", sourceMessage: "Version B").ToString();  // BL0003

        // Reuse — same key, same message (no BL0003)
        _reuseA = Loc.Translation(key: "Edge.Inline.Reuse", sourceMessage: "Same text").ToString();
        _reuseB = Loc.Translation(key: "Edge.Inline.Reuse", sourceMessage: "Same text").ToString();
    }
}
