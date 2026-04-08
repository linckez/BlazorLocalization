using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Central registry of types we scan for.
/// Resolve once per compilation via <see cref="ResolveLocalizer"/> / <see cref="ResolveTranslationAttribute"/>.
/// </summary>
internal static class ScanTargets
{
    /// <summary>
    /// Resolves the scan targets against the compilation.
    /// Returns null for any type not found in compilation references.
    /// </summary>
    public static INamedTypeSymbol? ResolveLocalizer(Compilation compilation)
        => compilation.GetTypeByMetadataName(ExtensionsContract.MetaIStringLocalizer);

    public static INamedTypeSymbol? ResolveTranslationAttribute(Compilation compilation)
        => compilation.GetTypeByMetadataName(ExtensionsContract.MetaTranslationAttribute);

    public static INamedTypeSymbol? ResolveDefinitionFactory(Compilation compilation)
        => compilation.GetTypeByMetadataName(ExtensionsContract.MetaTranslationDefinitions);
}
