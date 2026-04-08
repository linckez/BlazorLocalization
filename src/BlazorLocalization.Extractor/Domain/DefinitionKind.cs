namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// How a translation's source text was defined.
/// Machine-readable discriminator — renderers switch on this, never parse <see cref="DefinitionSite.Context"/>.
/// </summary>
public enum DefinitionKind
{
	/// <summary><c>.Translation(key, message)</c> in Razor/code — defines source text AND uses the key.</summary>
	InlineTranslation,

	/// <summary><c>DefineSimple/Plural/Select/SelectPlural</c> factory — defines source text only.</summary>
	ReusableDefinition,

	/// <summary><c>[Translation("...")]</c> on an enum member — defines source text only.</summary>
	EnumAttribute,

	/// <summary>A <c>.resx</c> resource file entry — defines source text only.</summary>
	ResourceFile
}
