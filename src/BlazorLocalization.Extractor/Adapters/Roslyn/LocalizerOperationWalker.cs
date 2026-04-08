using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// OperationWalker that detects IStringLocalizer usage — both indexer access
/// (<c>localizer["key"]</c>) and method invocations (<c>localizer.GetString()</c>,
/// <c>localizer.Translation()</c>) — as well as <c>DefineXxx()</c> static factory calls
/// on <c>TranslationDefinitions</c>.
/// </summary>
internal sealed class LocalizerOperationWalker(
    INamedTypeSymbol targetInterface,
    INamedTypeSymbol? definitionFactory = null) : OperationWalker
{
    private readonly List<(IOperation Op, ISymbol Symbol, IReadOnlyList<IArgumentOperation> Arguments)> _results = [];

    public IReadOnlyList<(IOperation Op, ISymbol Symbol, IReadOnlyList<IArgumentOperation> Arguments)> Results => _results;

    public override void VisitPropertyReference(IPropertyReferenceOperation operation)
    {
        if (operation.Property.IsIndexer &&
            IsAssignableTo(operation.Instance?.Type, targetInterface))
        {
            _results.Add((operation, operation.Property, operation.Arguments));
        }

        base.VisitPropertyReference(operation);
    }

    public override void VisitInvocation(IInvocationOperation operation)
    {
        var receiverType = operation.Instance?.Type
                           ?? operation.TargetMethod.Parameters.FirstOrDefault()?.Type;

        if (IsAssignableTo(receiverType, targetInterface))
        {
            _results.Add((operation, operation.TargetMethod, operation.Arguments));
        }
        else if (definitionFactory is not null
                 && operation.Instance is null
                 && SymbolEqualityComparer.Default.Equals(
                     operation.TargetMethod.ContainingType, definitionFactory))
        {
            _results.Add((operation, operation.TargetMethod, operation.Arguments));
        }

        base.VisitInvocation(operation);
    }

    internal static bool IsAssignableTo(ITypeSymbol? type, INamedTypeSymbol targetInterface)
    {
        if (type == null) return false;
        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, targetInterface))
            return true;
        return type.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, targetInterface) ||
            SymbolEqualityComparer.Default.Equals(i, targetInterface));
    }
}
