namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// A resolved argument in a method or constructor call, capturing
/// the raw source value, syntactic argument name, and resolved parameter name.
/// </summary>
public sealed record ResolvedArgument(
    int Position,
    string Value,
    bool IsLiteral,
    string? ArgumentName,
    string? ParameterName,
    ObjectCreation? ObjectCreation = null);
