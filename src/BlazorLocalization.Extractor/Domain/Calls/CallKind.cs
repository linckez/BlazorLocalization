namespace BlazorLocalization.Extractor.Domain.Calls;

/// <summary>
/// Distinguishes the syntactic form of a detected <see cref="ExtractedCall"/>.
/// </summary>
public enum CallKind
{
	MethodInvocation,
	IndexerAccess,
	AttributeDeclaration
}
