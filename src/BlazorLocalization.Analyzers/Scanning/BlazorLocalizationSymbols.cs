using BlazorLocalization.Extensions;
using BlazorLocalization.Extensions.Translation.Definitions;
using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Analyzers.Scanning;

/// <summary>
/// Resolves BlazorLocalization.Extensions symbols from the compilation.
/// Type resolution uses typeof() for compile-time safety (rename = compile error).
/// Method names use nameof() where possible. Parameter names are centralized constants
/// validated by contract tests (can't nameof() parameters or extension methods in another assembly).
/// </summary>
internal sealed class BlazorLocalizationSymbols
{
    // Type metadata names — typeof() gives compile errors on rename (DLLs packed in analyzer NuGet)
    private static readonly string ProviderBasedFactoryMetadataName = typeof(IProviderBasedStringLocalizerFactory).FullName!;
    private static readonly string TranslationDefinitionsMetadataName = typeof(TranslationDefinitions).FullName!;

    // Builder type metadata names — can't typeof() because builders live in Extensions (not Abstractions).
    // Validated by contract tests in SymbolNameContractTests.
    internal static readonly string SimpleBuilderMetadataName = "BlazorLocalization.Extensions.Translation.SimpleBuilder";
    internal static readonly string PluralBuilderMetadataName = "BlazorLocalization.Extensions.Translation.PluralBuilder";
    internal static readonly string SelectBuilderMetadataName = "BlazorLocalization.Extensions.Translation.SelectBuilder`1";
    internal static readonly string SelectPluralBuilderMetadataName = "BlazorLocalization.Extensions.Translation.SelectPluralBuilder`1";

    // Method name constants — nameof() on real Abstractions types (compile-time safe)
    public const string DefineSimpleMethodName = nameof(TranslationDefinitions.DefineSimple);
    public const string DefinePluralMethodName = nameof(TranslationDefinitions.DefinePlural);
    public const string DefineSelectMethodName = nameof(TranslationDefinitions.DefineSelect);
    public const string DefineSelectPluralMethodName = nameof(TranslationDefinitions.DefineSelectPlural);

    // Extension method names — StringLocalizerExtensions is in Extensions (not Abstractions),
    // so can't nameof(). Validated by contract tests.
    public const string TranslationMethodName = "Translation";
    public const string DisplayMethodName = "Display";

    // Parameter names — can't nameof() parameters, and pattern matching requires const.
    // Validated by contract tests.
    public const string KeyParameterName = "key";
    public const string MessageParameterName = "message";
    public const string SourceMessageParameterName = "sourceMessage";
    public const string ReplaceWithParameterName = "replaceWith";

    /// <summary>
    /// <c>IProviderBasedStringLocalizerFactory</c>. Null if the project doesn't reference BlazorLocalization.Extensions.
    /// Used as a guard: BL0004 only fires when this type is present.
    /// </summary>
    public INamedTypeSymbol? ProviderBasedFactoryType { get; }

    /// <summary>
    /// <c>TranslationDefinitions</c>. Null if the project doesn't reference BlazorLocalization.Extensions.
    /// Used by BL0003 to verify Define*() calls target the real type.
    /// </summary>
    public INamedTypeSymbol? TranslationDefinitionsType { get; }

    // Builder types — resolved from compilation, used for return-type routing in BL0003/BL0005.
    public INamedTypeSymbol? SimpleBuilderType { get; }
    public INamedTypeSymbol? PluralBuilderType { get; }
    public INamedTypeSymbol? SelectBuilderType { get; }
    public INamedTypeSymbol? SelectPluralBuilderType { get; }

    public BlazorLocalizationSymbols(Compilation compilation)
    {
        ProviderBasedFactoryType = compilation.GetTypeByMetadataName(ProviderBasedFactoryMetadataName);
        TranslationDefinitionsType = compilation.GetTypeByMetadataName(TranslationDefinitionsMetadataName);
        SimpleBuilderType = compilation.GetTypeByMetadataName(SimpleBuilderMetadataName);
        PluralBuilderType = compilation.GetTypeByMetadataName(PluralBuilderMetadataName);
        SelectBuilderType = compilation.GetTypeByMetadataName(SelectBuilderMetadataName);
        SelectPluralBuilderType = compilation.GetTypeByMetadataName(SelectPluralBuilderMetadataName);
    }

    /// <summary>
    /// Maps a method's return type to <see cref="KeyUsageKind"/> by matching against
    /// pre-resolved builder types. Returns null if the return type is not a builder.
    /// </summary>
    public KeyUsageKind? GetBuilderKind(ITypeSymbol returnType)
    {
        var orig = returnType.OriginalDefinition;
        if (SimpleBuilderType is not null && SymbolEqualityComparer.Default.Equals(orig, SimpleBuilderType)) return KeyUsageKind.Simple;
        if (PluralBuilderType is not null && SymbolEqualityComparer.Default.Equals(orig, PluralBuilderType)) return KeyUsageKind.Plural;
        if (SelectBuilderType is not null && SymbolEqualityComparer.Default.Equals(orig, SelectBuilderType)) return KeyUsageKind.Select;
        if (SelectPluralBuilderType is not null && SymbolEqualityComparer.Default.Equals(orig, SelectPluralBuilderType)) return KeyUsageKind.SelectPlural;
        return null;
    }
}
