using Microsoft.CodeAnalysis.CSharp;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Enumerates <c>.cs</c> files under a project directory and produces
/// <see cref="SourceDocument"/>s ready for <see cref="RoslynScanner"/>.
/// Skips <c>obj/</c> and <c>bin/</c> directories.
/// </summary>
internal static class CSharpFileProvider
{
    public static IReadOnlyList<SourceDocument> GetDocuments(string projectDir)
    {
        var docs = new List<SourceDocument>();
        foreach (var path in EnumerateSourceFiles(projectDir))
        {
            var tree = CSharpSyntaxTree.ParseText(System.IO.File.ReadAllText(path), path: path);
            docs.Add(new SourceDocument(tree, path, projectDir, LineMap: null));
        }
        return docs;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Order();
}
