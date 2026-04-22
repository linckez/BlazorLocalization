using BlazorLocalization.Extractor.Adapters.Resx;
using BlazorLocalization.Extractor.Adapters.Roslyn;
using BlazorLocalization.Extractor.Ports;

namespace BlazorLocalization.Extractor.Application;

/// <summary>
/// Orchestrates a full scan of a single project directory:
/// source providers → Roslyn scanner + Resx scanner → TranslationPipeline → result.
/// The single entry point shared by all driving adapters (CLI, API, etc.).
/// </summary>
public static class ProjectScanner
{
    /// <summary>
    /// Scans <paramref name="projectDir"/> end-to-end and returns the merged result
    /// along with preserved scanner outputs for downstream access.
    /// </summary>
    public static ProjectScanResult Scan(string projectDir, CancellationToken cancellationToken = default)
    {
        var projectName = ProjectMetadata.GetProjectName(projectDir);

        // Collect source documents from both providers
        var csDocs = CSharpFileProvider.GetDocuments(projectDir);
        var razorDocs = RazorSourceProvider.GetDocuments(projectDir);
        var allDocs = csDocs.Concat(razorDocs).ToList();

        cancellationToken.ThrowIfCancellationRequested();

        // Run scanners
        var roslynOutput = RoslynScanner.Scan(allDocs);

        cancellationToken.ThrowIfCancellationRequested();

        var resxOutput = ResxScanner.Scan(projectDir);

        // Merge through pipeline
        IScannerOutput[] scannerOutputs = [roslynOutput, resxOutput];
        var mergeResult = TranslationPipeline.Run(scannerOutputs);

        return new ProjectScanResult(projectName, mergeResult, scannerOutputs);
    }
}
