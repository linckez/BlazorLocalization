using System.Collections.Immutable;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BlazorLocalization.Analyzers.Tests;

// ──────────────────────────────────────────────────────────
// In-memory AdditionalText for testing (same pattern as experiment)
// ──────────────────────────────────────────────────────────
internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText _text;
    public override string Path { get; }
    public InMemoryAdditionalText(string path, string content)
    {
        Path = path;
        _text = SourceText.From(content);
    }
    public override SourceText? GetText(CancellationToken cancellationToken = default) => _text;
}

// ══════════════════════════════════════════════════════════
// ResxTranslationFileParser tests
// ══════════════════════════════════════════════════════════
public sealed class ResxTranslationFileParserTests
{
    private readonly ResxTranslationFileParser _parser = new();

    [Fact]
    public void CanHandle_ResxFile_ReturnsTrue()
    {
        Assert.True(_parser.CanHandle("Resources/Home.resx"));
        Assert.True(_parser.CanHandle("Home.da.resx"));
        Assert.True(_parser.CanHandle("PATH/TO/FILE.RESX"));
    }

    [Fact]
    public void CanHandle_NonResxFile_ReturnsFalse()
    {
        Assert.False(_parser.CanHandle("translations.json"));
        Assert.False(_parser.CanHandle("messages.po"));
        Assert.False(_parser.CanHandle("file.cs"));
    }

    [Fact]
    public void Parse_NeutralResx_ReturnsNullCulture()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Home.Title" xml:space="preserve">
                <value>Welcome</value>
              </data>
            </root>
            """;
        var result = _parser.Parse("Resources/Home.resx", SourceText.From(resx), CancellationToken.None);

        Assert.Null(result.Culture);
        Assert.Single(result.Entries);
        Assert.Equal("Welcome", result.Entries["Home.Title"]);
    }

    [Fact]
    public void Parse_CultureResx_ReturnsCultureCode()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Home.Title" xml:space="preserve">
                <value>Velkommen</value>
              </data>
            </root>
            """;
        var result = _parser.Parse("Resources/Home.da.resx", SourceText.From(resx), CancellationToken.None);

        Assert.Equal("da", result.Culture);
        Assert.Equal("Velkommen", result.Entries["Home.Title"]);
    }

    [Fact]
    public void Parse_RegionalCulture_ReturnsFull()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Key" xml:space="preserve">
                <value>Hola</value>
              </data>
            </root>
            """;
        var result = _parser.Parse("Home.es-MX.resx", SourceText.From(resx), CancellationToken.None);

        Assert.Equal("es-MX", result.Culture);
    }

    [Fact]
    public void Parse_SkipsEntriesWithTypeAttribute()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Icon" type="System.Resources.ResXFileRef, System.Windows.Forms">
                <value>icon.png;System.Byte[], mscorlib</value>
              </data>
              <data name="Greeting" xml:space="preserve">
                <value>Hello</value>
              </data>
            </root>
            """;
        var result = _parser.Parse("Resources/App.resx", SourceText.From(resx), CancellationToken.None);

        Assert.Single(result.Entries);
        Assert.Equal("Hello", result.Entries["Greeting"]);
    }

    [Fact]
    public void Parse_SkipsEmptyValues()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Empty" xml:space="preserve">
                <value></value>
              </data>
              <data name="Good" xml:space="preserve">
                <value>Present</value>
              </data>
            </root>
            """;
        var result = _parser.Parse("App.resx", SourceText.From(resx), CancellationToken.None);

        Assert.Single(result.Entries);
        Assert.Equal("Present", result.Entries["Good"]);
    }

    [Fact]
    public void Parse_MultipleEntries()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="A" xml:space="preserve"><value>Alpha</value></data>
              <data name="B" xml:space="preserve"><value>Bravo</value></data>
              <data name="C" xml:space="preserve"><value>Charlie</value></data>
            </root>
            """;
        var result = _parser.Parse("App.resx", SourceText.From(resx), CancellationToken.None);

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("Alpha", result.Entries["A"]);
        Assert.Equal("Bravo", result.Entries["B"]);
        Assert.Equal("Charlie", result.Entries["C"]);
    }

    [Theory]
    [InlineData("Home.resx", null)]
    [InlineData("Home.da.resx", "da")]
    [InlineData("Home.es-MX.resx", "es-MX")]
    [InlineData("Home.zh-Hans.resx", "zh-Hans")]
    [InlineData("Home.notaculture.resx", null)] // "notaculture" is not a real culture
    [InlineData("MyApp.Core.resx", null)]        // "Core" is not a culture
    public void ExtractCulture_VariousFilenames(string filename, string? expected)
    {
        Assert.Equal(expected, ResxTranslationFileParser.ExtractCulture(filename));
    }
}

// ══════════════════════════════════════════════════════════
// TranslationFileLookup tests
// ══════════════════════════════════════════════════════════
public sealed class TranslationFileLookupTests
{
    private static readonly IReadOnlyList<ITranslationFileParser> Parsers = new ITranslationFileParser[] { new ResxTranslationFileParser() };

    private static TranslationFileLookup BuildLookup(params (string path, string content)[] files)
    {
        var additionalTexts = files
            .Select(f => (AdditionalText)new InMemoryAdditionalText(f.path, f.content))
            .ToImmutableArray();
        return TranslationFileLookup.Build(additionalTexts, Parsers, CancellationToken.None);
    }

    private static string Resx(params (string key, string value)[] entries)
    {
        var dataElements = string.Join("\n",
            entries.Select(e => $"""  <data name="{e.key}" xml:space="preserve"><value>{e.value}</value></data>"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <root>
            {dataElements}
            </root>
            """;
    }

    [Fact]
    public void Empty_WhenNoFiles()
    {
        var lookup = BuildLookup();
        Assert.Same(TranslationFileLookup.Empty, lookup);
    }

    [Fact]
    public void TryGet_NeutralFile_ReturnsSourceText()
    {
        var lookup = BuildLookup(
            ("Home.resx", Resx(("Home.Title", "Welcome"))));

        Assert.True(lookup.TryGet("Home.Title", out var entry));
        Assert.Equal("Welcome", entry.SourceText);
        Assert.Empty(entry.Translations);
    }

    [Fact]
    public void TryGet_NeutralPlusCulture_ReturnsBoth()
    {
        var lookup = BuildLookup(
            ("Home.resx", Resx(("Home.Title", "Welcome"))),
            ("Home.da.resx", Resx(("Home.Title", "Velkommen"))));

        Assert.True(lookup.TryGet("Home.Title", out var entry));
        Assert.Equal("Welcome", entry.SourceText);
        Assert.Equal("Velkommen", entry.Translations["da"]);
    }

    [Fact]
    public void TryGet_MultipleCultures()
    {
        var lookup = BuildLookup(
            ("App.resx", Resx(("Save", "Save"))),
            ("App.da.resx", Resx(("Save", "Gem"))),
            ("App.es.resx", Resx(("Save", "Guardar"))));

        Assert.True(lookup.TryGet("Save", out var entry));
        Assert.Equal("Save", entry.SourceText);
        Assert.Equal(2, entry.Translations.Count);
        Assert.Equal("Gem", entry.Translations["da"]);
        Assert.Equal("Guardar", entry.Translations["es"]);
    }

    [Fact]
    public void TryGet_CultureOnlyFile_NoSourceText()
    {
        var lookup = BuildLookup(
            ("Home.da.resx", Resx(("Key", "Dansk"))));

        Assert.True(lookup.TryGet("Key", out var entry));
        Assert.Null(entry.SourceText);
        Assert.Equal("Dansk", entry.Translations["da"]);
    }

    [Fact]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        var lookup = BuildLookup(
            ("Home.resx", Resx(("Existing", "Value"))));

        Assert.False(lookup.TryGet("Missing", out _));
    }

    [Fact]
    public void TryGet_MultipleFilesAggregate()
    {
        var lookup = BuildLookup(
            ("Home.resx", Resx(("Home.Title", "Welcome"))),
            ("Common.resx", Resx(("Common.Save", "Save"))));

        Assert.True(lookup.TryGet("Home.Title", out var e1));
        Assert.Equal("Welcome", e1.SourceText);

        Assert.True(lookup.TryGet("Common.Save", out var e2));
        Assert.Equal("Save", e2.SourceText);
    }

    // ── Conflict scenarios ──

    [Fact]
    public void Conflict_SameKeySameCultureDifferentValue_MarksConflict()
    {
        var lookup = BuildLookup(
            ("Home.resx", Resx(("Title", "Welcome"))),
            ("Common.resx", Resx(("Title", "Hello"))));

        Assert.False(lookup.TryGet("Title", out _));
        Assert.True(lookup.HasConflict("Title"));
        Assert.Single(lookup.Conflicts);
        Assert.Equal("Title", lookup.Conflicts[0].Key);
        Assert.Null(lookup.Conflicts[0].Culture); // neutral conflict
        Assert.Equal(2, lookup.Conflicts[0].ConflictingSources.Count);
    }

    [Fact]
    public void Conflict_CultureLevelDifferentValue()
    {
        var lookup = BuildLookup(
            ("Home.da.resx", Resx(("Title", "Velkommen"))),
            ("Common.da.resx", Resx(("Title", "Hej"))));

        Assert.False(lookup.TryGet("Title", out _));
        Assert.True(lookup.HasConflict("Title"));
        Assert.Equal("da", lookup.Conflicts[0].Culture);
    }

    [Fact]
    public void NoConflict_SameKeySameValueDifferentFiles()
    {
        var lookup = BuildLookup(
            ("Home.resx", Resx(("Title", "Welcome"))),
            ("Common.resx", Resx(("Title", "Welcome"))));

        Assert.True(lookup.TryGet("Title", out var entry));
        Assert.Equal("Welcome", entry.SourceText);
        Assert.False(lookup.HasConflict("Title"));
        Assert.Empty(lookup.Conflicts);
        Assert.Equal(2, entry.Sources.Count); // both files recorded
    }

    [Fact]
    public void Conflict_DoesNotAffectOtherKeys()
    {
        var lookup = BuildLookup(
            ("Home.resx", Resx(("Title", "A"), ("Good", "OK"))),
            ("Common.resx", Resx(("Title", "B"), ("Good", "OK"))));

        Assert.False(lookup.TryGet("Title", out _));
        Assert.True(lookup.TryGet("Good", out var entry));
        Assert.Equal("OK", entry.SourceText);
    }

    // ── Sources / provenance ──

    [Fact]
    public void Sources_TrackFilePaths()
    {
        var lookup = BuildLookup(
            ("Resources/Home.resx", Resx(("Key", "Value"))),
            ("Resources/Home.da.resx", Resx(("Key", "Værdi"))));

        Assert.True(lookup.TryGet("Key", out var entry));
        Assert.Equal(2, entry.Sources.Count);
        Assert.Contains(entry.Sources, s => s.FilePath == "Resources/Home.resx" && s.Culture is null);
        Assert.Contains(entry.Sources, s => s.FilePath == "Resources/Home.da.resx" && s.Culture == "da");
    }

    // ── Empty/edge cases ──

    [Fact]
    public void Empty_HasNoConflictsOrEntries()
    {
        Assert.False(TranslationFileLookup.Empty.TryGet("anything", out _));
        Assert.False(TranslationFileLookup.Empty.HasConflict("anything"));
        Assert.Empty(TranslationFileLookup.Empty.Conflicts);
    }

    [Fact]
    public void NonResxFiles_AreIgnored()
    {
        var lookup = TranslationFileLookup.Build(
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("data.json", "{}")),
            Parsers,
            CancellationToken.None);

        Assert.Same(TranslationFileLookup.Empty, lookup);
    }
}
