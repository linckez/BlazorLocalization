using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Analyzers.Scanning;

/// <summary>
/// Resolves Microsoft.Extensions.Localization symbols from the compilation.
/// Type resolution uses typeof() for compile-time safety (DLLs packed in analyzer NuGet).
/// Method/parameter names are centralized constants validated by contract tests
/// (GetString is an extension method — can't nameof()).
/// </summary>
internal sealed class MicrosoftLocalizationSymbols
{
    // Type metadata names — typeof() gives compile errors on rename
    private static readonly string IStringLocalizerMetadataName = typeof(IStringLocalizer).FullName!;
    private static readonly string IStringLocalizerGenericMetadataName = typeof(IStringLocalizer<>).FullName!;

    // Type name — typeof() gives compile errors on rename. Used for syntax-level filtering.
    public static readonly string IStringLocalizerTypeName = typeof(IStringLocalizer).Name;

    // Method/parameter names — GetString is an extension method, can't nameof().
    // Validated by contract tests.
    public const string GetStringMethodName = "GetString";
    public const string NameParameterName = "name";

    /// <summary>Non-generic <c>IStringLocalizer</c>.</summary>
    public INamedTypeSymbol? IStringLocalizerType { get; }

    /// <summary>Generic <c>IStringLocalizer&lt;T&gt;</c>.</summary>
    public INamedTypeSymbol? IStringLocalizerGenericType { get; }

    public MicrosoftLocalizationSymbols(Compilation compilation)
    {
        IStringLocalizerType = compilation.GetTypeByMetadataName(IStringLocalizerMetadataName);
        IStringLocalizerGenericType = compilation.GetTypeByMetadataName(IStringLocalizerGenericMetadataName);
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> is or implements <c>IStringLocalizer</c>.
    /// </summary>
    public bool IsStringLocalizerType(ITypeSymbol? type)
    {
        if (type is null || IStringLocalizerType is null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, IStringLocalizerType))
            return true;

        if (IStringLocalizerGenericType is not null
            && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, IStringLocalizerGenericType))
            return true;

        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, IStringLocalizerType))
                return true;
            if (IStringLocalizerGenericType is not null
                && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, IStringLocalizerGenericType))
                return true;
        }

        return false;
    }
}
