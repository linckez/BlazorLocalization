using BlazorLocalization.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorLocalization.TranslationProvider.Crowdin.Tests;

public sealed class CrowdinServiceCollectionTests
{
    [Fact]
    public void Resolve_EmptyDistributionHash_ThrowsOptionsValidationException()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services
            .AddProviderBasedLocalization(config)
            .AddCrowdinTranslationProvider("Empty", o =>
            {
                o.DistributionHash = "";
            });

        using var sp = services.BuildServiceProvider();
        var act = () => sp.GetServices<ITranslationProvider>().ToList();

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Empty*DistributionHash*");
    }
}
