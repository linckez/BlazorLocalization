using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using BlazorLocalization.Analyzers.Analyzers;
using BlazorLocalization.Analyzers.Scanning;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Analyzers.CodeFixes;

/// <summary>
/// Code fix for BL0002: replaces GetString("key")/Loc["key"] with Loc.Translation(key: "key", message: "").
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class UseTranslationApiCodeFix : CodeFixProvider
{
    private static readonly IReadOnlyList<ITranslationFileParser> Parsers =
        new ITranslationFileParser[] { new ResxTranslationFileParser() };

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UseTranslationApi.Id);

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        // Build translation lookup from AdditionalFiles (if any)
        var additionalFiles = context.Document.Project.AnalyzerOptions.AdditionalFiles;
        var lookup = TranslationFileLookup.Build(additionalFiles, Parsers, context.CancellationToken);

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(MicrosoftLocalizationAnalyzer.KeyProperty, out var key)
                || key is null)
                continue;

            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

            diagnostic.Properties.TryGetValue(MicrosoftLocalizationAnalyzer.HasArgsProperty, out var hasArgsStr);
            var hasArgs = hasArgsStr == "True";

            diagnostic.Properties.TryGetValue(MicrosoftLocalizationAnalyzer.UsageKindProperty, out var usageKind);

            // Variant 1: always — key only with empty message
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use Translation() — key only",
                    createChangedDocument: ct => ApplyFix(context.Document, node, key, "", hasArgs, usageKind, null, ct),
                    equivalenceKey: "BL0002_KeyOnly"),
                diagnostic);

            // Variant 2: key + source text from resx (when available and unambiguous)
            if (lookup.TryGet(key, out var entry) && entry.SourceText is not null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Use Translation() — with source text",
                        createChangedDocument: ct => ApplyFix(context.Document, node, key, entry.SourceText, hasArgs, usageKind, null, ct),
                        equivalenceKey: "BL0002_WithSource"),
                    diagnostic);

                // Variant 3: key + source text + all translations (when cultures exist)
                if (entry.Translations.Count > 0)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Use Translation() — with all translations",
                            createChangedDocument: ct => ApplyFix(context.Document, node, key, entry.SourceText, hasArgs, usageKind, entry.Translations, ct),
                            equivalenceKey: "BL0002_WithTranslations"),
                        diagnostic);
                }
            }
        }
    }

    private static async Task<Document> ApplyFix(
        Document document,
        SyntaxNode node,
        string key,
        string message,
        bool hasArgs,
        string? usageKind,
        IReadOnlyDictionary<string, string>? translations,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        // Determine the receiver expression
        ExpressionSyntax? receiver = node switch
        {
            InvocationExpressionSyntax invocation
                when invocation.Expression is MemberAccessExpressionSyntax memberAccess
                => memberAccess.Expression,
            ElementAccessExpressionSyntax elementAccess
                => elementAccess.Expression,
            _ => null
        };

        if (receiver is null)
            return document;

        // Build: Loc.Translation(key: "key", sourceMessage: "")
        var arguments = new List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(
                SyntaxFactory.NameColon(BlazorLocalizationSymbols.KeyParameterName),
                default,
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(key))),
            SyntaxFactory.Argument(
                SyntaxFactory.NameColon(BlazorLocalizationSymbols.SourceMessageParameterName),
                default,
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(message)))
        };

        // If original had extra args, add replaceWith: new { /* TODO */ }
        if (hasArgs)
        {
            arguments.Add(
                SyntaxFactory.Argument(
                    SyntaxFactory.NameColon(BlazorLocalizationSymbols.ReplaceWithParameterName),
                    default,
                    SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.Token(SyntaxKind.NewKeyword),
                        type: null!,
                        SyntaxFactory.ArgumentList(),
                        initializer: null)
                    .WithLeadingTrivia(SyntaxFactory.Comment("/* TODO: migrate positional args to named placeholders */ "))));
        }

        var translationInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver.WithoutTrivia(),
                SyntaxFactory.IdentifierName(BlazorLocalizationSymbols.TranslationMethodName)),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(arguments)));

        // Chain .For("culture", "text") calls for each translation (sorted alphabetically)
        ExpressionSyntax result = translationInvocation;
        if (translations is not null)
        {
            foreach (var kvp in translations.OrderBy(t => t.Key, StringComparer.Ordinal))
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
        }

        // Preserve leading/trailing trivia from the original node
        result = result
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(node, result);
        return document.WithSyntaxRoot(newRoot);
    }
}
