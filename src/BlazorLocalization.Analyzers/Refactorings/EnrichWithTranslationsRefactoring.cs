using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using BlazorLocalization.Analyzers.Scanning;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Analyzers.Refactorings;

/// <summary>
/// Appends <c>.For("culture", "text")</c> calls to an existing
/// <c>Translation("key", "message")</c> invocation using data from translation files (resx).
/// Appears in the hammer (refactoring) menu.
/// </summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp), Shared]
public sealed class EnrichWithTranslationsRefactoring : CodeRefactoringProvider
{
    private static readonly IReadOnlyList<ITranslationFileParser> Parsers =
        new ITranslationFileParser[] { new ResxTranslationFileParser() };

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        // Find the InvocationExpressionSyntax at the cursor
        var node = root.FindNode(context.Span);
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
            return;

        // Walk up any .For() chain to find the root Translation() call
        var rootInvocation = GetRootTranslationInvocation(invocation);
        if (rootInvocation is null)
            return;

        if (rootInvocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        if (memberAccess.Name.Identifier.ValueText != BlazorLocalizationSymbols.TranslationMethodName)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
            return;

        // Verify receiver is IStringLocalizer
        var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        var msSymbols = new MicrosoftLocalizationSymbols(semanticModel.Compilation);
        if (!msSymbols.IsStringLocalizerType(receiverType))
            return;

        // Resolve the method symbol
        if (semanticModel.GetSymbolInfo(rootInvocation, context.CancellationToken).Symbol is not IMethodSymbol method)
            return;

        // Extract key
        if (!TranslationCallExtractor.TryGetKeyFromInvocation(rootInvocation, method, out var key, out _) || key is null)
            return;

        // Build translation lookup from AdditionalFiles
        var additionalFiles = context.Document.Project.AnalyzerOptions.AdditionalFiles;
        var lookup = TranslationFileLookup.Build(additionalFiles, Parsers, context.CancellationToken);

        // Gate: must have unambiguous entry with culture translations
        if (!lookup.TryGet(key, out var entry) || entry.Translations.Count == 0)
            return;

        // Find existing .For() cultures on the current expression chain
        var existingCultures = GetExistingForCultures(invocation);

        // Filter to cultures not already present
        var newCultures = entry.Translations
            .Where(t => !existingCultures.Contains(t.Key))
            .OrderBy(t => t.Key, StringComparer.Ordinal)
            .ToList();

        if (newCultures.Count == 0)
            return;

        // Find the outermost invocation in the chain (last .For() or the Translation() itself)
        var outermostInvocation = GetOutermostInvocation(invocation);

        context.RegisterRefactoring(
            CodeAction.Create(
                title: "Enrich with translations from resource files",
                createChangedDocument: ct => ApplyRefactoring(context.Document, outermostInvocation, newCultures, ct),
                equivalenceKey: "EnrichWithTranslations"));
    }

    private static async Task<Document> ApplyRefactoring(
        Document document,
        InvocationExpressionSyntax targetInvocation,
        List<KeyValuePair<string, string>> cultures,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        // Build .For() chain starting from the target invocation
        ExpressionSyntax result = targetInvocation.WithoutTrivia();
        foreach (var kvp in cultures)
        {
            result = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    result,
                    SyntaxFactory.IdentifierName("For")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(kvp.Key))),
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(kvp.Value)))
                    })));
        }

        result = result
            .WithLeadingTrivia(targetInvocation.GetLeadingTrivia())
            .WithTrailingTrivia(targetInvocation.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(targetInvocation, result);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Walk up through .For() calls to find the root Translation() invocation.
    /// </summary>
    private static InvocationExpressionSyntax? GetRootTranslationInvocation(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (true)
        {
            if (current.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.ValueText == BlazorLocalizationSymbols.TranslationMethodName)
                    return current;

                if (memberAccess.Name.Identifier.ValueText == "For"
                    && memberAccess.Expression is InvocationExpressionSyntax parent)
                {
                    current = parent;
                    continue;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Find the outermost invocation in the .For() chain.
    /// </summary>
    private static InvocationExpressionSyntax GetOutermostInvocation(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax parentMember
               && parentMember.Name.Identifier.ValueText == "For"
               && parentMember.Parent is InvocationExpressionSyntax parentInvocation)
        {
            current = parentInvocation;
        }
        return current;
    }

    /// <summary>
    /// Collect culture strings from existing .For("culture", "text") calls in the chain.
    /// </summary>
    private static HashSet<string> GetExistingForCultures(InvocationExpressionSyntax invocation)
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = invocation;

        while (current.Parent is MemberAccessExpressionSyntax parentMember
               && parentMember.Name.Identifier.ValueText == "For"
               && parentMember.Parent is InvocationExpressionSyntax parentInvocation)
        {
            current = parentInvocation;
            if (current.ArgumentList.Arguments.Count > 0
                && current.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal
                && literal.Token.Value is string culture)
            {
                cultures.Add(culture);
            }
        }

        // Also check if the starting invocation itself is a .For() call
        if (invocation.Expression is MemberAccessExpressionSyntax ma
            && ma.Name.Identifier.ValueText == "For"
            && invocation.ArgumentList.Arguments.Count > 0
            && invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit
            && lit.Token.Value is string c)
        {
            cultures.Add(c);
        }

        return cultures;
    }
}
