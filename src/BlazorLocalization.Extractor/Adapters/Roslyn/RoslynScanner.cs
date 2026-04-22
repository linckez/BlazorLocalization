using Basic.Reference.Assemblies;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Ports;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Orchestrates a Roslyn-based scan of C# source trees.
/// Creates a compilation, resolves scan targets, runs the walker,
/// and feeds results through <see cref="CallInterpreter"/> to produce domain types.
/// </summary>
internal static class RoslynScanner
{
    /// <summary>
    /// Scans the given source documents and returns a port-compliant <see cref="IScannerOutput"/>.
    /// Also returns <see cref="RoslynScannerOutput.RawCalls"/> for the inspect command.
    /// </summary>
    public static RoslynScannerOutput Scan(IReadOnlyList<SourceDocument> documents)
    {
        var metadataRefs = BuildReferences();
        var trees = documents.Select(d => d.Tree).ToList();

        var compilation = CSharpCompilation.Create(
            "Scan",
            trees,
            metadataRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var target = ScanTargets.ResolveLocalizer(compilation);
        var translationAttr = ScanTargets.ResolveTranslationAttribute(compilation);
        var definitionFactory = ScanTargets.ResolveDefinitionFactory(compilation);
        var resolvedTypes = ScanTargets.ResolveBuilderTypes(compilation);

        var diagnostics = new List<ScanDiagnostic>();
        if (target is null)
        {
            diagnostics.Add(new ScanDiagnostic(
                DiagnosticLevel.Error,
                "IStringLocalizer type not found in compilation references. " +
                "Ensure Microsoft.Extensions.Localization is referenced."));
            return new RoslynScannerOutput([], [], diagnostics, []);
        }

        var allCalls = new List<ScannedCallSite>();
        var definitions = new List<TranslationDefinition>();
        var references = new List<TranslationReference>();

        foreach (var doc in documents)
        {
            var tree = doc.Tree;
            var file = new SourceFilePath(doc.OriginalFilePath, doc.ProjectDir);

            var model = compilation.GetSemanticModel(tree);
            var walker = new LocalizerOperationWalker(target, definitionFactory);

            // Walk method bodies
            foreach (var methodSyntax in tree.GetRoot().DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                var body = methodSyntax switch
                {
                    MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
                    ConstructorDeclarationSyntax c => (SyntaxNode?)c.Body ?? c.ExpressionBody,
                    _ => null
                };
                if (body is null) continue;

                var blockOp = model.GetOperation(body);
                if (blockOp is null) continue;
                walker.Visit(blockOp);
            }

            // Walk top-level statements (minimal API / global statements)
            foreach (var globalStmt in tree.GetRoot().DescendantNodes().OfType<GlobalStatementSyntax>())
            {
                var op = model.GetOperation(globalStmt.Statement);
                if (op is not null) walker.Visit(op);
            }

            // Walk property bodies (for Razor component properties and expression-bodied properties)
            foreach (var propSyntax in tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (propSyntax.ExpressionBody is not null)
                {
                    var op = model.GetOperation(propSyntax.ExpressionBody);
                    if (op is not null) walker.Visit(op);
                }
                else if (propSyntax.AccessorList is not null)
                {
                    foreach (var accessor in propSyntax.AccessorList.Accessors)
                    {
                        var body = (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody;
                        if (body is null) continue;
                        var op = model.GetOperation(body);
                        if (op is not null) walker.Visit(op);
                    }
                }
            }

            // Walk field initializers (for DefineXxx static factory calls)
            foreach (var fieldSyntax in tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in fieldSyntax.Declaration.Variables)
                {
                    if (variable.Initializer is null) continue;
                    var op = model.GetOperation(variable.Initializer);
                    if (op is not null) walker.Visit(op);
                }
            }

            // Convert walker hits to ScannedCallSites
            foreach (var (op, symbol, arguments) in walker.Results)
            {
                var generatedLine = op.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var line = doc.ResolveOriginalLine(generatedLine);
                var callSite = CallSiteBuilder.Build(op, symbol, arguments, file, line);
                allCalls.Add(callSite);

                // Interpret into domain types
                var (def, refr) = CallInterpreter.Interpret(callSite, file, resolvedTypes);
                if (def is not null) definitions.Add(def);
                if (refr is not null) references.Add(refr);
            }

            // Walk enum members for [Translation] attributes
            if (translationAttr is not null)
            {
                foreach (var enumMember in tree.GetRoot().DescendantNodes().OfType<EnumMemberDeclarationSyntax>())
                {
                    var fieldSymbol = model.GetDeclaredSymbol(enumMember);
                    if (fieldSymbol is null) continue;

                    var generatedLine = enumMember.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var line = doc.ResolveOriginalLine(generatedLine);
                    var (def, refr) = EnumAttributeInterpreter.TryInterpret(fieldSymbol, translationAttr, file, line);
                    if (def is not null) definitions.Add(def);
                    if (refr is not null) references.Add(refr);
                }
            }
        }

        return new RoslynScannerOutput(definitions, references, diagnostics, allCalls);
    }

    private static List<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>(Net100.References.All);

        refs.Add(MetadataReference.CreateFromFile(
            typeof(Microsoft.Extensions.Localization.IStringLocalizer).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(
            typeof(Microsoft.AspNetCore.Components.ComponentBase).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(
            ExtensionsContract.ExtensionsAssemblyLocation));

        // Extensions.Abstractions may live in a separate assembly (TranslationAttribute,
        // TranslationDefinitions, builder types). Add it if it's a different DLL.
        var abstractionsLocation = typeof(BlazorLocalization.Extensions.Translation.Definitions.TranslationDefinitions).Assembly.Location;
        if (abstractionsLocation != ExtensionsContract.ExtensionsAssemblyLocation)
            refs.Add(MetadataReference.CreateFromFile(abstractionsLocation));

        return refs;
    }
}
