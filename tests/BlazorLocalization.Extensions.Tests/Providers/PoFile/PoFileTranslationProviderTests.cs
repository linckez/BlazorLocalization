using BlazorLocalization.Extensions.Providers.PoFile;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Tests.Providers.PoFile;

public sealed class PoFileTranslationProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"blazor-loc-po-{Guid.NewGuid():N}");
    private readonly ServiceProvider _sp;

    public PoFileTranslationProviderTests()
    {
        Directory.CreateDirectory(_tempDir);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache("PoTest");
        _sp = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _sp.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ReturnsTranslation_FromPoFile()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "da.po"),
            """
            msgid "Home.Title"
            msgstr "Velkommen"
            """);

        var provider = new PoFileTranslationProvider(
            "PoTest",
            _sp.GetRequiredService<IFusionCacheProvider>(),
            new ProviderBasedLocalizationOptions { CacheName = "PoTest" },
            new PoFileTranslationProviderOptions { TranslationsPath = _tempDir },
            _sp.GetRequiredService<ILogger<PoFileTranslationProvider>>());

        var result = await provider.GetTranslationAsync("da", "Home.Title");

        result.Should().Be("Velkommen");
    }
}
