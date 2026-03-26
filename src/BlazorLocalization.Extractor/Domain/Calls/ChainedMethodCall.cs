namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// A single method call in a fluent builder chain following a <c>Translation()</c> invocation,
/// e.g. <c>.One("…")</c>, <c>.For("da")</c>.
/// </summary>
public sealed record ChainedMethodCall(
    string MethodName,
    IReadOnlyList<ResolvedArgument> Arguments);
