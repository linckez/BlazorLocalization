using System.ComponentModel;

namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Export format for extracted translation files.
/// <c>[Description]</c> attributes serve as single source of truth for both <c>--help</c> and interactive wizard prompts.
/// </summary>
public enum ExportFormat
{
	/// <summary>Crowdin i18next JSON — flat key/value pairs, plurals via <c>_one</c>/<c>_other</c> suffixes.</summary>
	[Description("Crowdin i18next JSON (flat key/value, plurals via _one/_other)")]
	I18Next,

	/// <summary>GNU Gettext PO — <c>msgid</c>/<c>msgstr</c> pairs with <c>#:</c> source references and <c>#.</c> translator comments.</summary>
	[Description("GNU Gettext PO (with source references and translator comments)")]
	Po,

	/// <summary>Generic JSON — full-fidelity array with source references, useful for debugging and downstream tooling.</summary>
	[Description("Generic JSON (full-fidelity debug export with all metadata)")]
	Json
}
