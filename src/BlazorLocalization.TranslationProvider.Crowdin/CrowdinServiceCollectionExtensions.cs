using BlazorLocalization.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.TranslationProvider.Crowdin;

public static class CrowdinServiceCollectionExtensions
{
    private const string DefaultProviderName = "Crowdin";

    /// <param name="builder">The builder returned by <c>AddProviderBasedLocalization()</c>.</param>
    extension(ProviderBasedLocalizationBuilder builder)
    {
        /// <summary>
        /// Registers a <see cref="CrowdinTranslationProvider"/> as an <see cref="ITranslationProvider"/>
        /// with the default provider name <c>"Crowdin"</c>, binding options from the
        /// <c>TranslationProviders:Crowdin</c> sub-section of the localization configuration.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddCrowdinTranslationProvider()
        {
            return builder.AddCrowdinTranslationProvider(DefaultProviderName);
        }

        /// <summary>
        /// Registers a named <see cref="CrowdinTranslationProvider"/> as an <see cref="ITranslationProvider"/>,
        /// binding options from the <c>TranslationProviders:{providerName}</c> sub-section of the
        /// localization configuration.
        /// </summary>
        /// <inheritdoc cref="AddCrowdinTranslationProvider(ProviderBasedLocalizationBuilder, string, Action{CrowdinTranslationProviderOptions})" path="/remarks"/>
        /// <param name="providerName">
        /// A unique name for this provider instance (e.g. <c>"MainApp"</c>, <c>"SharedLib"</c>).
        /// Used as the named-options key, cache key prefix, and configuration section name.
        /// </param>
        public ProviderBasedLocalizationBuilder AddCrowdinTranslationProvider(string providerName)
        {
            var section = builder.TranslationProvidersSection?.GetSection(providerName);
            if (section?.Exists() == true)
                builder.Services.Configure<CrowdinTranslationProviderOptions>(providerName, section);

            RegisterCrowdinProvider(builder, providerName);
            return builder;
        }

        /// <summary>
        /// Registers a <see cref="CrowdinTranslationProvider"/> as an <see cref="ITranslationProvider"/>
        /// with the default provider name <c>"Crowdin"</c>.
        /// </summary>
        /// <param name="configure">Configures <see cref="CrowdinTranslationProviderOptions"/>.</param>
        public ProviderBasedLocalizationBuilder AddCrowdinTranslationProvider(Action<CrowdinTranslationProviderOptions> configure)
        {
            return builder.AddCrowdinTranslationProvider(DefaultProviderName, configure);
        }

        /// <summary>
        /// Registers a <see cref="CrowdinTranslationProvider"/> as an <see cref="ITranslationProvider"/>
        /// with the default provider name <c>"Crowdin"</c>, bound to an <see cref="IConfiguration"/> section.
        /// </summary>
        /// <param name="configuration">
        /// A configuration section whose keys map to <see cref="CrowdinTranslationProviderOptions"/> properties
        /// (e.g. <c>builder.Configuration.GetSection("Localization:TranslationProviders:Crowdin")</c>).
        /// </param>
        public ProviderBasedLocalizationBuilder AddCrowdinTranslationProvider(IConfiguration configuration)
        {
            return builder.AddCrowdinTranslationProvider(DefaultProviderName, configuration);
        }

        /// <summary>
        /// Registers a named <see cref="CrowdinTranslationProvider"/> as an <see cref="ITranslationProvider"/>
        /// backed by the Crowdin OTA CDN.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Multiple Crowdin providers can be registered with different names, each pointing at a different
        /// OTA distribution. The <paramref name="providerName"/> is used to:
        /// </para>
        /// <list type="bullet">
        ///   <item>Resolve the correct <see cref="CrowdinTranslationProviderOptions"/> via named options.</item>
        ///   <item>Namespace cache keys (<c>crowdin:{providerName}:{culture}:{key}</c>) so providers don't collide.</item>
        ///   <item>Create a dedicated named <see cref="HttpClient"/> (<c>Crowdin:{providerName}</c>).</item>
        ///   <item>Identify the provider in exception messages and log output.</item>
        /// </list>
        /// </remarks>
        /// <param name="providerName">
        /// A unique name for this provider instance (e.g. <c>"MainApp"</c>, <c>"SharedLib"</c>).
        /// Used as the named-options key and cache key prefix.
        /// </param>
        /// <param name="configure">Configures <see cref="CrowdinTranslationProviderOptions"/>.</param>
        public ProviderBasedLocalizationBuilder AddCrowdinTranslationProvider(string providerName,
            Action<CrowdinTranslationProviderOptions> configure)
        {
            builder.Services.Configure<CrowdinTranslationProviderOptions>(providerName, opts => configure(opts));
            RegisterCrowdinProvider(builder, providerName);
            return builder;
        }

        /// <summary>
        /// Registers a named <see cref="CrowdinTranslationProvider"/> as an <see cref="ITranslationProvider"/>
        /// backed by the Crowdin OTA CDN, bound to an <see cref="IConfiguration"/> section.
        /// </summary>
        /// <inheritdoc cref="AddCrowdinTranslationProvider(ProviderBasedLocalizationBuilder, string, Action{CrowdinTranslationProviderOptions})" path="/remarks"/>
        /// <param name="providerName">
        /// A unique name for this provider instance (e.g. <c>"MainApp"</c>, <c>"SharedLib"</c>).
        /// Used as the named-options key and cache key prefix.
        /// </param>
        /// <param name="configuration">
        /// A configuration section whose keys map to <see cref="CrowdinTranslationProviderOptions"/> properties
        /// (e.g. <c>builder.Configuration.GetSection("Localization:TranslationProviders:MainApp")</c>).
        /// </param>
        public ProviderBasedLocalizationBuilder AddCrowdinTranslationProvider(string providerName,
            IConfiguration configuration)
        {
            builder.Services.Configure<CrowdinTranslationProviderOptions>(providerName, configuration);
            RegisterCrowdinProvider(builder, providerName);
            return builder;
        }
    }

    private static void RegisterCrowdinProvider(ProviderBasedLocalizationBuilder builder, string providerName)
    {
        builder.TrackProviderName(providerName);

        var httpClientName = $"Crowdin:{providerName}";

        builder.Services.AddHttpClient(httpClientName, client =>
        {
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
        });

        builder.Services.Add(ServiceDescriptor.Singleton<ITranslationProvider>(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<CrowdinTranslationProviderOptions>>();
            var resolvedOptions = optionsMonitor.Get(providerName);

            if (string.IsNullOrWhiteSpace(resolvedOptions.DistributionHash))
                throw new OptionsValidationException(
                    providerName,
                    typeof(CrowdinTranslationProviderOptions),
                    [$"Crowdin provider '{providerName}' requires a non-empty DistributionHash. " +
                     $"Set it in configuration (Localization:TranslationProviders:{providerName}:DistributionHash) " +
                     "or via the Action<CrowdinTranslationProviderOptions> overload."]);

            var otaClient = new CrowdinOtaClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                resolvedOptions,
                httpClientName,
                providerName,
                sp.GetRequiredService<IFusionCacheProvider>(),
                sp.GetRequiredService<ProviderBasedLocalizationOptions>(),
                sp.GetRequiredService<ILogger<CrowdinOtaClient>>());

            return new CrowdinTranslationProvider(
                otaClient,
                providerName,
                sp.GetRequiredService<IFusionCacheProvider>(),
                sp.GetRequiredService<ProviderBasedLocalizationOptions>(),
                sp.GetRequiredService<ILogger<CrowdinTranslationProvider>>());
        }));
    }
}
