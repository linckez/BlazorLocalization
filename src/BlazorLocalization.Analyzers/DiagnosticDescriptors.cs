using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Analyzers;

internal static class DiagnosticDescriptors
{
    // Categories
    private const string Correctness = "Correctness";
    private const string Migration = "Migration";
    private const string Awareness = "Awareness";

    // BL0001 — Empty translation key
    public static readonly DiagnosticDescriptor EmptyKey = new(
        id: "BL0001",
        title: "Translation key must not be empty",
        messageFormat: "Translation key must not be empty",
        category: Correctness,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/linckez/BlazorLocalization/blob/main/docs/analyzers/BL0001-empty-key.md");

    // BL0002 — Use Translation() API instead of GetString/indexer
    public static readonly DiagnosticDescriptor UseTranslationApi = new(
        id: "BL0002",
        title: "Consider using Translation() API",
        messageFormat: "'{0}' can be replaced with Translation() for source-text fallback and named placeholders",
        category: Migration,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/linckez/BlazorLocalization/blob/main/docs/analyzers/BL0002-use-translation-api.md");

    // BL0003 — Duplicate translation key
    public static readonly DiagnosticDescriptor DuplicateKey = new(
        id: "BL0003",
        title: "Duplicate translation key",
        messageFormat: "Translation key '{0}': {1}",
        category: Correctness,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/linckez/BlazorLocalization/blob/main/docs/analyzers/BL0003-duplicate-key.md");

    // BL0004 — IStringLocalizer<T> type parameter has no effect
    public static readonly DiagnosticDescriptor RedundantTypeParameter = new(
        id: "BL0004",
        title: "IStringLocalizer<T> type parameter has no scoping effect",
        messageFormat: "IStringLocalizer<{0}> type parameter has no effect because ProviderBasedStringLocalizerFactory ignores it — use IStringLocalizer instead",
        category: Awareness,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/linckez/BlazorLocalization/blob/main/docs/analyzers/BL0004-unscoped-generic.md");

    // BL0005 — Undefined translation key (key-only call with no matching definition)
    public static readonly DiagnosticDescriptor UndefinedKey = new(
        id: "BL0005",
        title: "Undefined translation key",
        messageFormat: "Translation key '{0}' has no definition — add a Translation(key, message) or Define*() call, or the key will resolve only at runtime",
        category: Correctness,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd],
        helpLinkUri: "https://github.com/linckez/BlazorLocalization/blob/main/docs/analyzers/BL0005-undefined-key.md");

    // BL0006 — Translation file conflict (same key, different values across resx/po/json files)
    public static readonly DiagnosticDescriptor TranslationFileConflict = new(
        id: "BL0006",
        title: "Translation file conflict",
        messageFormat: "Key '{0}' has conflicting {1} across translation files: {2}",
        category: Correctness,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd],
        helpLinkUri: "https://github.com/linckez/BlazorLocalization/blob/main/docs/analyzers/BL0006-translation-file-conflict.md");
}
