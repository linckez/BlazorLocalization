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

    private readonly ServiceProvider _sp;
    private readonly ProviderBasedStringLocalizer _localizer;

    public ProviderBasedStringLocalizerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache(CacheName);
        _sp = services.BuildServiceProvider();

        _localizer = new ProviderBasedStringLocalizer(
            _sp.GetRequiredService<IFusionCacheProvider>().GetCache(CacheName),
            [],
            _sp.GetRequiredService<ILogger<ProviderBasedStringLocalizer>>());
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void PositionalIndexer_MalformedPlaceholder_ReturnsUnformattedTranslation()
    {
        // A translator introduces {1} but the caller only passes one argument —
        // string.Format() would throw FormatException and crash the host app.
        var cache = _sp.GetRequiredService<IFusionCacheProvider>().GetCache(CacheName);
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        cache.Set($"locale_{culture}_BadKey", "You have {1} items");

        var result = _localizer["BadKey", 42];

        result.ResourceNotFound.Should().BeFalse();
        result.Value.Should().Be("You have {1} items");
    }
}
