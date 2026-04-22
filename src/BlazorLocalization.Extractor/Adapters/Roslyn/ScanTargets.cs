using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Central registry of types we scan for.
/// Resolve once per compilation via <see cref="ResolveLocalizer"/> / <see cref="ResolveTranslationAttribute"/>.
/// </summary>
internal static class ScanTargets
{
    public static INamedTypeSymbol? ResolveLocalizer(Compilation compilation)
        => compilation.GetTypeByMetadataName(ExtensionsContract.MetaIStringLocalizer);

    public static INamedTypeSymbol? ResolveTranslationAttribute(Compilation compilation)
        => compilation.GetTypeByMetadataName(ExtensionsContract.MetaTranslationAttribute);

    public static INamedTypeSymbol? ResolveDefinitionFactory(Compilation compilation)
        => compilation.GetTypeByMetadataName(ExtensionsContract.MetaTranslationDefinitions);

    /// <summary>
    /// Resolves the builder and definition return types from the compilation.
    /// Used by <see cref="CallInterpreter"/> to route calls by return type.
    /// Returns null if the builder types aren't in the compilation references.
    /// </summary>
    public static ResolvedTypes? ResolveBuilderTypes(Compilation compilation)
    {
        var simple = compilation.GetTypeByMetadataName(ExtensionsContract.MetaSimpleBuilder);
        if (simple is null) return null; // Extensions not referenced

        return new ResolvedTypes(
            SimpleBuilder: simple,
            PluralBuilder: compilation.GetTypeByMetadataName(ExtensionsContract.MetaPluralBuilder),
            SelectBuilder: compilation.GetTypeByMetadataName(ExtensionsContract.MetaSelectBuilder),
            SelectPluralBuilder: compilation.GetTypeByMetadataName(ExtensionsContract.MetaSelectPluralBuilder),
            SimpleDefinition: compilation.GetTypeByMetadataName(ExtensionsContract.MetaSimpleDefinition),
            PluralDefinition: compilation.GetTypeByMetadataName(ExtensionsContract.MetaPluralDefinition),
            SelectDefinition: compilation.GetTypeByMetadataName(ExtensionsContract.MetaSelectDefinition),
            SelectPluralDefinition: compilation.GetTypeByMetadataName(ExtensionsContract.MetaSelectPluralDefinition));
    }
}

/// <summary>
/// Pre-resolved builder and definition type symbols.
/// Created once per compilation by <see cref="ScanTargets.ResolveBuilderTypes"/>.
/// Used by <see cref="CallInterpreter"/> to route calls by return type.
/// </summary>
internal sealed record ResolvedTypes(
    INamedTypeSymbol SimpleBuilder,
    INamedTypeSymbol? PluralBuilder,
    INamedTypeSymbol? SelectBuilder,
    INamedTypeSymbol? SelectPluralBuilder,
    INamedTypeSymbol? SimpleDefinition,
    INamedTypeSymbol? PluralDefinition,
    INamedTypeSymbol? SelectDefinition,
    INamedTypeSymbol? SelectPluralDefinition)
{
    /// <summary>
    /// Matches a return type against the pre-resolved builder types.
    /// Uses <see cref="ITypeSymbol.OriginalDefinition"/> to strip generic type arguments.
    /// </summary>
    public bool IsBuilder(ITypeSymbol returnType, out INamedTypeSymbol matched)
    {
        var orig = returnType.OriginalDefinition;
        if (Matches(orig, SimpleBuilder)) { matched = SimpleBuilder; return true; }
        if (Matches(orig, PluralBuilder)) { matched = PluralBuilder!; return true; }
        if (Matches(orig, SelectBuilder)) { matched = SelectBuilder!; return true; }
        if (Matches(orig, SelectPluralBuilder)) { matched = SelectPluralBuilder!; return true; }
        matched = null!;
        return false;
    }

    /// <summary>
    /// Matches a return type against the pre-resolved definition types (DefineXxx return types).
    /// </summary>
    public bool IsDefinitionType(ITypeSymbol returnType)
    {
        var orig = returnType.OriginalDefinition;
        return Matches(orig, SimpleDefinition)
            || Matches(orig, PluralDefinition)
            || Matches(orig, SelectDefinition)
            || Matches(orig, SelectPluralDefinition);
    }

    private static bool Matches(ITypeSymbol type, INamedTypeSymbol? target)
        => target is not null && SymbolEqualityComparer.Default.Equals(type, target);
}
