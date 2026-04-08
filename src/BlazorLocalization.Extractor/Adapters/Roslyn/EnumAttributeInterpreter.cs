using BlazorLocalization.Extractor.Domain;
using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Extracts <see cref="TranslationDefinition"/> records from enum members decorated with
/// <c>[Translation]</c> attributes. Uses the Symbol API (not IOperation) because attributes
/// are declarative metadata, not executable operations.
/// </summary>
internal static class EnumAttributeInterpreter
{
    /// <summary>
    /// Inspects a field symbol (enum member) for <c>[Translation]</c> attributes.
    /// Returns a <see cref="TranslationDefinition"/> and a matching <see cref="TranslationReference"/>
    /// (so the entry gets status=Resolved instead of Review).
    /// </summary>
    public static (TranslationDefinition? Definition, TranslationReference? Reference) TryInterpret(
        IFieldSymbol field,
        INamedTypeSymbol translationAttribute,
        SourceFilePath file,
        int line)
    {
        var matchingAttrs = field.GetAttributes()
            .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, translationAttribute))
            .ToList();

        if (matchingAttrs.Count == 0)
            return (null, null);

        string? customKey = null;
        string? sourceText = null;
        Dictionary<string, TranslationSourceText>? inlineTranslations = null;

        foreach (var attr in matchingAttrs)
        {
            // Constructor arg [0] = message
            var message = attr.ConstructorArguments is [{ Value: string msg }] ? msg : null;
            if (message is null)
                continue;

            // Named args: Locale, Key
            string? locale = null;
            string? key = null;
            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == ExtensionsContract.AttrLocale && named.Value.Value is string loc)
                    locale = loc;
                else if (named.Key == ExtensionsContract.AttrKey && named.Value.Value is string k)
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

        if (sourceText is null)
            return (null, null);

        var enumTypeName = field.ContainingType.Name;
        var memberName = field.Name;
        var entryKey = customKey ?? $"Enum.{enumTypeName}_{memberName}";

        var site = new DefinitionSite(file, line, DefinitionKind.EnumAttribute);

        var def = new TranslationDefinition(
            entryKey,
            new SingularText(sourceText),
            site,
            inlineTranslations);

        return (def, null);
    }
}
