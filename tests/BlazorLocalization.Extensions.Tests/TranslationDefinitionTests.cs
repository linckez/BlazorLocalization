using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace BlazorLocalization.Extensions.Tests;

/// <summary>
/// Verifies the reusable <c>Translate()</c> definition API: definitions resolve
/// through the same chain as <c>Translation()</c> (provider → inline → source text),
/// and all four builder types (Simple, Plural, Select, SelectPlural) work correctly
/// when defined once and used via <c>Loc.Translate(definition)</c>.
/// </summary>
public sealed class TranslationDefinitionTests : IDisposable
{
    private const string CacheName = "TranslationDefinitionTests";

    private readonly ServiceProvider _sp;
    private readonly IFusionCache _cache;
    private readonly IStringLocalizer _localizer;
    private readonly CultureInfo _prevCulture = CultureInfo.CurrentUICulture;

    public TranslationDefinitionTests()
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

    // ── Simple ──────────────────────────────────────────────────────

    private static readonly Translation.Definitions.SimpleDefinitionBuilder SaveButton =
        Translate.Simple("Common.Save", "Save")
            .For("da", "Gem")
            .For("de", "Speichern");

    [Fact]
    public void SimpleDefinition_ProviderHit_ReturnsTranslation()
    {
        _cache.Set("locale_en_Common.Save", "Save (provider)");

        var result = _localizer.Translate(SaveButton);

        result.Should().Be("Save (provider)");
    }

    [Fact]
    public void SimpleDefinition_ProviderMiss_ReturnsSourceText()
    {
        var result = _localizer.Translate(SaveButton);

        result.Should().Be("Save");
    }

    [Fact]
    public void SimpleDefinition_InlineTranslation_FallsBackToInline()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("da");

        var result = _localizer.Translate(SaveButton);

        result.Should().Be("Gem");
    }

    [Fact]
    public void SimpleDefinition_WithPlaceholders_Resolved()
    {
        var greeting = Translate.Simple("Greet", "Hello {Name}!");

        var result = _localizer.Translate(greeting, replaceWith: new { Name = "World" });

        result.Should().Be("Hello World!");
    }

    // ── Plural ──────────────────────────────────────────────────────

    private static readonly Translation.Definitions.PluralDefinitionBuilder CartItems =
        Translate.Plural("Cart.Items")
            .One("1 item")
            .Other("{ItemCount} items")
            .For("da")
            .One("1 vare")
            .Other("{ItemCount} varer");

    [Fact]
    public void PluralDefinition_CorrectCategorySelected()
    {
        _cache.Set("locale_en_Cart.Items_one", "1 item");
        _cache.Set("locale_en_Cart.Items_other", "{ItemCount} items");

        var singular = _localizer.Translate(CartItems, howMany: 1, replaceWith: new { ItemCount = 1 });
        var plural = _localizer.Translate(CartItems, howMany: 5, replaceWith: new { ItemCount = 5 });

        singular.Should().Be("1 item");
        plural.Should().Be("5 items");
    }

    [Fact]
    public void PluralDefinition_SourceTextFallback()
    {
        var result = _localizer.Translate(CartItems, howMany: 1);

        result.Should().Be("1 item");
    }

    [Fact]
    public void PluralDefinition_InlineTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("da");

        var result = _localizer.Translate(CartItems, howMany: 5, replaceWith: new { ItemCount = 5 });

        result.Should().Be("5 varer");
    }

    [Fact]
    public void PluralDefinition_ExactMatchTakesPriority()
    {
        var withExact = Translate.Plural("Cart.Exact")
            .Exactly(0, "Your cart is empty")
            .One("1 item")
            .Other("Several items");

        var result = _localizer.Translate(withExact, howMany: 0);

        result.Should().Be("Your cart is empty");
    }

    // ── Select ──────────────────────────────────────────────────────

    private static readonly Translation.Definitions.SelectDefinitionBuilder<Gender> GreetingDef =
        Translate.Select<Gender>("Invite")
            .When(Gender.Female, "{Name} invited you to her party")
            .When(Gender.Male, "{Name} invited you to his party")
            .Otherwise("{Name} invited you to their party");

    [Fact]
    public void SelectDefinition_MatchingCase()
    {
        _cache.Set("locale_en_Invite_Female", "{Name} invited you to her party");

        var result = _localizer.Translate(GreetingDef, Gender.Female, replaceWith: new { Name = "Alice" });

        result.Should().Be("Alice invited you to her party");
    }

    [Fact]
    public void SelectDefinition_OtherwiseFallback()
    {
        var result = _localizer.Translate(GreetingDef, Gender.Other, replaceWith: new { Name = "Sam" });

        result.Should().Be("Sam invited you to their party");
    }

    [Fact]
    public void SelectDefinition_InlineTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("da");

        var def = Translate.Select<Gender>("Greet.Select")
            .When(Gender.Female, "Welcome, ma'am!")
            .Otherwise("Welcome!")
            .For("da")
            .When(Gender.Female, "Velkommen, frue!")
            .Otherwise("Velkommen!");

        var result = _localizer.Translate(def, Gender.Female);

        result.Should().Be("Velkommen, frue!");
    }

    // ── SelectPlural ────────────────────────────────────────────────

    [Fact]
    public void SelectPluralDefinition_CompositeKey()
    {
        _cache.Set("locale_en_Inbox_Female_one", "She has {MessageCount} message");
        _cache.Set("locale_en_Inbox_Female_other", "She has {MessageCount} messages");

        var def = Translate.SelectPlural<Gender>("Inbox")
            .When(Gender.Female).One("She has {MessageCount} message").Other("She has {MessageCount} messages")
            .When(Gender.Male).One("He has {MessageCount} message").Other("He has {MessageCount} messages")
            .Otherwise().One("They have {MessageCount} message").Other("They have {MessageCount} messages");

        var result = _localizer.Translate(def, Gender.Female, 3, replaceWith: new { MessageCount = 3 });

        result.Should().Be("She has 3 messages");
    }

    [Fact]
    public void SelectPluralDefinition_SourceTextFallback()
    {
        var def = Translate.SelectPlural<Gender>("Inbox.Def")
            .When(Gender.Female).One("She has {N} message").Other("She has {N} messages")
            .Otherwise().One("They have {N} message").Other("They have {N} messages");

        var result = _localizer.Translate(def, Gender.Female, 1, replaceWith: new { N = 1 });

        result.Should().Be("She has 1 message");
    }

    // ── Reuse guarantee ─────────────────────────────────────────────

    [Fact]
    public void Definitions_AreReusable_MultipleCallsSameResult()
    {
        var def = Translate.Simple("Reuse.Test", "Hello!");

        var result1 = _localizer.Translate(def);
        var result2 = _localizer.Translate(def);

        result1.Should().Be("Hello!");
        result2.Should().Be("Hello!");
        ReferenceEquals(result1, result2).Should().BeTrue("same string instance from same source text");
    }

    private enum Gender { Female, Male, Other }
}
