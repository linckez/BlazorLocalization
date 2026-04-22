using BlazorLocalization.Analyzers.Analyzers;
using BlazorLocalization.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace BlazorLocalization.Analyzers.Tests;

using Verify = CSharpAnalyzerVerifier<TranslationKeyAnalyzer>;

public class TranslationKeyAnalyzerTests
{
    /// <summary>
    /// Stubs for IStringLocalizer + TranslationDefinitions.
    /// Analyzer matches by metadata name, so namespace/class names must be exact.
    /// </summary>
    private const string Stubs = """
        namespace Microsoft.Extensions.Localization
        {
            public interface IStringLocalizer
            {
                string this[string name] { get; }
                string GetString(string name);
                BlazorLocalization.Extensions.Translation.SimpleBuilder Translation(string key, string sourceMessage, object replaceWith = null);
                BlazorLocalization.Extensions.Translation.SimpleBuilder Translation(string key);
                BlazorLocalization.Extensions.Translation.PluralBuilder Translation(string key, int howMany, bool ordinal = false, object replaceWith = null);
            }
            public interface IStringLocalizer<T> : IStringLocalizer { }
        }
        namespace BlazorLocalization.Extensions.Translation
        {
            public class SimpleBuilder { }
            public class PluralBuilder { }
            public class SelectBuilder<T> { }
            public class SelectPluralBuilder<T> { }
        }
        namespace BlazorLocalization.Extensions.Translation.Definitions
        {
            public class SimpleDefinition { }
            public class PluralDefinition { }
            public class SelectDefinition<T> { }
            public class SelectPluralDefinition<T> { }
            public static class TranslationDefinitions
            {
                public static SimpleDefinition DefineSimple(string key, string sourceMessage) => null;
                public static PluralDefinition DefinePlural(string key) => null;
                public static SelectDefinition<T> DefineSelect<T>(string key) where T : System.Enum => null;
                public static SelectPluralDefinition<T> DefineSelectPlural<T>(string key) where T : System.Enum => null;
            }
        }
        namespace BlazorLocalization.Extensions
        {
            public interface IProviderBasedStringLocalizerFactory { }
        }
        """;

    private static CSharpAnalyzerTest<TranslationKeyAnalyzer, DefaultVerifier> CreateTest(
        string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TranslationKeyAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
        };
        test.TestState.Sources.Add(("Stubs.cs", Stubs));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static DiagnosticResult ExpectBL0003(int line, int column) =>
        Verify.Diagnostic("BL0003").WithLocation(line, column);

    private static DiagnosticResult ExpectBL0005(int line, int column) =>
        Verify.Diagnostic("BL0005").WithLocation(line, column);

    // === Sub-case A: Duplicate static definitions ===

    [Fact]
    public async Task DuplicateDefineSimple_Fires()
    {
        const string source = """
            using BlazorLocalization.Extensions.Translation.Definitions;
            class A
            {
                static readonly SimpleDefinition Save = TranslationDefinitions.DefineSimple("Common.Save", "Save");
            }
            class B
            {
                static readonly SimpleDefinition Save = TranslationDefinitions.DefineSimple("Common.Save", "Save button");
            }
            """;

        var test = CreateTest(source,
            ExpectBL0003(4, 45).WithLocation(8, 45).WithArguments("Common.Save", "is defined multiple times"),
            ExpectBL0003(8, 45).WithLocation(4, 45).WithArguments("Common.Save", "is defined multiple times"));
        await test.RunAsync();
    }

    // === Sub-case B: Inline Translation() with conflicting messages ===

    [Fact]
    public async Task InlineTranslation_ConflictingMessages_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                    loc.Translation("Home.Title", "Hello there");
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0003(6, 9).WithLocation(7, 9).WithArguments("Home.Title", "has conflicting messages"),
            ExpectBL0003(7, 9).WithLocation(6, 9).WithArguments("Home.Title", "has conflicting messages"));
        await test.RunAsync();
    }

    // === Sub-case B negative: Same key, same message — no diagnostic ===

    [Fact]
    public async Task InlineTranslation_SameKeyAndMessage_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                    loc.Translation("Home.Title", "Welcome");
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // === Sub-case C: Static definition + inline usage ===

    [Fact]
    public async Task StaticDefinition_PlusInlineUsage_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class Defs
            {
                static readonly SimpleDefinition Save = TranslationDefinitions.DefineSimple("Common.Save", "Save");
            }
            class Page
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Common.Save", "Save");
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0003(5, 45).WithLocation(11, 9).WithArguments("Common.Save", "has both a static definition and inline usage"),
            ExpectBL0003(11, 9).WithLocation(5, 45).WithArguments("Common.Save", "has both a static definition and inline usage"));
        await test.RunAsync();
    }

    // === Sub-case D: Type mismatch ===

    [Fact]
    public async Task DefineSimple_AndDefinePlural_SameKey_Fires()
    {
        const string source = """
            using BlazorLocalization.Extensions.Translation.Definitions;
            class A
            {
                static readonly SimpleDefinition X = TranslationDefinitions.DefineSimple("Common.Greeting", "Hello");
            }
            class B
            {
                static readonly PluralDefinition X = TranslationDefinitions.DefinePlural("Common.Greeting");
            }
            """;

        var test = CreateTest(source,
            ExpectBL0003(4, 42).WithLocation(8, 42).WithArguments("Common.Greeting", "type mismatch \u2014 defined as both Simple and Plural"),
            ExpectBL0003(8, 42).WithLocation(4, 42).WithArguments("Common.Greeting", "type mismatch \u2014 defined as both Simple and Plural"));
        await test.RunAsync();
    }

    // === No diagnostic: unique keys ===

    [Fact]
    public async Task UniqueKeys_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition Save = TranslationDefinitions.DefineSimple("Common.Save", "Save");
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                    loc.Translation("Home.Subtitle", "Hello");
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // === No diagnostic: no localization at all ===

    [Fact]
    public async Task NoLocalization_NoDiagnostic()
    {
        const string source = """
            class C
            {
                void M() { }
            }
            """;

        var test = new CSharpAnalyzerTest<TranslationKeyAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };
        await test.RunAsync();
    }

    // === Three usages of same key, all flagged ===

    [Fact]
    public async Task ThreeInlineUsages_ConflictingMessages_AllFlagged()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("X", "A");
                    loc.Translation("X", "B");
                    loc.Translation("X", "C");
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0003(6, 9).WithLocation(7, 9).WithArguments("X", "has conflicting messages"),
            ExpectBL0003(7, 9).WithLocation(6, 9).WithArguments("X", "has conflicting messages"),
            ExpectBL0003(8, 9).WithLocation(6, 9).WithLocation(7, 9).WithArguments("X", "has conflicting messages"));
        await test.RunAsync();
    }

    // ======================================================================
    // BL0005 — Undefined translation key
    // ======================================================================

    // === Key-only with no definition → BL0005 fires ===

    [Fact]
    public async Task KeyOnly_NoDefinition_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Orphan.Key");
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0005(6, 9).WithArguments("Orphan.Key"));
        await test.RunAsync();
    }

    // === Key-only with matching DefineSimple → no BL0005 ===

    [Fact]
    public async Task KeyOnly_WithDefineSimple_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class Defs
            {
                static readonly SimpleDefinition Save = TranslationDefinitions.DefineSimple("Common.Save", "Save");
            }
            class Page
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Common.Save");
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // === Key-only with matching inline Translation(key, message) → no BL0005 ===

    [Fact]
    public async Task KeyOnly_WithInlineDefinition_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                    loc.Translation("Home.Title");
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // === Multiple key-only same key, no definition → BL0005 on each ===

    [Fact]
    public async Task MultipleKeyOnly_NoDefinition_AllFlagged()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Missing.Key");
                    loc.Translation("Missing.Key");
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0005(6, 9).WithArguments("Missing.Key"),
            ExpectBL0005(7, 9).WithArguments("Missing.Key"));
        await test.RunAsync();
    }

    // === Key-only plural (no message param) with no definition → BL0005 fires ===

    [Fact]
    public async Task KeyOnlyPlural_NoDefinition_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Items.Count", 5);
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0005(6, 9).WithArguments("Items.Count"));
        await test.RunAsync();
    }

    // === Key-only plural with matching DefinePlural → no BL0005 ===

    [Fact]
    public async Task KeyOnlyPlural_WithDefinePlural_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class Defs
            {
                static readonly PluralDefinition Cart = TranslationDefinitions.DefinePlural("Cart.Items");
            }
            class Page
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Cart.Items", 3);
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // === Key-only does not interfere with BL0003 duplicate detection ===

    [Fact]
    public async Task KeyOnly_WithConflictingInline_BothDiagnostics()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("X", "A");
                    loc.Translation("X", "B");
                    loc.Translation("Y");
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0003(6, 9).WithLocation(7, 9).WithArguments("X", "has conflicting messages"),
            ExpectBL0003(7, 9).WithLocation(6, 9).WithArguments("X", "has conflicting messages"),
            ExpectBL0005(8, 9).WithArguments("Y"));
        await test.RunAsync();
    }

    // ======================================================================
    // BL0006 — Translation file conflict
    // ======================================================================

    private const string ResxTemplate = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="{0}" xml:space="preserve">
            <value>{1}</value>
          </data>
        </root>
        """;

    private static string MakeResx(string key, string value) =>
        string.Format(ResxTemplate, key, value);

    private static DiagnosticResult ExpectBL0006() =>
        Verify.Diagnostic("BL0006").WithLocation(DiagnosticResult.EmptyDiagnosticResults.First().Spans.Length == 0 ? 1 : 1, 1);

    [Fact]
    public async Task BL0006_ConflictingNeutralValues_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                }
            }
            """;

        var test = CreateTest(source);
        test.TestState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.TestState.AdditionalFiles.Add(("Translations/Common.resx", MakeResx("Home.Title", "Hello!")));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic("BL0006")
                .WithNoLocation()
                .WithArguments(
                    "Home.Title",
                    "source text",
                    "\"Hello!\" (Common.resx) vs \"Welcome\" (Home.resx)"));
        await test.RunAsync();
    }

    [Fact]
    public async Task BL0006_SameValue_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                }
            }
            """;

        var test = CreateTest(source);
        test.TestState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.TestState.AdditionalFiles.Add(("Translations/Common.resx", MakeResx("Home.Title", "Welcome")));
        // No BL0006 — same value is not a conflict
        await test.RunAsync();
    }

    [Fact]
    public async Task BL0006_CultureConflict_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                }
            }
            """;

        var test = CreateTest(source);
        test.TestState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.TestState.AdditionalFiles.Add(("Translations/Home.da.resx", MakeResx("Home.Title", "Velkommen")));
        test.TestState.AdditionalFiles.Add(("Translations/Common.da.resx", MakeResx("Home.Title", "Hej!")));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic("BL0006")
                .WithNoLocation()
                .WithArguments(
                    "Home.Title",
                    "translation for culture 'da'",
                    "\"Hej!\" (Common.da.resx) vs \"Velkommen\" (Home.da.resx)"));
        await test.RunAsync();
    }

    [Fact]
    public async Task BL0006_NoResxFiles_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                }
            }
            """;

        // No AdditionalFiles at all — should not crash, no BL0006
        var test = CreateTest(source);
        await test.RunAsync();
    }

    // ======================================================================
    // BL0003 with resx context (Phase 5)
    // ======================================================================

    [Fact]
    public async Task BL0003_WithResxContext_IncludesResxValueInMessage()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome");
                    loc.Translation("Home.Title", "Hello there");
                }
            }
            """;

        var test = CreateTest(source);
        test.TestState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.ExpectedDiagnostics.AddRange([
            ExpectBL0003(6, 9).WithLocation(7, 9).WithArguments("Home.Title", """has conflicting messages (resource file says: "Welcome")"""),
            ExpectBL0003(7, 9).WithLocation(6, 9).WithArguments("Home.Title", """has conflicting messages (resource file says: "Welcome")"""),
        ]);
        await test.RunAsync();
    }
}
