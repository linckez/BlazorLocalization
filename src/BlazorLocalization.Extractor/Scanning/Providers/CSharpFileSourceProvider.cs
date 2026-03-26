using BlazorLocalization.Extractor.Scanning.Sources;
using Microsoft.CodeAnalysis.CSharp;

namespace BlazorLocalization.Extractor.Scanning.Providers;

/// <summary>
/// Reads C# files from a source root and converts them into syntax trees.
/// </summary>
public sealed class CSharpFileSourceProvider : ISourceProvider
{
	private readonly string _root;

	public CSharpFileSourceProvider(string root)
	{
		_root = root;
	}

	public IEnumerable<SourceDocument> GetDocuments()
	{
		foreach (var path in EnumerateSourceFiles(_root))
		{
			var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
			yield return new SourceDocument(tree, new SourceOrigin(path, Path.GetFileName(_root), null));
		}
	}

	private static IEnumerable<string> EnumerateSourceFiles(string root) =>
		Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
						&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
			.Order();
}
