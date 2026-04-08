using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// How the IStringLocalizer was accessed at this call site.
/// </summary>
internal enum CallKind
{
    /// <summary><c>localizer["key"]</c></summary>
    Indexer,

    /// <summary><c>localizer.GetString("key")</c> or similar instance method.</summary>
    MethodCall,

    /// <summary><c>localizer.Translation("key", "msg")</c> — extension method, returns a builder.</summary>
    ExtensionMethod
}

/// <summary>
/// A single extracted argument from a call site, with its resolved value.
/// </summary>
internal sealed record ScannedArgument(
    int Position,
    string? ParameterName,
    OperationValue Value);

/// <summary>
/// A raw call site detected by the walker. Adapter-internal — never crosses the port boundary.
/// The inspect command consumes this directly for diagnostic detail.
/// </summary>
internal sealed record ScannedCallSite(
    string MethodName,
    string ContainingTypeName,
    CallKind CallKind,
    SourceFilePath File,
    int Line,
    IReadOnlyList<ScannedArgument> Arguments,
    IReadOnlyList<FluentChainWalker.ChainLink>? Chain)
{
    /// <summary>
    /// Whether this call provides source text (Definition) or just uses a key (Reference).
    /// Definitions: .Translation() returning a builder, DefineXxx() factories.
    /// References: indexer access, GetString(), or anything without a builder return type.
    /// </summary>
    public bool IsDefinition => CallKind == CallKind.ExtensionMethod &&
        (MethodName == "Translation" || MethodName.StartsWith("Define", StringComparison.Ordinal));
}
