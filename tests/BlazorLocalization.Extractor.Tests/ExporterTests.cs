using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Exporters;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// One shared input set covering all 5 TranslationSourceText branches (Singular, Plural, Select, SelectPlural, null).
/// Each exporter is tested once against this set via Verify snapshot.
/// </summary>
public class ExporterTests
{
	private static readonly IReadOnlyList<MergedTranslationEntry> TestEntries =
	[
		new("App.Title",
			new SingularText("Welcome"),
			[new SourceReference("Home.razor", 10, "MyApp", null)]),

		new("Cart.Items",
			new PluralText(
				Other: "{ItemCount} items",
				One: "{ItemCount} item",
				Zero: "No items",
				ExactMatches: new Dictionary<int, string> { [42] = "The answer" }),
			[new SourceReference("Cart.razor", 20, "MyApp", null)]),

		new("Invite",
			new SelectText(
				Cases: new Dictionary<string, string>
				{
					["Female"] = "She invited you",
					["Male"] = "He invited you"
				},
				Otherwise: "They invited you"),
			[new SourceReference("Invite.razor", 30, "MyApp", null)]),

		new("Inbox",
			new SelectPluralText(
				Cases: new Dictionary<string, PluralText>
				{
					["Female"] = new("She has {MessageCount} messages", One: "She has {MessageCount} message"),
					["Male"] = new("He has {MessageCount} messages", One: "He has {MessageCount} message")
				},
				Otherwise: new("They have {MessageCount} messages", One: "They have {MessageCount} message")),
			[new SourceReference("Inbox.razor", 40, "MyApp", null)]),

		new("Legacy.Key",
			null,
			[new SourceReference("Old.cs", 50, "MyApp", "GetString call")])
	];

	[Fact]
	public Task I18NextJson() =>
		Verify(new I18NextJsonExporter().Export(TestEntries));

	[Fact]
	public Task Po() =>
		Verify(new PoExporter().Export(TestEntries));

	[Fact]
	public Task GenericJson() =>
		Verify(new GenericJsonExporter().Export(TestEntries));
}
