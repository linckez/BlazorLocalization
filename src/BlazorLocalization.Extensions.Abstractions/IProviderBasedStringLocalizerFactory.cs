using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions;

/// <summary>
/// Marker interface for the provider-based string localizer factory.
/// Used by analyzers to detect whether the project uses BlazorLocalization's
/// flat key namespace (where <c>IStringLocalizer&lt;T&gt;</c> type parameter has no effect).
/// </summary>
public interface IProviderBasedStringLocalizerFactory : IStringLocalizerFactory;
