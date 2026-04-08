using System.Xml.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Compiles <c>.razor</c> and <c>.cshtml</c> files to generated C# and produces
/// <see cref="SourceDocument"/>s with <see cref="LineMap"/>s for accurate line resolution.
/// </summary>
internal static class RazorSourceProvider
{
    public static IReadOnlyList<SourceDocument> GetDocuments(string projectDir)
    {
        var rootNamespace = ResolveRootNamespace(projectDir);
        var docs = new List<SourceDocument>();

        foreach (var path in EnumerateRazorFiles(projectDir))
        {
            var generated = CompileRazorToCSharp(path, projectDir, rootNamespace);
            if (generated is null)
                continue;

            var tree = CSharpSyntaxTree.ParseText(generated, path: path);
            var lineMap = BuildLineMap(tree.GetRoot());
            docs.Add(new SourceDocument(tree, path, projectDir, new LineMap(lineMap)));
        }

        return docs;
    }

    private static IEnumerable<string> EnumerateRazorFiles(string root) =>
        new[] { "*.razor", "*.cshtml" }
            .SelectMany(ext => Directory.EnumerateFiles(root, ext, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Order();

    private static string? CompileRazorToCSharp(string filePath, string projectRoot, string rootNamespace)
    {
        var fileSystem = RazorProjectFileSystem.Create(projectRoot);
        var engine = RazorProjectEngine.Create(
            RazorConfiguration.Default,
            fileSystem,
            builder => builder.SetRootNamespace(rootNamespace));

        var relativePath = "/" + Path.GetRelativePath(projectRoot, filePath).Replace('\\', '/');
        var item = fileSystem.GetItem(relativePath, fileKind: null);
        if (item is null)
            return null;

        var codeDocument = engine.Process(item);
        return codeDocument.GetCSharpDocument().GeneratedCode;
    }

    private static string ResolveRootNamespace(string projectRoot)
    {
        var csproj = Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (csproj is not null)
        {
            var doc = XDocument.Load(csproj);
            var ns = doc.Descendants("RootNamespace").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(ns))
                return ns;
        }
        return Path.GetFileName(projectRoot);
    }

    private static List<(int GeneratedLine, int OriginalLine)> BuildLineMap(Microsoft.CodeAnalysis.SyntaxNode root)
    {
        var map = new List<(int GeneratedLine, int OriginalLine)>();

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.LineDirectiveTrivia) || !trivia.HasStructure)
                continue;

            if (trivia.GetStructure() is not LineDirectiveTriviaSyntax directive)
                continue;

            if (!int.TryParse(directive.Line.Text, out var originalLine))
                continue;

            var generatedLine = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            map.Add((generatedLine, originalLine));
        }

        return map.OrderBy(m => m.GeneratedLine).ToList();
    }
}
