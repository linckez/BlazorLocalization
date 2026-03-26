using Basic.Reference.Assemblies;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning.Extractors;
using BlazorLocalization.Extractor.Scanning.Providers;
using BlazorLocalization.Extractor.Scanning.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Localization;
using StringLocalizerExtensions = BlazorLocalization.Extensions.StringLocalizerExtensions;

namespace BlazorLocalization.Extractor.Scanning;

/// <summary>
/// Orchestrates source scanning: collects documents, compiles them, and delegates
/// extraction to <see cref="LocalizerCallExtractor"/> and <see cref="EnumAttributeExtractor"/>.
/// </summary>
public sealed class Scanner
{
	private readonly IReadOnlyList<ISourceProvider> _providers;

	public Scanner(IEnumerable<ISourceProvider> providers)
	{
		_providers = providers.Distinct().ToList();
	}

	/// <summary>
	/// Scans all source providers and returns raw calls plus interpreted translation entries.
	/// </summary>
	public ScanResult Run()
	{
		var documents = _providers.SelectMany(p => p.GetDocuments()).ToList();
		if (documents.Count == 0)
			return new ScanResult([], []);

		var trees = documents.Select(d => d.Tree).ToList();
		var compilation = CreateCompilation(trees);
		var symbols = new BuilderSymbolTable(compilation);

		var calls = new List<ExtractedCall>();
		var entries = new List<TranslationEntry>();

		foreach (var doc in documents)
		{
			var semanticModel = compilation.GetSemanticModel(doc.Tree, ignoreAccessibility: true);
			var root = doc.Tree.GetRoot();

			foreach (var node in root.DescendantNodes())
			{
				(ExtractedCall Call, TranslationEntry? Entry)? result = node switch
				{
					InvocationExpressionSyntax invocation
						=> LocalizerCallExtractor.TryExtractInvocation(invocation, semanticModel, doc.Origin, symbols),
					ElementAccessExpressionSyntax elementAccess
						=> LocalizerCallExtractor.TryExtractIndexer(elementAccess, semanticModel, doc.Origin),
					EnumMemberDeclarationSyntax enumMember
						=> EnumAttributeExtractor.TryExtract(enumMember, semanticModel, doc.Origin, symbols),
					_ => null
				};

				if (result is not null)
				{
					calls.Add(result.Value.Call);
					if (result.Value.Entry is not null)
						entries.Add(result.Value.Entry);
				}
			}
		}

		return new ScanResult(calls, entries);
	}

	private static CSharpCompilation CreateCompilation(IReadOnlyList<SyntaxTree> trees)
	{
		var references = BuildMetadataReferences();
		return CSharpCompilation.Create(
			"ExtractorAnalysis",
			trees,
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	/// <summary>
	/// Builds the metadata references needed for Roslyn's semantic model.
	/// Uses embedded reference assemblies (Basic.Reference.Assemblies) for BCL types — no disk I/O
	/// for the ~200 core framework assemblies. Our own extension assemblies are loaded from disk
	/// (always available as tool package dependencies).
	/// </summary>
	private static List<MetadataReference> BuildMetadataReferences()
	{
		var refs = new List<MetadataReference>(Net100.References.All);

		refs.Add(MetadataReference.CreateFromFile(typeof(IStringLocalizer).Assembly.Location));
		refs.Add(MetadataReference.CreateFromFile(typeof(StringLocalizerExtensions).Assembly.Location));

		return refs;
	}
}
