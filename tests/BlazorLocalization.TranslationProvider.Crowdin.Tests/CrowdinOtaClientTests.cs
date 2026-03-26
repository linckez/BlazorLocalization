using BlazorLocalization.TranslationProvider.Crowdin.Models;
using FluentAssertions;

namespace BlazorLocalization.TranslationProvider.Crowdin.Tests;

public sealed class CrowdinOtaClientTests
{
    private static CrowdinManifest CreateManifest(params (string culture, string path)[] entries)
    {
        var content = entries.ToDictionary(e => e.culture, e => new[] { e.path });
        return new CrowdinManifest(entries.Select(e => e.culture).ToArray(), content);
    }

    [Theory]
    [InlineData("es-MX", "es-MX", "/content/es-MX/file.json", "exact match")]
    [InlineData("es-MX", "es", "/content/es/file.json", "two-letter fallback")]
    public void ResolveCulturePath_MatchFound_ReturnsFirstPath(
        string requestedCulture, string manifestCulture, string manifestPath, string _)
    {
        var manifest = CreateManifest((manifestCulture, manifestPath));

        var result = CrowdinOtaClient.ResolveCulturePath(manifest, requestedCulture);

        result.Should().Be(manifestPath);
    }

    [Fact]
    public void ResolveCulturePath_NeitherExactNorTwoLetter_ReturnsNull()
    {
        var manifest = CreateManifest(("fr", "/content/fr/file.json"));

        var result = CrowdinOtaClient.ResolveCulturePath(manifest, "es-MX");

        result.Should().BeNull();
    }
}
