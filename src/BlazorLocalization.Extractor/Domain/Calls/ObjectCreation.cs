namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// A constructor invocation detected inside a method argument,
/// e.g. <c>new PluralSourceText("one", "other")</c>.
/// </summary>
public sealed record ObjectCreation(
    string TypeName,
    IReadOnlyList<ResolvedArgument> ConstructorArguments);
