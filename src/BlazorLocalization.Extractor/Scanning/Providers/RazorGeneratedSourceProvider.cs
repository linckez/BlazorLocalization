using System.Xml.Linq;
using BlazorLocalization.Extractor.Scanning.Sources;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Extractor.Scanning.Providers;

/// <summary>
/// Compiles Razor files to generated C# and exposes them as syntax trees.
/// </summary>
public sealed class RazorGeneratedSourceProvider : ISourceProvider
{
	private readonly string _projectRoot;

	public RazorGeneratedSourceProvider(string projectRoot)
	{
		_projectRoot = projectRoot;
	}

	public IEnumerable<SourceDocument> GetDocuments()
	{
		var rootNamespace = ResolveRootNamespace(_projectRoot);

		foreach (var path in EnumerateRazorFiles(_projectRoot))
		{
			var generated = CompileRazorToCSharp(path, _projectRoot, rootNamespace);
			if (generated is null)
				continue;

			var tree = CSharpSyntaxTree.ParseText(generated, path: path);
			var lineMap = BuildLineMap(tree.GetRoot());
			yield return new SourceDocument(tree, new SourceOrigin(path, Path.GetFileName(_projectRoot), new LineMap(lineMap)));
		}
	}

	private static IEnumerable<string> EnumerateRazorFiles(string root)
	{
		foreach (var ext in new[] { "*.razor", "*.cshtml" })
		{
			foreach (var path in Directory.EnumerateFiles(root, ext, SearchOption.AllDirectories))
			{
				if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
					|| path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
					continue;

				yield return path;
			}
		}
	}

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

	/// <summary>
	/// Reads the RootNamespace from the .csproj in <paramref name="projectRoot"/>,
	/// falling back to the directory name (MSBuild's default behaviour).
	/// </summary>
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

	private static List<(int GeneratedLine, int OriginalLine)> BuildLineMap(SyntaxNode root)
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
