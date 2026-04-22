namespace BlazorLocalization.Extensions.Exceptions;

/// <summary>
/// A transient provider failure that is expected to self-heal — network timeouts,
/// DNS resolution errors, HTTP 5xx from the translation CDN, etc.
/// </summary>
/// <remarks>
/// FusionCache's fail-safe will serve the last known good value while this condition persists.
/// Logged at <c>Debug</c> level to avoid log noise during temporary outages.
/// </remarks>
public sealed class TranslationProviderTransientException(
    string providerName,
    string message,
    Exception innerException)
    : TranslationProviderException(providerName, message, innerException);
