using System.Collections.Immutable;
using BlazorLocalization.Analyzers.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BlazorLocalization.Analyzers.Analyzers;

/// <summary>
/// BL0001: Flags empty translation keys in Translation()/GetString()/indexer calls.
/// Uses semantic model to confirm the receiver is IStringLocalizer and resolves arguments
/// by parameter name via the method symbol.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyKeyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.EmptyKey);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var msSymbols = new MicrosoftLocalizationSymbols(compilationContext.Compilation);
            if (msSymbols.IStringLocalizerType is null)
                return;

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, msSymbols),
                SyntaxKind.InvocationExpression);

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeElementAccess(ctx, msSymbols),
                SyntaxKind.ElementAccessExpression);
        });
    }

    /// <summary>
    /// Handles: Translation(key: "", ...), GetString(""), GetString("", args)
    /// </summary>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, MicrosoftLocalizationSymbols msSymbols)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get method name from member access (e.g. Loc.Translation, Loc.GetString)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (methodName is not (BlazorLocalizationSymbols.TranslationMethodName or MicrosoftLocalizationSymbols.GetStringMethodName))
            return;

        // Semantic check: receiver must be IStringLocalizer
        var receiverType = context.SemanticModel.GetTypeInfo(
            memberAccess.Expression, context.CancellationToken).Type;
        if (!msSymbols.IsStringLocalizerType(receiverType))
            return;

        // Resolve the method symbol for parameter-name-based argument matching
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
            return;

        if (!TranslationCallExtractor.TryGetKeyFromInvocation(invocation, method, out var key, out var keyLocation)
            || key is null || keyLocation is null)
            return;

        if (key.Length == 0)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(DiagnosticDescriptors.EmptyKey, keyLocation));
        }
    }

    /// <summary>
    /// Handles: Loc[""], Loc["", args]
    /// </summary>
    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context, MicrosoftLocalizationSymbols msSymbols)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        // Semantic check: receiver must be IStringLocalizer
        var receiverType = context.SemanticModel.GetTypeInfo(
            elementAccess.Expression, context.CancellationToken).Type;
        if (!msSymbols.IsStringLocalizerType(receiverType))
            return;

        if (!TranslationCallExtractor.TryGetKeyFromElementAccess(elementAccess, out var key, out var keyLocation)
            || key is null || keyLocation is null)
            return;

        if (key.Length == 0)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(DiagnosticDescriptors.EmptyKey, keyLocation));
        }
    }
}
