using System.Xml.Linq;

namespace BlazorLocalization.Extractor.Application;

/// <summary>
/// Resolves project metadata (name, assembly name) from a project directory.
/// </summary>
internal static class ProjectMetadata
{
    /// <summary>
    /// Returns the project name for a project directory.
    /// Reads <c>&lt;AssemblyName&gt;</c> from the <c>.csproj</c> if present,
    /// otherwise falls back to the <c>.csproj</c> filename without extension.
    /// If no <c>.csproj</c> exists, returns the directory name.
    /// </summary>
    internal static string GetProjectName(string projectDir)
    {
        if (!Directory.Exists(projectDir))
            return Path.GetFileName(projectDir);

        var csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (csproj is null)
            return Path.GetFileName(projectDir);

        try
        {
            var doc = XDocument.Load(csproj);
            var assemblyName = doc.Descendants("AssemblyName").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(assemblyName))
                return assemblyName;
        }
        catch
        {
            // Malformed csproj — fall through to filename
        }

        return Path.GetFileNameWithoutExtension(csproj);
    }
}
