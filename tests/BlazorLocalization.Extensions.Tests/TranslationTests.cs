using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Tests;

/// <summary>
/// Verifies the fluent <c>Translation()</c> API: resolution chain
/// (provider → inline translation → source text), SmartFormat placeholders,
/// and the four builder types (Simple, Plural, Select, SelectPlural).
/// Cache is pre-populated to avoid async warm-up jitter.
/// </summary>
public sealed class TranslationTests : IDisposable
{
    private const string CacheName = "TranslationTests";

    private readonly ServiceProvider _sp;
    private readonly IFusionCache _cache;
    private readonly IStringLocalizer _localizer;
    private readonly CultureInfo _prevCulture = CultureInfo.CurrentUICulture;

    public TranslationTests()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache(CacheName);
        _sp = services.BuildServiceProvider();

        _cache = _sp.GetRequiredService<IFusionCacheProvider>().GetCache(CacheName);
        _localizer = new ProviderBasedStringLocalizer(
            _cache,
            [],
            _sp.GetRequiredService<ILogger<ProviderBasedStringLocalizer>>());
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _prevCulture;
        _sp.Dispose();
    }

    [Fact]
    public void SimpleTranslation_ProviderHit_ReturnsTranslation()
    {
        _cache.Set("locale_en_Home.Title", "Translationd Title");

        var result = _localizer.Translation("Home.Title", "Default Title").ToString();

        result.Should().Be("Translationd Title");
    }

    [Fact]
    public void SimpleTranslation_ProviderMiss_ReturnsSourceText()
    {
        var result = _localizer.Translation("Missing.Key", "English Fallback").ToString();

        result.Should().Be("English Fallback");
    }

    [Fact]
    public void SimpleTranslation_InlineTranslation_FallsBackToInline()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("da");

        var result = _localizer.Translation("Home.Title", "Welcome")
            .For("da", "Velkommen")
            .ToString();

        result.Should().Be("Velkommen");
    }

    [Fact]
    public void SimpleTranslation_NamedPlaceholders_Resolved()
    {
        var result = _localizer.Translation("Greet", "Hello {Name}!", new { Name = "World" }).ToString();

        result.Should().Be("Hello World!");
    }

    [Fact]
    public void PluralTranslation_CorrectCategorySelected()
    {
        // English: howMany=1 → "one", howMany=5 → "other"
        _cache.Set("locale_en_Cart.Items_one", "1 item");
        _cache.Set("locale_en_Cart.Items_other", "{ItemCount} items");

        var singular = _localizer.Translation("Cart.Items", 1, replaceWith: new { ItemCount = 1 })
            .One("1 item").Other("{ItemCount} items").ToString();

        var plural = _localizer.Translation("Cart.Items", 5, replaceWith: new { ItemCount = 5 })
            .One("1 item").Other("{ItemCount} items").ToString();

        singular.Should().Be("1 item");
        plural.Should().Be("5 items");
    }

    [Fact]
    public void PluralTranslation_ExactMatchTakesPriority()
    {
        // Exact match for 0 should win over the CLDR "other" category.
        var result = _localizer.Translation("Cart.Items", 0)
            .Exactly(0, "Your cart is empty")
            .One("1 item")
            .Other("Several items")
            .ToString();

        result.Should().Be("Your cart is empty");
    }

    [Fact]
    public void SelectTranslation_MatchingCase()
    {
        _cache.Set("locale_en_Invite_Female", "{Name} invited you to her party");

        var result = _localizer.Translation("Invite", Gender.Female, new { Name = "Alice" })
            .When(Gender.Female, "{Name} invited you to her party")
            .When(Gender.Male, "{Name} invited you to his party")
            .Otherwise("{Name} invited you to their party")
            .ToString();

        result.Should().Be("Alice invited you to her party");
    }

    [Fact]
    public void SelectPluralTranslation_CompositeKey()
    {
        // Key convention: {key}_{SelectValue}_{pluralCategory}
        _cache.Set("locale_en_Inbox_Female_one", "She has {MessageCount} message");
        _cache.Set("locale_en_Inbox_Female_other", "She has {MessageCount} messages");

        var result = _localizer.Translation("Inbox", Gender.Female, 3, replaceWith: new { MessageCount = 3 })
            .When(Gender.Female).One("She has {MessageCount} message").Other("She has {MessageCount} messages")
            .When(Gender.Male).One("He has {MessageCount} message").Other("He has {MessageCount} messages")
            .Otherwise().One("They have {MessageCount} message").Other("They have {MessageCount} messages")
            .ToString();

        result.Should().Be("She has 3 messages");
    }

    private enum Gender { Female, Male, Other }
}
