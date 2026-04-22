using BlazorLocalization.Extractor.Application;

namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Immutable wrapper around a source file reference that always carries the absolute path.
/// Presentation (relative vs absolute) is a method call — never a mutation.
/// </summary>
public sealed record SourceFilePath(string AbsolutePath, string ProjectDir)
{
	/// <summary>Path relative to <see cref="ProjectDir"/>, using forward slashes.</summary>
	public string RelativePath =>
		Path.GetRelativePath(ProjectDir, AbsolutePath).Replace('\\', '/');

	/// <summary>File name with extension (e.g. <c>Home.razor</c>).</summary>
	public string FileName => Path.GetFileName(AbsolutePath);

	/// <summary>Project name from <c>&lt;AssemblyName&gt;</c> or the <c>.csproj</c> filename.</summary>
	public string ProjectName => ProjectMetadata.GetProjectName(ProjectDir);

	/// <summary>Whether this file is a .resx file.</summary>
	public bool IsResx => AbsolutePath.EndsWith(".resx", StringComparison.OrdinalIgnoreCase);

	/// <summary>Returns the path formatted for export output.</summary>
	public string Display(PathStyle style) => style switch
	{
		PathStyle.Relative => RelativePath,
		PathStyle.Absolute => AbsolutePath,
		_ => RelativePath
	};
}
