namespace BlazorLocalization.Extractor.Domain;

/// <summary>Where a translation definition was found.</summary>
public sealed record DefinitionSite(SourceFilePath File, int Line, DefinitionKind Kind, string? Context = null);

/// <summary>Where a translation reference (key usage) was found.</summary>
public sealed record ReferenceSite(SourceFilePath File, int Line, string? Context = null);
