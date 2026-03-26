using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Providers.PoFile;

public static class PoFileServiceCollectionExtensions
{
    private const string DefaultProviderName = "PoFile";

    /// <param name="builder">The builder returned by <c>AddProviderBasedLocalization()</c>.</param>
    extension(ProviderBasedLocalizationBuilder builder)
    {
        /// <summary>
        /// Registers a PO file translation provider with the default name <c>"PoFile"</c>,
        /// binding options from <c>TranslationProviders:PoFile</c>.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddPoFileTranslationProvider()
        {
            return builder.AddPoFileTranslationProvider(DefaultProviderName);
        }

        /// <summary>
        /// Registers a named PO file translation provider, binding options from
        /// <c>TranslationProviders:{providerName}</c>.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddPoFileTranslationProvider(string providerName)
        {
            var section = builder.TranslationProvidersSection?.GetSection(providerName);
            if (section?.Exists() == true)
                builder.Services.Configure<PoFileTranslationProviderOptions>(providerName, section);

            RegisterPoFileProvider(builder, providerName);
            return builder;
        }

        /// <summary>
        /// Registers a PO file translation provider with the default name <c>"PoFile"</c>.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddPoFileTranslationProvider(Action<PoFileTranslationProviderOptions> configure)
        {
            return builder.AddPoFileTranslationProvider(DefaultProviderName, configure);
        }

        /// <summary>
        /// Registers a PO file translation provider with the default name <c>"PoFile"</c>,
        /// bound to an <see cref="IConfiguration"/> section.
        /// </summary>
        public ProviderBasedLocalizationBuilder AddPoFileTranslationProvider(IConfiguration configuration)
        {
            return builder.AddPoFileTranslationProvider(DefaultProviderName, configuration);
        }

        /// <summary>
        /// Registers a named PO file translation provider.
        /// </summary>
        /// <param name="providerName">
        /// A unique name for this provider instance. Used as the named-options key and cache key prefix.
        /// </param>
        /// <param name="configure">Configures <see cref="PoFileTranslationProviderOptions"/>.</param>
        public ProviderBasedLocalizationBuilder AddPoFileTranslationProvider(string providerName,
            Action<PoFileTranslationProviderOptions> configure)
        {
            builder.Services.Configure<PoFileTranslationProviderOptions>(providerName, opts => configure(opts));
            RegisterPoFileProvider(builder, providerName);
            return builder;
        }

        /// <summary>
        /// Registers a named PO file translation provider, bound to an <see cref="IConfiguration"/> section.
        /// </summary>
        /// <param name="providerName">
        /// A unique name for this provider instance. Used as the named-options key and cache key prefix.
        /// </param>
        /// <param name="configuration">
        /// A configuration section whose keys map to <see cref="PoFileTranslationProviderOptions"/> properties.
        /// </param>
        public ProviderBasedLocalizationBuilder AddPoFileTranslationProvider(string providerName,
            IConfiguration configuration)
        {
            builder.Services.Configure<PoFileTranslationProviderOptions>(providerName, configuration);
            RegisterPoFileProvider(builder, providerName);
            return builder;
        }
    }

    private static void RegisterPoFileProvider(ProviderBasedLocalizationBuilder builder, string providerName)
    {
        builder.TrackProviderName(providerName);

        builder.Services.Add(ServiceDescriptor.Singleton<ITranslationProvider>(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<PoFileTranslationProviderOptions>>();
            var resolvedOptions = optionsMonitor.Get(providerName);

            if (string.IsNullOrWhiteSpace(resolvedOptions.TranslationsPath))
                throw new OptionsValidationException(
                    providerName,
                    typeof(PoFileTranslationProviderOptions),
                    [$"PO file provider '{providerName}' requires a non-empty TranslationsPath. " +
                     $"Set it in configuration (Localization:TranslationProviders:{providerName}:TranslationsPath) " +
                     "or via the Action<PoFileTranslationProviderOptions> overload."]);

            return new PoFileTranslationProvider(
                providerName,
                sp.GetRequiredService<IFusionCacheProvider>(),
                sp.GetRequiredService<ProviderBasedLocalizationOptions>(),
                resolvedOptions,
                sp.GetRequiredService<ILogger<PoFileTranslationProvider>>());
        }));
    }
}
