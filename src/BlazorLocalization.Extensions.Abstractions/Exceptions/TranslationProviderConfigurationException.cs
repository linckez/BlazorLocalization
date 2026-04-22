namespace BlazorLocalization.Extensions.Exceptions;

/// <summary>
/// A provider failure caused by misconfiguration that will never self-heal —
/// wrong distribution hash, mismatched export format, invalid credentials, etc.
/// </summary>
/// <remarks>
/// Requires developer intervention. Logged at <c>Warning</c> level by
/// ProviderBasedStringLocalizer so it surfaces in default log output.
/// </remarks>
public sealed class TranslationProviderConfigurationException(
    string providerName,
    string message,
    Exception innerException)
    : TranslationProviderException(providerName, message, innerException);
