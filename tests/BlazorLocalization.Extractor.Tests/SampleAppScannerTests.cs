using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Spot-check tests against the cached SampleBlazorApp scan result.
/// Each test exercises a unique Scanner/ChainInterpreter code path.
/// </summary>
public class SampleAppScannerTests(SampleAppFixture fixture) : IClassFixture<SampleAppFixture>
{
	private MergedTranslationEntry Entry(string key) =>
		fixture.EntryByKey.TryGetValue(key, out var e)
			? e
			: throw new KeyNotFoundException($"No merged entry for key '{key}'");

	[Fact]
	public void AllExpectedKeysPresent()
	{
		var expected = new[]
		{
			// Simple
			"S01", "S02",
			// Plural
			"S03", "S04", "S05", "S06",
			// Select
			"S07", "S08",
			// SelectPlural
			"S09", "S10", "S11",
			// Indexer
			"S12",
			// Resx
			"Resx.Match", "Resx.Conflict", "Resx.Only", "Resx.CultureOnly",
			// Attribute, ternary, cast, multi-call, text block
			"S16.Attr", "S17.TernA", "S17.TernB", "S18.Cast", "S19.A", "S19.B", "S20.TextBlock",
			// Code-behind string literals
			"S21.Verbatim", "S21.Raw", "S21.Concat",
			// Nested + placeholder
			"S22.Nested", "S23.Placeholder",
			// Enum [Translation] attributes
			"Enum.FlightStatus_Delayed", "Flight.Late",
			// EdgeCasePage
			"CB.Razor", "CB.CodeBehind",
			// ReconnectModal
			"RC.Title"
		};

		fixture.EntryByKey.Keys.Should().Contain(expected);
	}

	[Fact]
	public void SimpleTranslation_SingularText()
	{
		Entry("S01").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Hello World");
	}

	[Fact]
	public void PluralTranslation_AllSixCldrCategories()
	{
		var plural = Entry("S03").SourceText.Should().BeOfType<PluralText>().Subject;

		plural.Zero.Should().Be("no items");
		plural.One.Should().Be("{ItemCount} item");
		plural.Two.Should().Be("{ItemCount} items");
		plural.Few.Should().Be("{ItemCount} items");
		plural.Many.Should().Be("{ItemCount} items");
		plural.Other.Should().Be("{ItemCount} items");
	}

	[Fact]
	public void SelectTranslation_WhenCasesAndOtherwise()
	{
		var select = Entry("S07").SourceText.Should().BeOfType<SelectText>().Subject;

		select.Cases.Should().ContainKey("Alpha").WhoseValue.Should().Be("Alpha path");
		select.Cases.Should().ContainKey("Beta").WhoseValue.Should().Be("Beta path");
		select.Otherwise.Should().Be("Other path");
	}

	[Fact]
	public void SelectPluralTranslation_NestedPluralText()
	{
		var sp = Entry("S09").SourceText.Should().BeOfType<SelectPluralText>().Subject;

		sp.Cases.Should().ContainKey("Alpha");
		sp.Cases["Alpha"].One.Should().Be("{ItemCount} Alpha item");
		sp.Cases["Alpha"].Other.Should().Be("{ItemCount} Alpha items");

		sp.Otherwise.Should().NotBeNull();
		sp.Otherwise!.Other.Should().Be("{ItemCount} other items");
	}

	[Fact]
	public void Indexer_NullSourceText()
	{
		Entry("S12").SourceText.Should().BeNull();
	}

	[Fact]
	public void InlineFor_CapturesLocaleTranslations()
	{
		var entry = Entry("S02");
		entry.InlineTranslations.Should().NotBeNull();
		entry.InlineTranslations!.Should().ContainKey("da");
		entry.InlineTranslations!.Should().ContainKey("es-MX");

		entry.InlineTranslations["da"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Hej");
	}

	[Fact]
	public void PluralOrdinal_SetsIsOrdinalFlag()
	{
		Entry("S04").SourceText.Should().BeOfType<PluralText>()
			.Which.IsOrdinal.Should().BeTrue();
	}

	[Fact]
	public void PluralExactly_CapturesExactMatches()
	{
		var plural = Entry("S05").SourceText.Should().BeOfType<PluralText>().Subject;

		plural.ExactMatches.Should().NotBeNull();
		plural.ExactMatches!.Should().ContainKey(0)
			.WhoseValue.Should().Be("none at all");
	}

	[Fact]
	public void ResxOnlyEntry_Imported()
	{
		Entry("Resx.Only").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Only in resx, no code counterpart");
	}

	[Fact]
	public void ResxEntry_HasCultureInlineTranslations()
	{
		var entry = Entry("Resx.Match");
		entry.InlineTranslations.Should().NotBeNull();
		entry.InlineTranslations!.Should().ContainKey("da");
		entry.InlineTranslations!.Should().ContainKey("es-MX");

		entry.InlineTranslations["da"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Matchende kildetekst");
		entry.InlineTranslations["es-MX"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Texto fuente coincidente");
	}

	[Fact]
	public void ResxCultureOnlyEntry_NullSourceTextWithInlineTranslations()
	{
		var entry = Entry("Resx.CultureOnly");
		entry.SourceText.Should().BeNull();
		entry.InlineTranslations.Should().NotBeNull();
		entry.InlineTranslations!.Should().ContainKey("da");
		entry.InlineTranslations!["da"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Kun i dansk, ikke i neutral");
		// es-MX does not have this key
		entry.InlineTranslations!.Should().NotContainKey("es-MX");
	}

	[Fact]
	public void ResxConflict_Detected()
	{
		fixture.MergeResult.Conflicts
			.Should().Contain(c => c.Key == "Resx.Conflict");
	}

	[Fact]
	public void CodeBehind_DetectedAcrossPartial()
	{
		Entry("CB.CodeBehind").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("From code-behind");
	}

	[Fact]
	public void StringConcatenation_Folded()
	{
		Entry("S21.Concat").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("First half second half");
	}

	[Fact]
	public void EnumTranslation_SourceText()
	{
		Entry("Enum.FlightStatus_Delayed").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Delayed");
	}

	[Fact]
	public void EnumTranslation_InlineTranslations()
	{
		var entry = Entry("Enum.FlightStatus_Delayed");
		entry.InlineTranslations.Should().NotBeNull();
		entry.InlineTranslations!.Should().ContainKey("da");
		entry.InlineTranslations!.Should().ContainKey("es-MX");

		entry.InlineTranslations["da"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Forsinket");
		entry.InlineTranslations["es-MX"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Retrasado");
	}

	[Fact]
	public void EnumTranslation_CustomKey()
	{
		Entry("Flight.Late").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Arrived a bit late");
	}

	[Fact]
	public void EnumTranslation_ProducesExtractedCalls()
	{
		var enumCalls = fixture.ScanResult.Calls
			.Where(c => c.CallKind == CallKind.AttributeDeclaration)
			.ToList();

		enumCalls.Should().Contain(c => c.ContainingTypeName == "FlightStatus" && c.MethodName == "Delayed");
		enumCalls.Should().Contain(c => c.ContainingTypeName == "FlightStatus" && c.MethodName == "ArrivedABitLate");
	}
}
