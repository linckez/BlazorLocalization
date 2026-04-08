using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Walks Parent from a detected .Translation() call upward through the
/// fluent builder chain (.For(), .One(), .Other(), .When(), .Otherwise(), etc.).
/// Returns chain links in source order (innermost first → outermost last).
///
/// IOperation trees are inverted relative to source reading order.
/// In source we write:  .Translation("key", "msg").For("da", "dansk").For("de", "deutsch")
/// But in the IOperation tree, evaluation order rules — what executes first is deepest:
///
///   .For("de", ...)              ← outermost (Parent of .For("da"))
///     Instance: .For("da", ...)
///       Instance: .Translation(...)   ← deepest child, executes first
///
/// So to collect the chain from .Translation(), we walk UP via Parent.
/// </summary>
internal static class FluentChainWalker
{
    private static readonly HashSet<string> BuilderTypeNames =
    [
        ExtensionsContract.TypeSimpleBuilder,
        ExtensionsContract.TypePluralBuilder,
        ExtensionsContract.TypeSelectBuilder,
        ExtensionsContract.TypeSelectPluralBuilder,
        ExtensionsContract.TypeSimpleDefinition,
        ExtensionsContract.TypePluralDefinition,
        ExtensionsContract.TypeSelectDefinition,
        ExtensionsContract.TypeSelectPluralDefinition,
    ];

    public record ChainLink(string MethodName, IReadOnlyList<IArgumentOperation> Arguments);

    /// <summary>
    /// Starting from a detected IOperation (e.g. .Translation()), walk Parent
    /// collecting each IInvocationOperation whose containing type is a known builder.
    /// </summary>
    public static List<ChainLink> WalkChain(IOperation anchor)
    {
        var links = new List<ChainLink>();
        var current = anchor.Parent;

        while (current is IInvocationOperation invocation &&
               IsBuilderType(invocation.TargetMethod.ContainingType))
        {
            links.Add(new ChainLink(
                invocation.TargetMethod.Name,
                invocation.Arguments));
            current = invocation.Parent;
        }

        return links;
    }

    internal static bool IsBuilderType(INamedTypeSymbol? type)
    {
        if (type is null) return false;
        return BuilderTypeNames.Contains(type.Name);
    }
}
