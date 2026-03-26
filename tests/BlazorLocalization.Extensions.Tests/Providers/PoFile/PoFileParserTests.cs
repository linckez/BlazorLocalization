using BlazorLocalization.Extensions.Providers.PoFile;
using FluentAssertions;

namespace BlazorLocalization.Extensions.Tests.Providers.PoFile;

public sealed class PoFileParserTests
{
    [Fact]
    public void Parse_PolishFourForms_AllPreserved()
    {
        const string polishPo = """
            msgid ""
            msgstr ""
            "Plural-Forms: nplurals=4; plural=(n==1 ? 0 : ...);\n"

            msgid "file"
            msgid_plural "files"
            msgstr[0] "plik"
            msgstr[1] "pliki"
            msgstr[2] "plików"
            msgstr[3] "plików (dużo)"

            msgid "greeting"
            msgstr "Cześć"
            """;

        var result = PoFileParser.Parse(polishPo, "pl");

        result.Should().ContainKey("file_one").WhoseValue.Should().Be("plik");
        result.Should().ContainKey("file_few").WhoseValue.Should().Be("pliki");
        result.Should().ContainKey("file_many").WhoseValue.Should().Be("plików");
        result.Should().ContainKey("file_other").WhoseValue.Should().Be("plików (dużo)");
        result.Should().NotContainKey("file");
        result.Should().ContainKey("greeting").WhoseValue.Should().Be("Cześć");
    }

    [Fact]
    public void Parse_EnglishTwoForms_StillWorks()
    {
        const string englishPo = """
            msgid "item"
            msgid_plural "items"
            msgstr[0] "item"
            msgstr[1] "items"
            """;

        var result = PoFileParser.Parse(englishPo, "en");

        result.Should().ContainKey("item_one").WhoseValue.Should().Be("item");
        result.Should().ContainKey("item_other").WhoseValue.Should().Be("items");
    }

    [Fact]
    public void Parse_ArabicSixForms_AllPreserved()
    {
        const string arabicPo = """
            msgid "book"
            msgid_plural "books"
            msgstr[0] "لا كتب"
            msgstr[1] "كتاب"
            msgstr[2] "كتابان"
            msgstr[3] "كتب"
            msgstr[4] "كتاباً"
            msgstr[5] "كتاب (كثير)"
            """;

        var result = PoFileParser.Parse(arabicPo, "ar");

        result.Should().ContainKey("book_zero").WhoseValue.Should().Be("لا كتب");
        result.Should().ContainKey("book_one").WhoseValue.Should().Be("كتاب");
        result.Should().ContainKey("book_two").WhoseValue.Should().Be("كتابان");
        result.Should().ContainKey("book_few").WhoseValue.Should().Be("كتب");
        result.Should().ContainKey("book_many").WhoseValue.Should().Be("كتاباً");
        result.Should().ContainKey("book_other").WhoseValue.Should().Be("كتاب (كثير)");
    }
}
