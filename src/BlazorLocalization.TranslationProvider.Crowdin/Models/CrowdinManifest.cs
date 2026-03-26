using System.Text.Json.Serialization;

namespace BlazorLocalization.TranslationProvider.Crowdin.Models;

/// <summary>
/// Represents the OTA distribution manifest fetched from
/// <c>{BaseUrl}/{DistributionHash}/manifest.json</c>.
/// </summary>
/// <param name="Languages">All languages enabled in the Crowdin project.</param>
/// <param name="Content">
/// Maps a Crowdin language code (e.g. <c>da</c>, <c>es-MX</c>) to its exported file paths.
/// Typically a single file per language; the provider uses the first entry.
/// </param>
public record CrowdinManifest(
    [property: JsonPropertyName("languages")] string[] Languages,
    [property: JsonPropertyName("content")] Dictionary<string, string[]> Content);
