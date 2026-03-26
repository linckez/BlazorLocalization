using BlazorLocalization.Extensions.Providers.PoFile;
using FluentAssertions;

namespace BlazorLocalization.Extensions.Tests.Providers.PoFile;

public sealed class PoPluralMappingTests
{
    [Theory]
    [InlineData("en", new[] { "one", "other" })]
    [InlineData("pl", new[] { "one", "few", "many", "other" })]
    [InlineData("ar", new[] { "zero", "one", "two", "few", "many", "other" })]
    [InlineData("ja", new[] { "other" })]
    [InlineData("ru", new[] { "one", "few", "many", "other" })]
    [InlineData("cs", new[] { "one", "few", "other" })]
    [InlineData("lv", new[] { "zero", "one", "other" })]
    public void GetCategories_ReturnsCanonicalOrder(string locale, string[] expected)
    {
        PoPluralMapping.GetCategories(locale).Should().Equal(expected);
    }

    [Theory]
    [InlineData("pl", 0, "one")]
    [InlineData("pl", 1, "few")]
    [InlineData("pl", 2, "many")]
    [InlineData("pl", 3, "other")]
    [InlineData("pl", 4, null)]
    [InlineData("en", 0, "one")]
    [InlineData("en", 1, "other")]
    [InlineData("en", 2, null)]
    [InlineData("ar", 5, "other")]
    public void MapPoPluralIndex_MapsCorrectly(string locale, int index, string? expected)
    {
        PoPluralMapping.MapPoPluralIndex(locale, index).Should().Be(expected);
    }
}
