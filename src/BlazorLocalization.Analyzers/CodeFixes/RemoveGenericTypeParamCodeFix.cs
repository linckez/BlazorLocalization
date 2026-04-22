using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Analyzers.CodeFixes;

/// <summary>
/// Code fix for BL0004: removes the type parameter from IStringLocalizer&lt;T&gt;,
/// replacing it with non-generic IStringLocalizer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class RemoveGenericTypeParamCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.RedundantTypeParameter.Id);

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not GenericNameSyntax genericName)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove unused type parameter",
                    createChangedDocument: ct => RemoveTypeParameter(context.Document, genericName, ct),
                    equivalenceKey: DiagnosticDescriptors.RedundantTypeParameter.Id + "_RemoveTypeParam"),
                diagnostic);
        }
    }

    private static async Task<Document> RemoveTypeParameter(
        Document document,
        GenericNameSyntax genericName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var replacement = SyntaxFactory.IdentifierName(genericName.Identifier.ValueText)
            .WithLeadingTrivia(genericName.GetLeadingTrivia())
            .WithTrailingTrivia(genericName.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(genericName, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
