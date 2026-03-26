using BlazorLocalization.Extractor.Scanning.Sources;

namespace BlazorLocalization.Extractor.Scanning.Providers;

/// <summary>
/// Provides syntax trees and their source origins.
/// </summary>
public interface ISourceProvider
{
	IEnumerable<SourceDocument> GetDocuments();
}
