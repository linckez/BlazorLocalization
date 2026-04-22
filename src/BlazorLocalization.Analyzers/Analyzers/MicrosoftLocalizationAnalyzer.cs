using System.Collections.Immutable;
using BlazorLocalization.Analyzers.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BlazorLocalization.Analyzers.Analyzers;

/// <summary>
/// BL0002: Flags vanilla IStringLocalizer usage (GetString/indexer) and suggests Translation() API.
/// Requires semantic model to confirm the receiver is IStringLocalizer.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MicrosoftLocalizationAnalyzer : DiagnosticAnalyzer
{
    internal const string KeyProperty = "Key";
    internal const string HasArgsProperty = "HasArgs";
    internal const string UsageKindProperty = "UsageKind";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UseTranslationApi);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var msSymbols = new MicrosoftLocalizationSymbols(compilationContext.Compilation);

            // If IStringLocalizer isn't in the compilation, this project doesn't use localization
            if (msSymbols.IStringLocalizerType is null)
                return;

            // Only suggest Translation() when BlazorLocalization.Extensions is referenced
            var blSymbols = new BlazorLocalizationSymbols(compilationContext.Compilation);
            if (blSymbols.ProviderBasedFactoryType is null)
                return;

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, msSymbols),
                SyntaxKind.InvocationExpression);

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeElementAccess(ctx, msSymbols),
                SyntaxKind.ElementAccessExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, MicrosoftLocalizationSymbols msSymbols)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.ValueText;

        // Skip if already using our API
        if (methodName is BlazorLocalizationSymbols.TranslationMethodName or BlazorLocalizationSymbols.DisplayMethodName)
            return;

        // Only flag GetString calls
        if (methodName is not MicrosoftLocalizationSymbols.GetStringMethodName)
            return;

        // Semantic check: receiver must be IStringLocalizer
        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (!msSymbols.IsStringLocalizerType(receiverType))
            return;

        // Resolve the method symbol for parameter-name-based argument matching
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
            return;

        // Extract key for diagnostic properties (code fix reads it)
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(UsageKindProperty, MicrosoftLocalizationSymbols.GetStringMethodName);

        if (TranslationCallExtractor.TryGetKeyFromInvocation(invocation, method, out var key, out _))
        {
            properties = properties
                .Add(KeyProperty, key)
                .Add(HasArgsProperty, TranslationCallExtractor.HasExtraArguments(invocation, method).ToString());
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.UseTranslationApi,
                invocation.GetLocation(),
                properties,
                MicrosoftLocalizationSymbols.GetStringMethodName));
    }

    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context, MicrosoftLocalizationSymbols msSymbols)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        // Semantic check: receiver must be IStringLocalizer
        var receiverType = context.SemanticModel.GetTypeInfo(elementAccess.Expression, context.CancellationToken).Type;
        if (!msSymbols.IsStringLocalizerType(receiverType))
            return;

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(UsageKindProperty, "Indexer");

        if (TranslationCallExtractor.TryGetKeyFromElementAccess(elementAccess, out var key, out _))
        {
            properties = properties
                .Add(KeyProperty, key)
                .Add(HasArgsProperty, TranslationCallExtractor.HasExtraArguments(elementAccess).ToString());
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.UseTranslationApi,
                elementAccess.GetLocation(),
                properties,
                "indexer"));
    }
}
