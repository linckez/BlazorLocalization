using System.Globalization;
using BlazorLocalization.Extensions.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Tests;

/// <summary>
/// Verifies the provider resolution chain inside <see cref="ProviderBasedStringLocalizer"/>:
/// the async-behind-sync factory lambda that iterates providers, handles exceptions,
/// and caches results.
/// </summary>
public sealed class ProviderChainTests : IDisposable
{
    private const string CacheName = "ProviderChain";

    private readonly ServiceProvider _sp;
    private readonly CultureInfo _prevCulture = CultureInfo.CurrentUICulture;

    public ProviderChainTests()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache(CacheName);
        _sp = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _prevCulture;
        _sp.Dispose();
    }

    private ProviderBasedStringLocalizer CreateLocalizer(params ITranslationProvider[] providers) =>
        new(
            _sp.GetRequiredService<IFusionCacheProvider>().GetCache(CacheName),
            providers,
            _sp.GetRequiredService<ILogger<ProviderBasedStringLocalizer>>());

    [Fact]
    public async Task ProviderHit_ReturnsCachedTranslation()
    {
        var provider = new StubProvider(new() { { ("en", "Greeting"), "Hello" } });
        var localizer = CreateLocalizer(provider);

        // First call — cold miss (async factory fires in background).
        var first = localizer["Greeting"];

        // Give the background factory time to complete and cache the result.
        await Task.Delay(200);

        var second = localizer["Greeting"];

        second.ResourceNotFound.Should().BeFalse();
        second.Value.Should().Be("Hello");
    }

    [Fact]
    public async Task MultiProvider_FirstNonNullWins()
    {
        var providerA = new StubProvider(new());
        var providerB = new StubProvider(new() { { ("en", "Key"), "FromB" } });
        var localizer = CreateLocalizer(providerA, providerB);

        _ = localizer["Key"];
        await Task.Delay(200);

        var result = localizer["Key"];
        result.Value.Should().Be("FromB");
    }

    [Fact]
    public async Task TransientException_SkipsToNextProvider()
    {
        var failing = new ThrowingProvider();
        var fallback = new StubProvider(new() { { ("en", "Key"), "Fallback" } });
        var localizer = CreateLocalizer(failing, fallback);

        _ = localizer["Key"];
        await Task.Delay(200);

        var result = localizer["Key"];
        result.Value.Should().Be("Fallback");
    }

    [Fact]
    public async Task AllProvidersMiss_ReturnsResourceNotFound()
    {
        var empty = new StubProvider(new());
        var localizer = CreateLocalizer(empty);

        _ = localizer["Missing"];
        await Task.Delay(200);

        // After the factory completes with null, the key should still be not found
        // (null is not cached — SkipMemoryCacheWrite prevents ghost entries).
        var result = localizer["Missing"];
        result.ResourceNotFound.Should().BeTrue();
    }

    /// <summary>
    /// Dictionary-backed stub that simulates an async provider.
    /// </summary>
    private sealed class StubProvider(Dictionary<(string Culture, string Key), string> data) : ITranslationProvider
    {
        public Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default) =>
            Task.FromResult(data.TryGetValue((culture, key), out var value) ? value : null);
    }

    /// <summary>
    /// Provider that always throws <see cref="TranslationProviderTransientException"/>.
    /// </summary>
    private sealed class ThrowingProvider : ITranslationProvider
    {
        public Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct = default) =>
            throw new TranslationProviderTransientException("Failing", "Simulated failure", new Exception("inner"));
    }
}
