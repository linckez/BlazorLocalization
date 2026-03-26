namespace BlazorLocalization.Extensions.Exceptions;

/// <summary>
/// Base exception for all <see cref="ITranslationProvider"/> failures.
/// Carries the <see cref="ProviderName"/> so log entries identify which provider failed
/// (e.g. "Crowdin", "Database") without inspecting the stack trace.
/// </summary>
public abstract class TranslationProviderException(
    string providerName,
    string message,
    Exception innerException) : Exception(message, innerException)
{
    /// <summary>
    /// Human-readable name of the provider that threw (e.g. <c>"Crowdin"</c>).
    /// </summary>
    public string ProviderName { get; } = providerName;
}
