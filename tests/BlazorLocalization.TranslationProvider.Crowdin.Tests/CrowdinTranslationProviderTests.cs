using System.Net;
using BlazorLocalization.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.TranslationProvider.Crowdin.Tests;

public sealed class CrowdinTranslationProviderTests : IDisposable
{
    private const string ProviderName = "test";
    private const string Hash = "hash123";
    private const string BaseUrl = "https://cdn.test";
    private const string CacheName = "CrowdinTestCache";

    private readonly FakeHandler _handler = new();
    private readonly ServiceProvider _sp;
    private readonly ProviderBasedLocalizationOptions _cacheOptions = new() { CacheName = CacheName };

    public CrowdinTranslationProviderTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache(CacheName);
        services.AddHttpClient($"Crowdin:{ProviderName}")
            .ConfigurePrimaryHttpMessageHandler(() => _handler);
        _sp = services.BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task GetTranslationAsync_HappyPath_ReturnsFannedOutValue()
    {
        RegisterManifest(("en", "/content/en/file.xml"));
        _handler.Register($"{BaseUrl}/{Hash}/content/en/file.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
              <string name="greeting">Hello</string>
              <string name="farewell">Goodbye</string>
            </resources>
            """);

        var provider = CreateProvider();

        var result = await provider.GetTranslationAsync("en", "greeting");

        result.Should().Be("Hello");
    }

    [Fact]
    public async Task GetTranslationAsync_CultureNotInManifest_ReturnsNull()
    {
        RegisterManifest(("fr", "/content/fr/file.xml"));

        var provider = CreateProvider();

        var result = await provider.GetTranslationAsync("de", "greeting");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTranslationAsync_HttpException_ReturnsNullWithoutCrashing()
    {
        RegisterManifest(("en", "/content/en/file.xml"));
        _handler.RegisterFailure($"{BaseUrl}/{Hash}/content/en/file.xml", new HttpRequestException("CDN down"));

        var provider = CreateProvider();

        var result = await provider.GetTranslationAsync("en", "greeting");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTranslationAsync_WrongExportFormat_ReturnsNullWithoutCrashing()
    {
        // Simulates a CDN Distribution configured with XLIFF instead of Android XML.
        // XLIFF is valid XML but contains <trans-unit>, not <string> — parser returns empty dict.
        RegisterManifest(("en", "/content/en/file.xliff"));
        _handler.Register($"{BaseUrl}/{Hash}/content/en/file.xliff", """
            <?xml version="1.0" encoding="utf-8"?>
            <xliff version="1.2">
              <file source-language="en" target-language="en">
                <body>
                  <trans-unit id="greeting">
                    <source>Hello</source>
                    <target>Hello</target>
                  </trans-unit>
                </body>
              </file>
            </xliff>
            """);

        var provider = CreateProvider();

        var result = await provider.GetTranslationAsync("en", "greeting");

        // Parser finds zero <string> elements → no keys fanned out → null
        result.Should().BeNull();
    }

    private CrowdinTranslationProvider CreateProvider()
    {
        var options = new CrowdinTranslationProviderOptions
        {
            DistributionHash = Hash,
            BaseUrl = BaseUrl
        };

        var otaClient = new CrowdinOtaClient(
            _sp.GetRequiredService<IHttpClientFactory>(),
            options,
            $"Crowdin:{ProviderName}",
            ProviderName,
            _sp.GetRequiredService<IFusionCacheProvider>(),
            _cacheOptions,
            _sp.GetRequiredService<ILogger<CrowdinOtaClient>>());

        return new CrowdinTranslationProvider(
            otaClient,
            ProviderName,
            _sp.GetRequiredService<IFusionCacheProvider>(),
            _cacheOptions,
            _sp.GetRequiredService<ILogger<CrowdinTranslationProvider>>());
    }

    private void RegisterManifest(params (string culture, string path)[] entries)
    {
        var contentEntries = string.Join(",\n",
            entries.Select(e => $"    \"{e.culture}\": [\"{e.path}\"]"));
        var languages = string.Join(", ", entries.Select(e => $"\"{e.culture}\""));

        _handler.Register($"{BaseUrl}/{Hash}/manifest.json", $$"""
            {
              "languages": [{{languages}}],
              "content": {
            {{contentEntries}}
              }
            }
            """);
    }

    /// <summary>
    /// Minimal HTTP handler that returns canned responses or throws based on URL.
    /// </summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);

        public void Register(string url, string content) => _responses[url] = content;
        public void RegisterFailure(string url, Exception ex) => _failures[url] = ex;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();

            if (_failures.TryGetValue(url, out var ex))
                return Task.FromException<HttpResponseMessage>(ex);

            if (_responses.TryGetValue(url, out var content))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
