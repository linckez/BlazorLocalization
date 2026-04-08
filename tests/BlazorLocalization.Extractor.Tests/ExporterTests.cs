using BlazorLocalization.Extractor.Adapters.Export;
using BlazorLocalization.Extractor.Domain;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// One shared input set covering all 5 TranslationSourceText branches (Singular, Plural, Select, SelectPlural, null).
/// Each exporter is tested once against this set via Verify snapshot.
/// </summary>
public class ExporterTests
{
	private static readonly IReadOnlyList<MergedTranslation> TestEntries =
	[
		new("App.Title",
			new SingularText("Welcome"),
			[new DefinitionSite(new SourceFilePath("/test/MyApp/Home.razor", "/test/MyApp"), 10, DefinitionKind.InlineTranslation)],
			[]),

		new("Cart.Items",
			new PluralText(
				Other: "{ItemCount} items",
				One: "{ItemCount} item",
				Zero: "No items",
				ExactMatches: new Dictionary<int, string> { [42] = "The answer" }),
			[new DefinitionSite(new SourceFilePath("/test/MyApp/Cart.razor", "/test/MyApp"), 20, DefinitionKind.InlineTranslation)],
			[]),

		new("Invite",
			new SelectText(
				Cases: new Dictionary<string, string>
				{
					["Female"] = "She invited you",
					["Male"] = "He invited you"
				},
				Otherwise: "They invited you"),
			[new DefinitionSite(new SourceFilePath("/test/MyApp/Invite.razor", "/test/MyApp"), 30, DefinitionKind.InlineTranslation)],
			[]),

		new("Inbox",
			new SelectPluralText(
				Cases: new Dictionary<string, PluralText>
				{
					["Female"] = new("She has {MessageCount} messages", One: "She has {MessageCount} message"),
					["Male"] = new("He has {MessageCount} messages", One: "He has {MessageCount} message")
				},
				Otherwise: new("They have {MessageCount} messages", One: "They have {MessageCount} message")),
			[new DefinitionSite(new SourceFilePath("/test/MyApp/Inbox.razor", "/test/MyApp"), 40, DefinitionKind.InlineTranslation)],
			[]),

		new("Legacy.Key",
			null,
			[],
			[new ReferenceSite(new SourceFilePath("/test/MyApp/Old.cs", "/test/MyApp"), 50, "GetString call")])
	];

	[Fact]
	public Task I18NextJson() =>
		Verify(new I18NextJsonExporter().Export(TestEntries, PathStyle.Relative));

	[Fact]
	public Task Po() =>
		Verify(new PoExporter().Export(TestEntries, PathStyle.Relative));

	[Fact]
	public Task GenericJson() =>
		Verify(new GenericJsonExporter().Export(TestEntries, PathStyle.Relative));
}
