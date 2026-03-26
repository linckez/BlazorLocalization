namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// A fully resolved method call detected by the scanning engine,
/// carrying its source location, overload resolution outcome, and arguments.
/// </summary>
public sealed record ExtractedCall(
    string ContainingTypeName,
    string MethodName,
    CallKind CallKind,
    SourceLocation Location,
    OverloadResolutionStatus OverloadResolution,
    IReadOnlyList<ResolvedArgument> Arguments,
    IReadOnlyList<ChainedMethodCall>? FluentChain = null);
