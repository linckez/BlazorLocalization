using BlazorLocalization.Extractor.Application;
using BlazorLocalization.Extractor.Adapters.Roslyn;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

public class RazorMigratorTests
{
	// ── GenerateReplacement ─────────────────────────────────────

	[Fact]
	public void GenerateReplacement_SimpleKey_NoTranslations()
	{
		var result = RazorMigrator.GenerateReplacement("Loc", "Home.Title", "Welcome", null);
		result.Should().Be("""Loc.Translation(key: "Home.Title", sourceMessage: "Welcome")""");
	}

	[Fact]
	public void GenerateReplacement_WithSingleLocale()
	{
		var translations = new Dictionary<string, string> { ["da"] = "Velkommen" };
		var result = RazorMigrator.GenerateReplacement("Loc", "Home.Title", "Welcome", translations);
		result.Should().Be("""Loc.Translation(key: "Home.Title", sourceMessage: "Welcome").For(locale: "da", message: "Velkommen")""");
	}

	[Fact]
	public void GenerateReplacement_MultipleLocales_SortedAlphabetically()
	{
		var translations = new Dictionary<string, string> { ["es"] = "Hola", ["da"] = "Hej" };
		var result = RazorMigrator.GenerateReplacement("Loc", "Greeting", "Hello", translations);
		result.Should().Be("""Loc.Translation(key: "Greeting", sourceMessage: "Hello").For(locale: "da", message: "Hej").For(locale: "es", message: "Hola")""");
	}

	[Fact]
	public void GenerateReplacement_EmptyMessage()
	{
		var result = RazorMigrator.GenerateReplacement("Loc", "Missing.Key", "", null);
		result.Should().Be("""Loc.Translation(key: "Missing.Key", sourceMessage: "")""");
	}

	[Fact]
	public void GenerateReplacement_EscapedQuotesInMessage()
	{
		var result = RazorMigrator.GenerateReplacement("Loc", "Quote.Key", "She said \"hello\"", null);
		result.Should().Contain("sourceMessage: \"She said \\\"hello\\\"\"");
	}

	[Fact]
	public void GenerateReplacement_DifferentLocalizerName()
	{
		var result = RazorMigrator.GenerateReplacement("SharedLoc", "Shared.Footer", "Footer text", null);
		result.Should().Be("""SharedLoc.Translation(key: "Shared.Footer", sourceMessage: "Footer text")""");
	}

	// ── FindSpanOnLine ──────────────────────────────────────────

	[Fact]
	public void FindSpanOnLine_Indexer_FindsCorrectSpan()
	{
		var fileText = "<h1>@Loc[\"Home.Title\"]</h1>\n<p>other</p>";
		var span = RazorMigrator.FindSpanOnLine(fileText, 1, "Home.Title", CallKind.Indexer);
		span.Should().NotBeNull();
		fileText[span!.Value.Start..span.Value.End].Should().Be("Loc[\"Home.Title\"]");
	}

	[Fact]
	public void FindSpanOnLine_GetString_FindsCorrectSpan()
	{
		var fileText = "<p>@Loc.GetString(\"Home.Subtitle\")</p>";
		var span = RazorMigrator.FindSpanOnLine(fileText, 1, "Home.Subtitle", CallKind.MethodCall);
		span.Should().NotBeNull();
		fileText[span!.Value.Start..span.Value.End].Should().Be("Loc.GetString(\"Home.Subtitle\")");
	}

	[Fact]
	public void FindSpanOnLine_SecondLine()
	{
		var fileText = "<h1>Title</h1>\n<p>@Loc[\"Key\"]</p>";
		var span = RazorMigrator.FindSpanOnLine(fileText, 2, "Key", CallKind.Indexer);
		span.Should().NotBeNull();
		fileText[span!.Value.Start..span.Value.End].Should().Be("Loc[\"Key\"]");
	}

	[Fact]
	public void FindSpanOnLine_LineNotFound_ReturnsNull()
	{
		var fileText = "single line";
		var span = RazorMigrator.FindSpanOnLine(fileText, 5, "Key", CallKind.Indexer);
		span.Should().BeNull();
	}

	[Fact]
	public void FindSpanOnLine_PatternNotOnLine_ReturnsNull()
	{
		var fileText = "<h1>No localizer here</h1>";
		var span = RazorMigrator.FindSpanOnLine(fileText, 1, "Missing", CallKind.Indexer);
		span.Should().BeNull();
	}

	// ── EscapeString ────────────────────────────────────────────

	[Fact]
	public void EscapeString_Quotes()
	{
		RazorMigrator.EscapeString("She said \"hello\"").Should().Be("She said \\\"hello\\\"");
	}

	[Fact]
	public void EscapeString_Backslashes()
	{
		RazorMigrator.EscapeString("path\\to\\file").Should().Be("path\\\\to\\\\file");
	}

	[Fact]
	public void EscapeString_PlainText()
	{
		RazorMigrator.EscapeString("Hello world").Should().Be("Hello world");
	}

	[Fact]
	public void EscapeString_Newlines()
	{
		RazorMigrator.EscapeString("line1\nline2").Should().Be("line1\\nline2");
	}

	[Fact]
	public void EscapeString_CarriageReturn()
	{
		RazorMigrator.EscapeString("line1\r\nline2").Should().Be("line1\\r\\nline2");
	}

	[Fact]
	public void EscapeString_Tab()
	{
		RazorMigrator.EscapeString("col1\tcol2").Should().Be("col1\\tcol2");
	}

	[Fact]
	public void EscapeString_NullChar()
	{
		RazorMigrator.EscapeString("a\0b").Should().Be("a\\0b");
	}

	// ── IsValidExpression ───────────────────────────────────────

	[Fact]
	public void IsValidExpression_ValidTranslationCall()
	{
		RazorMigrator.IsValidExpression("""Loc.Translation(key: "K", sourceMessage: "M")""").Should().BeTrue();
	}

	[Fact]
	public void IsValidExpression_ValidWithForChain()
	{
		RazorMigrator.IsValidExpression("""Loc.Translation(key: "K", sourceMessage: "M").For(locale: "da", message: "V")""").Should().BeTrue();
	}

	[Fact]
	public void IsValidExpression_InvalidSyntax()
	{
		RazorMigrator.IsValidExpression("Loc.Translation(key: \"K\", sourceMessage: \"\"broken\")").Should().BeFalse();
	}
}
