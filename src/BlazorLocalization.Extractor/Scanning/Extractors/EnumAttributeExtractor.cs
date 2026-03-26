using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Extractor.Scanning.Extractors;

/// <summary>
/// Detects <c>[Translation]</c> attributes on enum members and produces both an
/// <see cref="ExtractedCall"/> (for inspect) and a <see cref="TranslationEntry"/> (for export).
/// </summary>
internal static class EnumAttributeExtractor
{
	/// <summary>
	/// Extracts an <see cref="ExtractedCall"/> and <see cref="TranslationEntry"/> from an enum
	/// member decorated with <c>[Translation]</c> attributes. Returns <c>null</c> if the member
	/// has no matching attributes.
	/// </summary>
	public static (ExtractedCall Call, TranslationEntry? Entry)? TryExtract(
		EnumMemberDeclarationSyntax enumMember,
		SemanticModel semanticModel,
		SourceOrigin origin,
		BuilderSymbolTable symbols)
	{
		var fieldSymbol = semanticModel.GetDeclaredSymbol(enumMember);
		if (fieldSymbol is null)
			return null;

		var matchingAttrs = fieldSymbol.GetAttributes()
			.Where(a => symbols.IsTranslationAttribute(a.AttributeClass))
			.ToList();

		if (matchingAttrs.Count == 0)
			return null;

		string? customKey = null;
		string? sourceText = null;
		Dictionary<string, TranslationSourceText>? inlineTranslations = null;

		foreach (var attr in matchingAttrs)
		{
			// Constructor arg [0] = Message
			var message = attr.ConstructorArguments is [{ Value: string msg }] ? msg : null;
			if (message is null)
				continue;

			// Named arg: Locale
			string? locale = null;
			string? key = null;
			foreach (var named in attr.NamedArguments)
			{
				if (named.Key == symbols.TranslationLocaleParam && named.Value.Value is string loc)
					locale = loc;
				else if (named.Key == symbols.TranslationKeyParam && named.Value.Value is string k)
					key = k;
			}

			if (locale is not null)
			{
				inlineTranslations ??= new(StringComparer.OrdinalIgnoreCase);
				inlineTranslations[locale] = new SingularText(message);
			}
			else
			{
				sourceText = message;
				customKey ??= key;
			}
		}

		if (sourceText is null && inlineTranslations is null)
			return null;

		var enumTypeName = fieldSymbol.ContainingType.Name;
		var memberName = fieldSymbol.Name;
		var entryKey = customKey ?? $"Enum.{enumTypeName}_{memberName}";

		var location = origin.ResolveLocation(enumMember);

		// Build arguments matching the attribute's constructor + named parameters
		var arguments = new List<ResolvedArgument>();
		var position = 0;
		if (sourceText is not null)
			arguments.Add(new ResolvedArgument(position++, sourceText, IsLiteral: true, null, symbols.TranslationMessageParam));
		if (customKey is not null)
			arguments.Add(new ResolvedArgument(position++, customKey, IsLiteral: true, null, symbols.TranslationKeyParam));
		if (inlineTranslations is not null)
		{
			foreach (var (locale, text) in inlineTranslations)
				arguments.Add(new ResolvedArgument(position++, $"{locale}: {((SingularText)text).Value}", IsLiteral: true, null, symbols.TranslationLocaleParam));
		}

		var call = new ExtractedCall(
			enumTypeName,
			memberName,
			CallKind.AttributeDeclaration,
			location,
			OverloadResolutionStatus.Resolved,
			arguments);

		var source = new SourceReference(
			origin.FilePath,
			location.Line,
			origin.ProjectName,
			Context: null);

		var entry = sourceText is not null || inlineTranslations is not null
			? new TranslationEntry(
				entryKey,
				sourceText is not null ? new SingularText(sourceText) : null,
				source,
				inlineTranslations)
			: null;

		return (call, entry);
	}
}
