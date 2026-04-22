using System.Collections.Immutable;
using BlazorLocalization.Analyzers.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BlazorLocalization.Analyzers.Analyzers;

/// <summary>
/// BL0004: Flags IStringLocalizer&lt;T&gt; at injection points when ProviderBasedStringLocalizerFactory
/// is in the compilation. The type parameter has no scoping effect — all localizers share one flat cache.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenericLocalizerAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.RedundantTypeParameter);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var blSymbols = new BlazorLocalizationSymbols(compilationContext.Compilation);

            // Guard: only fire when ProviderBasedStringLocalizerFactory is in the compilation
            if (blSymbols.ProviderBasedFactoryType is null)
                return;

            var msSymbols = new MicrosoftLocalizationSymbols(compilationContext.Compilation);

            // Guard: need the generic IStringLocalizer<T> type
            if (msSymbols.IStringLocalizerGenericType is null)
                return;

            var injectAttributeType = compilationContext.Compilation.GetTypeByMetadataName(
                "Microsoft.AspNetCore.Components.InjectAttribute");

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeGenericName(ctx, msSymbols, injectAttributeType),
                SyntaxKind.GenericName);
        });
    }

    private static void AnalyzeGenericName(
        SyntaxNodeAnalysisContext context,
        MicrosoftLocalizationSymbols msSymbols,
        INamedTypeSymbol? injectAttributeType)
    {
        var genericName = (GenericNameSyntax)context.Node;

        // Quick syntax filter: identifier must be "IStringLocalizer" with exactly 1 type argument
        if (genericName.Identifier.ValueText != MicrosoftLocalizationSymbols.IStringLocalizerTypeName)
            return;

        if (genericName.TypeArgumentList.Arguments.Count != 1)
            return;

        // Skip open type parameters (e.g. IStringLocalizer<T> in a generic class)
        var typeArg = genericName.TypeArgumentList.Arguments[0];
        var typeArgInfo = context.SemanticModel.GetTypeInfo(typeArg, context.CancellationToken);
        if (typeArgInfo.Type is ITypeParameterSymbol)
            return;

        // Semantic confirmation: must resolve to Microsoft.Extensions.Localization.IStringLocalizer<T>
        var symbolInfo = context.SemanticModel.GetSymbolInfo(genericName, context.CancellationToken);
        if (symbolInfo.Symbol is not INamedTypeSymbol namedType)
            return;

        if (!SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, msSymbols.IStringLocalizerGenericType))
            return;

        // Context check: only flag at injection points (ctor params, [Inject] properties)
        if (!IsAtInjectionPoint(genericName, context, injectAttributeType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.RedundantTypeParameter,
                genericName.GetLocation(),
                typeArg.ToString()));
    }

    private static bool IsAtInjectionPoint(
        GenericNameSyntax genericName,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? injectAttributeType)
    {
        foreach (var ancestor in genericName.Ancestors())
        {
            switch (ancestor)
            {
                case ParameterSyntax parameter:
                    return IsConstructorParameter(parameter);

                case PropertyDeclarationSyntax property:
                    return HasInjectAttribute(property, context, injectAttributeType);

                // Stop walking at member boundaries that aren't handled above
                case MethodDeclarationSyntax:
                case FieldDeclarationSyntax:
                case EventDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    private static bool IsConstructorParameter(ParameterSyntax parameter)
    {
        if (parameter.Parent is not ParameterListSyntax parameterList)
            return false;

        return parameterList.Parent is ConstructorDeclarationSyntax
            or ClassDeclarationSyntax
            or RecordDeclarationSyntax
            or StructDeclarationSyntax;
    }

    private static bool HasInjectAttribute(
        PropertyDeclarationSyntax property,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? injectAttributeType)
    {
        if (injectAttributeType is null)
            return false;

        foreach (var attributeList in property.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
                    is IMethodSymbol ctor
                    && SymbolEqualityComparer.Default.Equals(ctor.ContainingType, injectAttributeType))
                    return true;
            }
        }

        return false;
    }
}
