namespace BlazorLocalization.TranslationProvider.Crowdin;

/// <summary>
/// Configuration for the Crowdin OTA CDN translation provider.
/// </summary>
public sealed class CrowdinTranslationProviderOptions
{
    /// <summary>
    /// The distribution hash from the Crowdin dashboard (Content Delivery → OTA).
    /// This is the unique identifier for your OTA distribution.
    /// </summary>
    public required string DistributionHash { get; set; }

    /// <summary>
    /// Base URL for the Crowdin CDN. Override only for self-hosted Crowdin Enterprise instances.
    /// </summary>
    public string BaseUrl { get; set; } = "https://distributions.crowdin.net";

}
