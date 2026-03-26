using BlazorLocalization.TranslationProvider.Crowdin.Parsing;
using FluentAssertions;

namespace BlazorLocalization.TranslationProvider.Crowdin.Tests.Parsing;

public sealed class AndroidXmlParserTests
{
    private readonly AndroidXmlParser _parser = new();

    [Fact]
    public void Parse_BasicEntries_ExtractsKeyValuePairs()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
              <string name="greeting">Hello</string>
              <string name="farewell">Goodbye</string>
            </resources>
            """;

        var result = _parser.Parse(xml);

        result.Should().HaveCount(2)
            .And.ContainKey("greeting").WhoseValue.Should().Be("Hello");
    }

    [Fact]
    public void Parse_DottedKeyInQuotes_StripsWrappingQuotes()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
              <string name="&quot;RC.Title&quot;">Connection Interrupted</string>
              <string name="plain">No quotes</string>
            </resources>
            """;

        var result = _parser.Parse(xml);

        result.Should().ContainKey("RC.Title").WhoseValue.Should().Be("Connection Interrupted");
        result.Should().ContainKey("plain").WhoseValue.Should().Be("No quotes");
    }

    [Fact]
    public void Parse_EmptyValue_PreservesEmptyString()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
              <string name="empty"></string>
            </resources>
            """;

        var result = _parser.Parse(xml);

        result.Should().ContainKey("empty").WhoseValue.Should().BeEmpty();
    }

    [Fact]
    public void Parse_HtmlEntities_DecodedByXDocument()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
              <string name="html">Text with &lt;strong&gt;bold&lt;/strong&gt;</string>
            </resources>
            """;

        var result = _parser.Parse(xml);

        result["html"].Should().Be("Text with <strong>bold</strong>");
    }

    [Fact]
    public void Parse_NonStringElements_Ignored()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
              <string name="kept">value</string>
              <plurals name="ignored"><item>x</item></plurals>
            </resources>
            """;

        var result = _parser.Parse(xml);

        result.Should().HaveCount(1).And.ContainKey("kept");
    }

    [Fact]
    public void Parse_MissingNameAttribute_SkipsElement()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
              <string>no name attr</string>
              <string name="valid">ok</string>
            </resources>
            """;

        var result = _parser.Parse(xml);

        result.Should().HaveCount(1).And.ContainKey("valid");
    }

    [Theory]
    [InlineData("en", "SampleBlazorApp.en.xml")]
    [InlineData("pl", "SampleBlazorApp.pl.xml")]
    [InlineData("ar", "SampleBlazorApp.ar.xml")]
    public void Parse_RealFixture_Returns57Entries(string locale, string filename)
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", filename));

        var result = _parser.Parse(xml, locale);

        result.Should().HaveCount(57);
    }

    [Fact]
    public void Parse_EnglishFixture_DottedKeysStripped()
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", "SampleBlazorApp.en.xml"));

        var result = _parser.Parse(xml);

        // Dotted keys should have quotes stripped
        result.Should().ContainKey("RC.Title").WhoseValue.Should().Be("Connection Interrupted");
        result.Should().ContainKey("CB.Razor").WhoseValue.Should().Be("From Razor side");
        result.Should().ContainKey("Resx.Match").WhoseValue.Should().Be("Matching source text");
        result.Should().ContainKey("S18.Cast").WhoseValue.Should().Be("Text with <strong>bold</strong>");

        // Non-dotted keys preserved as-is
        result.Should().ContainKey("S01").WhoseValue.Should().Be("Hello World");
        result.Should().ContainKey("_dynamicKey").WhoseValue.Should().Be("Dynamic message");
    }

    [Fact]
    public void Parse_EnglishFixture_PluralSuffixesPreserved()
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", "SampleBlazorApp.en.xml"));

        var result = _parser.Parse(xml);

        result.Should().ContainKey("S03_zero").WhoseValue.Should().Be("no items");
        result.Should().ContainKey("S03_one").WhoseValue.Should().Be("{} item");
        result.Should().ContainKey("S03_other").WhoseValue.Should().Be("{} items");
    }

    [Fact]
    public void Parse_EnglishFixture_EmptyStringPreserved()
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", "SampleBlazorApp.en.xml"));

        var result = _parser.Parse(xml);

        result.Should().ContainKey("S12").WhoseValue.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PolishFixture_TranslationdValues()
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", "SampleBlazorApp.pl.xml"));

        var result = _parser.Parse(xml);

        result.Should().ContainKey("S01").WhoseValue.Should().Be("Witaj Świecie");
        result.Should().ContainKey("RC.Title").WhoseValue.Should().Be("Połączenie przerwane");
    }

    [Fact]
    public void Parse_ArabicFixture_NoRtlMarks()
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", "SampleBlazorApp.ar.xml"));

        var result = _parser.Parse(xml);

        // Verify no U+200E (LRM) or U+200F (RLM) marks snuck in
        foreach (var (key, value) in result)
        {
            key.Should().NotContain("\u200E", because: "key '{0}' should not contain LRM", key);
            key.Should().NotContain("\u200F", because: "key '{0}' should not contain RLM", key);
            value.Should().NotContain("\u200E", because: "value for '{0}' should not contain LRM", key);
            value.Should().NotContain("\u200F", because: "value for '{0}' should not contain RLM", key);
        }
    }

    [Fact]
    public void Parse_EnglishFixture_MultiLinePreserved()
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", "SampleBlazorApp.en.xml"));

        var result = _parser.Parse(xml);

        result["S21.Verbatim"].Should().Contain("\n", because: "multi-line strings should preserve newlines");
    }
}
