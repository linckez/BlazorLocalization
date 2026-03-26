using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Providers.JsonFile;

public static class JsonFileServiceCollectionExtensions
{
    private const string DefaultProviderName = "JsonFile";

    /// <param name="builder">The builder returned by <c>AddProviderBasedLocalization()</c>.</param>
    extension(ProviderBasedLocalizationBuilder builder)
    {
        /// <summary>
        /// Registers a JSON file translation provider with the default name <c>"JsonFile"</c>,
        /// binding options from <c>TranslationProviders:JsonFile</c>.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddJsonFileTranslationProvider()
        {
            return builder.AddJsonFileTranslationProvider(DefaultProviderName);
        }

        /// <summary>
        /// Registers a named JSON file translation provider, binding options from
        /// <c>TranslationProviders:{providerName}</c>.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddJsonFileTranslationProvider(string providerName)
        {
            var section = builder.TranslationProvidersSection?.GetSection(providerName);
            if (section?.Exists() == true)
                builder.Services.Configure<JsonFileTranslationProviderOptions>(providerName, section);

            RegisterJsonFileProvider(builder, providerName);
            return builder;
        }

        /// <summary>
        /// Registers a JSON file translation provider with the default name <c>"JsonFile"</c>.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddJsonFileTranslationProvider(Action<JsonFileTranslationProviderOptions> configure)
        {
            return builder.AddJsonFileTranslationProvider(DefaultProviderName, configure);
        }

        /// <summary>
        /// Registers a JSON file translation provider with the default name <c>"JsonFile"</c>,
        /// bound to an <see cref="IConfiguration"/> section.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddJsonFileTranslationProvider(IConfiguration configuration)
        {
            return builder.AddJsonFileTranslationProvider(DefaultProviderName, configuration);
        }

        /// <summary>
        /// Registers a named JSON file translation provider.
        /// </summary>
        /// <param name="providerName">
        /// A unique name for this provider instance. Used as the named-options key and cache key prefix.
        /// </param>
        /// <param name="configure">Configures <see cref="JsonFileTranslationProviderOptions"/>.</param>
        public ProviderBasedLocalizationBuilder AddJsonFileTranslationProvider(string providerName,
            Action<JsonFileTranslationProviderOptions> configure)
        {
            builder.Services.Configure<JsonFileTranslationProviderOptions>(providerName, opts => configure(opts));
            RegisterJsonFileProvider(builder, providerName);
            return builder;
        }

        /// <summary>
        /// Registers a named JSON file translation provider, bound to an <see cref="IConfiguration"/> section.
        /// </summary>
        /// <param name="providerName">
        /// A unique name for this provider instance. Used as the named-options key and cache key prefix.
        /// </param>
        /// <param name="configuration">
        /// A configuration section whose keys map to <see cref="JsonFileTranslationProviderOptions"/> properties.
        /// </param>
        public ProviderBasedLocalizationBuilder AddJsonFileTranslationProvider(string providerName,
            IConfiguration configuration)
        {
            builder.Services.Configure<JsonFileTranslationProviderOptions>(providerName, configuration);
            RegisterJsonFileProvider(builder, providerName);
            return builder;
        }
    }

    private static void RegisterJsonFileProvider(ProviderBasedLocalizationBuilder builder, string providerName)
    {
        builder.TrackProviderName(providerName);

        builder.Services.Add(ServiceDescriptor.Singleton<ITranslationProvider>(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<JsonFileTranslationProviderOptions>>();
            var resolvedOptions = optionsMonitor.Get(providerName);

            if (string.IsNullOrWhiteSpace(resolvedOptions.TranslationsPath))
                throw new OptionsValidationException(
                    providerName,
                    typeof(JsonFileTranslationProviderOptions),
                    [$"JSON file provider '{providerName}' requires a non-empty TranslationsPath. " +
                     $"Set it in configuration (Localization:TranslationProviders:{providerName}:TranslationsPath) " +
                     "or via the Action<JsonFileTranslationProviderOptions> overload."]);

            return new JsonFileTranslationProvider(
                providerName,
                sp.GetRequiredService<IFusionCacheProvider>(),
                sp.GetRequiredService<ProviderBasedLocalizationOptions>(),
                resolvedOptions,
                sp.GetRequiredService<ILogger<JsonFileTranslationProvider>>());
        }));
    }
}
