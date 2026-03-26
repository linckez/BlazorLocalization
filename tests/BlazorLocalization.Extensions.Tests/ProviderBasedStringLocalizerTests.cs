using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Tests;

/// <summary>
/// Verifies the "never crash the host" contract: every public method on
/// <see cref="ProviderBasedStringLocalizer"/> must absorb failures gracefully.
/// </summary>
public sealed class ProviderBasedStringLocalizerTests : IDisposable
{
    private const string CacheName = "SafetyTests";

    private readonly CultureInfo _prevCulture;
    private readonly ServiceProvider _sp;
    private readonly ProviderBasedStringLocalizer _localizer;

    public ProviderBasedStringLocalizerTests()
    {
        _prevCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache(CacheName);
        _sp = services.BuildServiceProvider();

        _localizer = new ProviderBasedStringLocalizer(
            _sp.GetRequiredService<IFusionCacheProvider>().GetCache(CacheName),
            [],
            _sp.GetRequiredService<ILogger<ProviderBasedStringLocalizer>>());
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _prevCulture;
        _sp.Dispose();
    }

    [Fact]
    public void PositionalIndexer_MalformedPlaceholder_ReturnsUnformattedTranslation()
    {
        // A translator introduces {1} but the caller only passes one argument —
        // string.Format() would throw FormatException and crash the host app.
        var cache = _sp.GetRequiredService<IFusionCacheProvider>().GetCache(CacheName);
        cache.Set("locale_en_BadKey", "You have {1} items");

        var result = _localizer["BadKey", 42];

        result.ResourceNotFound.Should().BeFalse();
        result.Value.Should().Be("You have {1} items");
    }
}
