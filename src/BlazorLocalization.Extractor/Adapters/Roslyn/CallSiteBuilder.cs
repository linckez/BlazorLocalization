using BlazorLocalization.Extractor.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Converts raw walker hits into structured <see cref="ScannedCallSite"/> records.
/// Extracts arguments via <see cref="ValueExtractor"/> and chains via <see cref="FluentChainWalker"/>.
/// </summary>
internal static class CallSiteBuilder
{
    public static ScannedCallSite Build(
        IOperation operation,
        ISymbol symbol,
        IReadOnlyList<IArgumentOperation> arguments,
        SourceFilePath file,
        int line)
    {
        var scannedArgs = ExtractArguments(arguments);

        return operation switch
        {
            IPropertyReferenceOperation propRef when propRef.Property.IsIndexer =>
                new ScannedCallSite(
                    MethodName: "this[]",
                    ContainingTypeName: propRef.Property.ContainingType?.Name ?? "?",
                    CallKind: CallKind.Indexer,
                    File: file,
                    Line: line,
                    Arguments: scannedArgs,
                    Chain: null),

            IInvocationOperation invocation =>
                BuildFromInvocation(invocation, scannedArgs, file, line),

            _ => new ScannedCallSite(
                MethodName: symbol.Name,
                ContainingTypeName: symbol.ContainingType?.Name ?? "?",
                CallKind: CallKind.MethodCall,
                File: file,
                Line: line,
                Arguments: scannedArgs,
                Chain: null)
        };
    }

    private static ScannedCallSite BuildFromInvocation(
        IInvocationOperation invocation,
        IReadOnlyList<ScannedArgument> arguments,
        SourceFilePath file,
        int line)
    {
        var method = invocation.TargetMethod;

        // Detect builder-returning calls (Translation() → SimpleBuilder, PluralBuilder, etc.)
        var returnType = method.ReturnType;
        var returnsBuilder = FluentChainWalker.IsBuilderType(returnType as INamedTypeSymbol);

        // A call is a "definition" if it returns a builder type OR is a DefineXxx factory
        var isDefinitionLike = returnsBuilder ||
            method.Name.StartsWith("Define", StringComparison.Ordinal);

        var callKind = isDefinitionLike ? CallKind.ExtensionMethod : CallKind.MethodCall;

        // Only collect fluent chain for builder-returning calls
        var chain = returnsBuilder ? FluentChainWalker.WalkChain(invocation) : null;
        if (chain is { Count: 0 }) chain = null;

        return new ScannedCallSite(
            MethodName: method.Name,
            ContainingTypeName: method.ContainingType?.Name ?? "?",
            CallKind: callKind,
            File: file,
            Line: line,
            Arguments: arguments,
            Chain: chain);
    }

    private static IReadOnlyList<ScannedArgument> ExtractArguments(IReadOnlyList<IArgumentOperation> arguments)
    {
        var result = new List<ScannedArgument>(arguments.Count);

        for (var i = 0; i < arguments.Count; i++)
        {
            var arg = arguments[i];
            var value = arg.Value.Accept(ValueExtractor.Instance, null)
                        ?? new OperationValue.Unrecognized(arg.Value.Kind, arg.Value.Syntax.ToString());

            result.Add(new ScannedArgument(
                Position: i,
                ParameterName: arg.Parameter?.Name,
                Value: value));
        }

        return result;
    }
}
