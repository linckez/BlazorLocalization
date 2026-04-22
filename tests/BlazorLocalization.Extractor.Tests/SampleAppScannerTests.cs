using BlazorLocalization.Extractor.Domain;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Spot-check tests against the cached SampleBlazorApp scan result.
/// Each test exercises a unique Scanner/ChainInterpreter code path.
/// </summary>
public class SampleAppScannerTests(SampleAppFixture fixture) : IClassFixture<SampleAppFixture>
{
	private MergedTranslation Entry(string key) =>
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
			// Resx (CultureOnly excluded — new adapter skips keys without neutral counterpart)
			"Resx.Match", "Resx.Conflict", "Resx.Only",
			// Reference types (call-site → .resx)
			"R01.IndexerResolved", "R04.WithCultures",
			// R03.RuntimeTarget exists in RESX but not matched by code (unreferenced .resx entry)
			"R03.RuntimeTarget",
			// Runtime variable keys (unresolvable — variable name extracted, not the value)
			"_dynamicNoResx", "_dynamicWithResx",
			// SharedResource RESX entries
			"AppTitle", "WelcomeMessage",
			// Attribute, ternary, cast, multi-call, text block
			"S16.Attr", "S17.TernA", "S17.TernB", "S18.Cast", "S19.A", "S19.B", "S20.TextBlock",
			// Code-behind string literals
			"S21.Verbatim", "S21.Raw", "S21.Concat",
			// Nested + placeholder
			"S22.Nested", "S23.Placeholder",
			// Enum [Translation] attributes
			"Enum.FlightStatus_Delayed", "Flight.Late",
			// Reusable definitions (DefineXxx)
			"Def.Save", "Def.Cart", "Def.Greeting", "Def.Inbox",
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

	// ResxCultureOnlyEntry test removed — new adapter intentionally skips
	// keys that exist only in culture .resx files (no neutral counterpart).

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
	public void EnumTranslation_HasDefinition_NoFakeReference()
	{
		var entry = Entry("Enum.FlightStatus_Delayed");
		entry.Definitions.Should().NotBeEmpty();
		entry.Definitions[0].Kind.Should().Be(DefinitionKind.EnumAttribute);
		entry.References.Should().BeEmpty("enum attributes are definitions, not usages");
		entry.Status.Should().Be(TranslationStatus.Review, "no Loc.Display() usage can be resolved yet");
	}

	// ── DefinitionKind tests ──

	[Fact]
	public void InlineTranslation_HasInlineKind()
	{
		Entry("S01").Definitions.First().Kind.Should().Be(DefinitionKind.InlineTranslation);
	}

	[Fact]
	public void ReusableDefinition_HasReusableKind()
	{
		Entry("Def.Save").Definitions.First().Kind.Should().Be(DefinitionKind.ReusableDefinition);
	}

	[Fact]
	public void ReusableDefinition_HasNoReference()
	{
		var entry = Entry("Def.Save");
		entry.References.Should().BeEmpty("DefineXxx is a data definition, not a usage");
		entry.Status.Should().Be(TranslationStatus.Review);
	}

	[Fact]
	public void EnumAttribute_HasEnumKind()
	{
		Entry("Enum.FlightStatus_Delayed").Definitions.First().Kind.Should().Be(DefinitionKind.EnumAttribute);
	}

	[Fact]
	public void ResourceFile_HasResourceKind()
	{
		Entry("Resx.Only").Definitions.First().Kind.Should().Be(DefinitionKind.ResourceFile);
	}

	// ── Reference type tests (call-site → .resx) ──

	[Fact]
	public void CompileTimeIndexer_ResolvesResxSourceText()
	{
		Entry("R01.IndexerResolved").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Resolved from database via indexer");
	}

	[Fact]
	public void CompileTimeIndexer_ResolvesResxCultures()
	{
		var entry = Entry("R01.IndexerResolved");
		entry.InlineTranslations.Should().NotBeNull();
		entry.InlineTranslations!.Should().ContainKey("da");
		entry.InlineTranslations!.Should().ContainKey("es-MX");

		entry.InlineTranslations["da"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Opløst fra database via indekser");
	}

	[Fact]
	public void CompileTimeIndexer_WithCultures_ResolvesAll()
	{
		var entry = Entry("R04.WithCultures");
		entry.SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Base text with culture variants");
		entry.InlineTranslations.Should().NotBeNull();
		entry.InlineTranslations!.Should().ContainKey("da");
		entry.InlineTranslations!.Should().ContainKey("es-MX");

		entry.InlineTranslations["da"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Grundtekst med kulturvarianter");
		entry.InlineTranslations["es-MX"].Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("Texto base con variantes culturales");
	}

	[Fact]
	public void UnreferencedResxEntry_ImportedWithoutCodeMatch()
	{
		Entry("R03.RuntimeTarget").SourceText.Should().BeOfType<SingularText>()
			.Which.Value.Should().Be("You can only reach me at runtime");
	}

	[Fact]
	public void CompileTimeIndexerWithoutResx_NullSourceText()
	{
		Entry("S12").SourceText.Should().BeNull();
	}

	// ── Origin & cross-reference tests ──

	[Fact]
	public void ResxEntry_HasResxDefinition()
	{
		Entry("Resx.Only").Definitions.Should().Contain(d => d.File.IsResx);
	}

	[Fact]
	public void CodeOnlyEntry_HasNoResxDefinition()
	{
		Entry("S01").Definitions.Should().NotContain(d => d.File.IsResx);
	}

	[Fact]
	public void MatchedEntry_HasBothCodeAndResxDefinitions()
	{
		var entry = Entry("R01.IndexerResolved");
		entry.Definitions.Should().Contain(d => d.File.IsResx);
		entry.References.Should().NotBeEmpty();
	}

	[Fact]
	public void CompileTimeIndexer_IsKeyLiteralTrue()
	{
		Entry("R01.IndexerResolved").IsKeyLiteral.Should().BeTrue();
	}

	[Fact]
	public void RuntimeIndexer_IsKeyLiteralFalse()
	{
		Entry("_dynamicNoResx").IsKeyLiteral.Should().BeFalse();
	}

	[Fact]
	public void CrossReference_MatchedEntry_IsResolved()
	{
		Entry("R01.IndexerResolved").Status.Should().Be(TranslationStatus.Resolved);
	}

	[Fact]
	public void CrossReference_CodeOnlyWithSourceText_IsResolved()
	{
		Entry("S01").Status.Should().Be(TranslationStatus.Resolved);
	}

	[Fact]
	public void CrossReference_Unreferenced_IsReview()
	{
		Entry("Resx.Only").Status.Should().Be(TranslationStatus.Review);
	}

	[Fact]
	public void CrossReference_Unresolvable_IsReview()
	{
		Entry("_dynamicNoResx").Status.Should().Be(TranslationStatus.Review);
	}

	// ── Key-only Translation overloads ──

	[Fact]
	public void KeyOnly_Simple_ProducesReferenceOnly()
	{
		var entry = Entry("S30.KeyOnly");
		entry.Definitions.Should().BeEmpty("key-only Translation(key) has no source text");
		entry.References.Should().ContainSingle();
	}

	[Fact]
	public void KeyOnly_Plural_ProducesReferenceOnly()
	{
		var entry = Entry("S31.KeyOnlyPlural");
		entry.Definitions.Should().BeEmpty("key-only Translation(key, howMany) has no source text");
		entry.References.Should().ContainSingle();
	}

	[Fact]
	public void KeyOnly_Select_ProducesReferenceOnly()
	{
		var entry = Entry("S32.KeyOnlySelect");
		entry.Definitions.Should().BeEmpty("key-only Translation(key, select) has no source text");
		entry.References.Should().ContainSingle();
	}

	[Fact]
	public void KeyOnly_SelectPlural_ProducesReferenceOnly()
	{
		var entry = Entry("S33.KeyOnlySelectPlural");
		entry.Definitions.Should().BeEmpty("key-only Translation(key, select, howMany) has no source text");
		entry.References.Should().ContainSingle();
	}
}
