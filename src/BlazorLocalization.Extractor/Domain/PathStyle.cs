using System.ComponentModel;

namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Controls how source file paths are written in export output.
/// <c>[Description]</c> attributes serve as single source of truth for both <c>--help</c> and interactive wizard prompts.
/// </summary>
public enum PathStyle
{
	/// <summary>Paths relative to the project root directory (e.g. <c>Components/Pages/Home.razor</c>).</summary>
	[Description("Paths relative to project root (recommended)")]
	Relative,

	/// <summary>Full absolute filesystem paths.</summary>
	[Description("Full filesystem paths")]
	Absolute
}
