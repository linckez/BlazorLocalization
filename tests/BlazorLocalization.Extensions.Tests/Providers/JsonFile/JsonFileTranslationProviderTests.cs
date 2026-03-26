using BlazorLocalization.Extensions.Providers.JsonFile;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Tests.Providers.JsonFile;

public sealed class JsonFileTranslationProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"blazor-loc-json-{Guid.NewGuid():N}");
    private readonly ServiceProvider _sp;

    public JsonFileTranslationProviderTests()
    {
        Directory.CreateDirectory(_tempDir);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache("JsonTest");
        _sp = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _sp.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ReturnsTranslation_FromJsonFile()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "da.json"),
            """{ "Home.Title": "Velkommen" }""");

        var provider = new JsonFileTranslationProvider(
            "JsonTest",
            _sp.GetRequiredService<IFusionCacheProvider>(),
            new ProviderBasedLocalizationOptions { CacheName = "JsonTest" },
            new JsonFileTranslationProviderOptions { TranslationsPath = _tempDir },
            _sp.GetRequiredService<ILogger<JsonFileTranslationProvider>>());

        var result = await provider.GetTranslationAsync("da", "Home.Title");

        result.Should().Be("Velkommen");
    }
}
