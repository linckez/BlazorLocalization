using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Analyzers.Scanning;

/// <summary>
/// Classifies what kind of translation usage was found for a given key.
/// </summary>
internal enum KeyUsageKind
{
    /// <summary>DefineSimple() or inline Translation(key, message)</summary>
    Simple,
    /// <summary>DefinePlural() or inline Translation(key, howMany)</summary>
    Plural,
    /// <summary>DefineSelect&lt;T&gt;()</summary>
    Select,
    /// <summary>DefineSelectPlural&lt;T&gt;()</summary>
    SelectPlural,
}

/// <summary>
/// Records one usage of a translation key in the compilation.
/// Collected during syntax node analysis; duplicates are reported immediately.
/// </summary>
internal readonly struct KeyUsage
{
    public Location Location { get; }
    public string? Message { get; }
    public KeyUsageKind Kind { get; }
    public bool IsStatic { get; }

    public KeyUsage(Location location, string? message, KeyUsageKind kind, bool isStatic)
    {
        Location = location;
        Message = message;
        Kind = kind;
        IsStatic = isStatic;
    }
}
