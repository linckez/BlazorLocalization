using BlazorLocalization.Analyzers.Scanning;
using FluentAssertions;

namespace BlazorLocalization.Analyzers.Tests;

public class KeyToIdentifierTests
{
    [Theory]
    [InlineData("Obs.Save", "ObsSave")]
    [InlineData("Common.Save", "CommonSave")]
    [InlineData("save_button", "SaveButton")]
    [InlineData("save-button", "SaveButton")]
    [InlineData("save button", "SaveButton")]
    [InlineData("Home.Title", "HomeTitle")]
    [InlineData("Enum.FlightStatus_OnTime", "EnumFlightStatusOnTime")]
    [InlineData("saveButton", "SaveButton")]
    [InlineData("SaveButton", "SaveButton")]
    [InlineData("SAVE", "SAVE")]
    [InlineData("a", "A")]
    [InlineData("a.b.c", "ABC")]
    public void Converts_key_to_PascalCase(string key, string expected)
    {
        KeyToIdentifier.ToFieldName(key).Should().Be(expected);
    }

    [Theory]
    [InlineData("123", "Translation123")]
    [InlineData("1.2.3", "Translation123")]
    [InlineData("0_items", "Translation0Items")]
    public void Prefixes_digit_leading_results(string key, string expected)
    {
        KeyToIdentifier.ToFieldName(key).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("_._")]
    public void Returns_fallback_for_degenerate_keys(string key)
    {
        KeyToIdentifier.ToFieldName(key).Should().Be("TranslationDefinition");
    }

    [Fact]
    public void Handles_null_key()
    {
        KeyToIdentifier.ToFieldName(null!).Should().Be("TranslationDefinition");
    }

    [Fact]
    public void Strips_special_characters()
    {
        KeyToIdentifier.ToFieldName("key@#$%value").Should().Be("KeyValue");
    }

    [Fact]
    public void Handles_mixed_separators()
    {
        KeyToIdentifier.ToFieldName("my.key-name_here").Should().Be("MyKeyNameHere");
    }
}
