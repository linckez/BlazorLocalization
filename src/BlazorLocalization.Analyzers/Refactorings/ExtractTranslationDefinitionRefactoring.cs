using System.Composition;
using BlazorLocalization.Analyzers.Scanning;
using BlazorLocalization.Extensions.Translation.Definitions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;

namespace BlazorLocalization.Analyzers.Refactorings;

/// <summary>
/// Extracts an inline <c>Loc.Translation("key", "message")</c> call into a
/// <c>static readonly SimpleDefinition</c> field + <c>Loc.Translation(field)</c> call site.
/// Appears in the hammer (refactoring) menu, not the lightbulb (code fix) menu.
/// </summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp), Shared]
public sealed class ExtractTranslationDefinitionRefactoring : CodeRefactoringProvider
{
    private static readonly string DefinitionsNamespace = typeof(SimpleDefinition).Namespace!;

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

        // Must be a member access: Loc.Translation(...)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        if (memberAccess.Name.Identifier.ValueText != BlazorLocalizationSymbols.TranslationMethodName)
            return;

        // Must be inside a type declaration (can't add a field in top-level statements)
        var containingType = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
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
        if (semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
            return;

        // Extract key and message by parameter name
        if (!TryGetLiteralArgument(invocation, method, BlazorLocalizationSymbols.KeyParameterName, out var key, out _))
            return;

        if (!TryGetLiteralArgument(invocation, method, BlazorLocalizationSymbols.SourceMessageParameterName, out var message, out _))
            return;

        // Guard: if this is already Translation(SimpleDefinition), don't offer
        // (the method wouldn't have a "key" parameter, so we'd already have bailed above)

        context.RegisterRefactoring(
            CodeAction.Create(
                title: "Extract to static translation definition",
                createChangedDocument: ct => ApplyRefactoring(context.Document, invocation, method, key!, message!, ct),
                equivalenceKey: "ExtractTranslationDefinition"));
    }

    private static async Task<Document> ApplyRefactoring(
        Document document,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string key,
        string message,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var containingType = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
            return document;

        // Generate field name from key
        var fieldName = KeyToIdentifier.ToFieldName(key);

        // Check for name collision with existing members
        fieldName = EnsureUniqueName(fieldName, containingType);

        // Build the field declaration:
        // static readonly SimpleDefinition FieldName = TranslationDefinitions.DefineSimple("key", "message");
        var fieldDecl = BuildFieldDeclaration(fieldName, key, message);

        // Find the member that contains the invocation
        var containingMember = FindContainingMember(invocation, containingType);

        // Build the replacement call site: Translation(fieldName) or Translation(fieldName, replaceWith: ...)
        var newInvocation = BuildReplacementInvocation(invocation, method, fieldName);

        // Apply all mutations on one syntax tree:
        // 1. Replace invocation arguments
        // 2. Insert field declaration
        // 3. Add using directive if needed
        var editor = new SyntaxEditor(root, document.Project.Solution.Workspace.Services);

        // Replace the invocation
        editor.ReplaceNode(invocation, newInvocation
            .WithLeadingTrivia(invocation.GetLeadingTrivia())
            .WithTrailingTrivia(invocation.GetTrailingTrivia()));

        // Insert field before the containing member
        if (containingMember is not null)
        {
            editor.InsertBefore(containingMember, fieldDecl);
        }
        else
        {
            // Fallback: insert at the beginning of the type
            var firstMember = containingType.Members.FirstOrDefault();
            if (firstMember is not null)
                editor.InsertBefore(firstMember, fieldDecl);
            else
                editor.AddMember(containingType, fieldDecl);
        }

        var newRoot = editor.GetChangedRoot();

        // Add using directives if needed
        if (newRoot is CompilationUnitSyntax compilationUnit)
        {
            newRoot = EnsureUsingDirectives(compilationUnit);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static FieldDeclarationSyntax BuildFieldDeclaration(string fieldName, string key, string message)
    {
        // static readonly SimpleDefinition FieldName = TranslationDefinitions.DefineSimple("key", "message");
        var initializer = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(nameof(TranslationDefinitions)),
                SyntaxFactory.IdentifierName(BlazorLocalizationSymbols.DefineSimpleMethodName)),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(key))),
                    SyntaxFactory.Argument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(message)))
                })));

        var variableDeclarator = SyntaxFactory.VariableDeclarator(
            SyntaxFactory.Identifier(fieldName)
                .WithAdditionalAnnotations(RenameAnnotation.Create()))
            .WithInitializer(SyntaxFactory.EqualsValueClause(initializer));

        var variableDeclaration = SyntaxFactory.VariableDeclaration(
            SyntaxFactory.IdentifierName(nameof(SimpleDefinition)),
            SyntaxFactory.SingletonSeparatedList(variableDeclarator));

        return SyntaxFactory.FieldDeclaration(variableDeclaration)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static InvocationExpressionSyntax BuildReplacementInvocation(
        InvocationExpressionSyntax original,
        IMethodSymbol method,
        string fieldName)
    {
        // Keep the receiver and .Translation(...) member access
        var newArgs = new System.Collections.Generic.List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(fieldName))
        };

        // Preserve any replaceWith argument
        var replaceWithArg = TranslationCallExtractor.FindArgumentForParameter(original.ArgumentList.Arguments, method.Parameters, BlazorLocalizationSymbols.ReplaceWithParameterName);
        if (replaceWithArg is not null)
        {
            newArgs.Add(replaceWithArg);
        }

        return original.WithArgumentList(
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(newArgs)));
    }

    private static string EnsureUniqueName(string fieldName, TypeDeclarationSyntax containingType)
    {
        var existingNames = new System.Collections.Generic.HashSet<string>();
        foreach (var member in containingType.Members)
        {
            if (member is FieldDeclarationSyntax field)
            {
                foreach (var variable in field.Declaration.Variables)
                    existingNames.Add(variable.Identifier.ValueText);
            }
            else if (member is PropertyDeclarationSyntax property)
            {
                existingNames.Add(property.Identifier.ValueText);
            }
            else if (member is MethodDeclarationSyntax methodDecl)
            {
                existingNames.Add(methodDecl.Identifier.ValueText);
            }
        }

        if (!existingNames.Contains(fieldName))
            return fieldName;

        var suffix = 1;
        while (existingNames.Contains(fieldName + suffix))
            suffix++;

        return fieldName + suffix;
    }

    private static MemberDeclarationSyntax? FindContainingMember(
        SyntaxNode node, TypeDeclarationSyntax containingType)
    {
        var current = node.Parent;
        while (current is not null && current != containingType)
        {
            if (current is MemberDeclarationSyntax member && current.Parent == containingType)
                return member;
            current = current.Parent;
        }

        return null;
    }

    private static CompilationUnitSyntax EnsureUsingDirectives(CompilationUnitSyntax root)
    {
        var hasDefinitionsUsing = false;
        foreach (var u in root.Usings)
        {
            if (u.Name?.ToString() == DefinitionsNamespace)
            {
                hasDefinitionsUsing = true;
                break;
            }
        }

        if (!hasDefinitionsUsing)
        {
            root = root.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(DefinitionsNamespace))
                    .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
        }

        return root;
    }

    private static bool TryGetLiteralArgument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string parameterName,
        out string? value,
        out ArgumentSyntax? argument)
    {
        value = null;
        argument = TranslationCallExtractor.FindArgumentForParameter(invocation.ArgumentList.Arguments, method.Parameters, parameterName);

        if (argument is null)
            return false;

        if (argument.Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            return false;

        value = literal.Token.ValueText;
        return true;
    }
}
