using BlazorLocalization.Analyzers.Analyzers;
using BlazorLocalization.Analyzers.CodeFixes;
using BlazorLocalization.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace BlazorLocalization.Analyzers.Tests;

using AnalyzerVerify = CSharpAnalyzerVerifier<MicrosoftLocalizationAnalyzer>;
using CodeFixVerify = CSharpCodeFixVerifier<MicrosoftLocalizationAnalyzer, UseTranslationApiCodeFix>;

public class MicrosoftLocalizationAnalyzerTests
{
    /// <summary>
    /// Stub IStringLocalizer interface so we don't need an assembly reference to Microsoft.Extensions.Localization.
    /// The analyzer matches by metadata name + namespace, so the stub must use the exact full name.
    /// </summary>
    private const string IStringLocalizerStub = """
        namespace Microsoft.Extensions.Localization
        {
            public interface IStringLocalizer
            {
                string this[string name] { get; }
                string this[string name, params object[] arguments] { get; }
                string GetString(string name);
                string GetString(string name, params object[] arguments);
                string Translation(string key, string message, object replaceWith = null);
                string Display(string value);
            }
            public interface IStringLocalizer<T> : IStringLocalizer { }
        }
        """;

    /// <summary>
    /// Stub factory interface so BL0002 fires (it guards on the presence of BlazorLocalization.Extensions).
    /// </summary>
    private const string FactoryStub = """
        namespace BlazorLocalization.Extensions
        {
            public interface IProviderBasedStringLocalizerFactory { }
        }
        """;

    private static DiagnosticResult ExpectBL0002(int line, int column) =>
        AnalyzerVerify.Diagnostic("BL0002").WithLocation(line, column);

    private static CSharpAnalyzerTest<MicrosoftLocalizationAnalyzer, DefaultVerifier> CreateAnalyzerTest(
        string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MicrosoftLocalizationAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { source },
                AdditionalFiles = { },
            },
        };
        test.TestState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static CSharpCodeFixTest<MicrosoftLocalizationAnalyzer, UseTranslationApiCodeFix, DefaultVerifier> CreateCodeFixTest(
        string source, string fixedSource, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<MicrosoftLocalizationAnalyzer, UseTranslationApiCodeFix, DefaultVerifier>
        {
            TestState =
            {
                Sources = { source },
            },
            FixedState =
            {
                Sources = { fixedSource },
            },
        };
        test.TestState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.FixedState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.FixedState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    // === Analyzer: Fires on ===

    [Fact]
    public async Task GetString_OnIStringLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.GetString("Home.Title");
                }
            }
            """;

        var test = CreateAnalyzerTest(source,
            ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    [Fact]
    public async Task GetString_WithArgs_OnIStringLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.GetString("Home.Title", "arg1");
                }
            }
            """;

        var test = CreateAnalyzerTest(source,
            ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Indexer_OnIStringLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    var x = loc["Home.Title"];
                }
            }
            """;

        var test = CreateAnalyzerTest(source,
            ExpectBL0002(6, 17).WithArguments("indexer"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Indexer_WithArgs_OnIStringLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    var x = loc["Home.Title", "arg1"];
                }
            }
            """;

        var test = CreateAnalyzerTest(source,
            ExpectBL0002(6, 17).WithArguments("indexer"));
        await test.RunAsync();
    }

    [Fact]
    public async Task GetString_OnGenericIStringLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer<C> loc)
                {
                    loc.GetString("Home.Title");
                }
            }
            """;

        var test = CreateAnalyzerTest(source,
            ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Indexer_OnGenericIStringLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer<C> loc)
                {
                    var x = loc["Home.Title"];
                }
            }
            """;

        var test = CreateAnalyzerTest(source,
            ExpectBL0002(6, 17).WithArguments("indexer"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Multiple_VanillaCalls_AllFire()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.GetString("A");
                    var x = loc["B"];
                }
            }
            """;

        var test = CreateAnalyzerTest(source,
            ExpectBL0002(6, 9).WithArguments("GetString"),
            ExpectBL0002(7, 17).WithArguments("indexer"));
        await test.RunAsync();
    }

    // === Analyzer: Does NOT fire on ===

    [Fact]
    public async Task Translation_OnIStringLocalizer_NoDiagnostic()
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

        var test = CreateAnalyzerTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task Display_OnIStringLocalizer_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Display("value");
                }
            }
            """;

        var test = CreateAnalyzerTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task GetString_OnNonLocalizer_NoDiagnostic()
    {
        const string source = """
            class SomethingElse
            {
                public string GetString(string key) => key;
            }
            class C
            {
                void M()
                {
                    var x = new SomethingElse();
                    x.GetString("Home.Title");
                }
            }
            """;

        var test = CreateAnalyzerTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task Indexer_OnDictionary_NoDiagnostic()
    {
        const string source = """
            using System.Collections.Generic;
            class C
            {
                void M()
                {
                    var dict = new Dictionary<string, string>();
                    var x = dict["key"];
                }
            }
            """;

        var test = CreateAnalyzerTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task NoLocalization_NoDiagnostic()
    {
        // Source with no IStringLocalizer usage at all
        const string source = """
            class C
            {
                void M() { }
            }
            """;

        // Don't add the stub — simulate a project without localization
        var test = new CSharpAnalyzerTest<MicrosoftLocalizationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };
        await test.RunAsync();
    }

    // === Code Fix Tests ===

    [Fact]
    public async Task CodeFix_GetString_ToTranslation()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.GetString("Home.Title");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation(key: "Home.Title", message: "");
                }
            }
            """;

        var test = CreateCodeFixTest(source, fixedSource,
            ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_Indexer_ToTranslation()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    var x = loc["Home.Title"];
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    var x = loc.Translation(key: "Home.Title", message: "");
                }
            }
            """;

        var test = CreateCodeFixTest(source, fixedSource,
            ExpectBL0002(6, 17).WithArguments("indexer"));
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_GenericReceiver_ToTranslation()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer<C> loc)
                {
                    loc.GetString("Home.Title");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer<C> loc)
                {
                    loc.Translation(key: "Home.Title", message: "");
                }
            }
            """;

        var test = CreateCodeFixTest(source, fixedSource,
            ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    [Fact]
    public async Task VariableKey_Diagnostic_NoCodeFix()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc, string key)
                {
                    loc.GetString(key);
                }
            }
            """;

        // Diagnostic fires but no code fix (no literal key to extract)
        var test = new CSharpCodeFixTest<MicrosoftLocalizationAnalyzer, UseTranslationApiCodeFix, DefaultVerifier>
        {
            TestState =
            {
                Sources = { source },
            },
            FixedState =
            {
                Sources = { source },
            },
        };
        test.TestState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.FixedState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.FixedState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.ExpectedDiagnostics.Add(
            ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    // === Enriched code fix variants (Phases 4) ===

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

    /// <summary>
    /// Stub with correct return types — SimpleBuilder with .For() so Variant 3 compiles.
    /// </summary>
    private const string RichIStringLocalizerStub = """
        namespace BlazorLocalization.Extensions.Translation
        {
            public class SimpleBuilder
            {
                public SimpleBuilder For(string locale, string message) => this;
            }
        }
        namespace Microsoft.Extensions.Localization
        {
            public interface IStringLocalizer
            {
                string this[string name] { get; }
                string this[string name, params object[] arguments] { get; }
                string GetString(string name);
                string GetString(string name, params object[] arguments);
                BlazorLocalization.Extensions.Translation.SimpleBuilder Translation(string key, string message, object replaceWith = null);
                string Display(string value);
            }
            public interface IStringLocalizer<T> : IStringLocalizer { }
        }
        """;

    [Fact]
    public async Task CodeFix_WithResx_OffersVariant2_WithSourceText()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.GetString("Home.Title");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation(key: "Home.Title", message: "Welcome");
                }
            }
            """;

        var test = new CSharpCodeFixTest<MicrosoftLocalizationAnalyzer, UseTranslationApiCodeFix, DefaultVerifier>
        {
            TestState = { Sources = { source } },
            FixedState = { Sources = { fixedSource } },
            CodeActionIndex = 1, // Variant 2: with source text
            CodeActionEquivalenceKey = "BL0002_WithSource",
        };
        test.TestState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.FixedState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.FixedState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.TestState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.FixedState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.ExpectedDiagnostics.Add(ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_WithResxAndCultures_OffersVariant3_WithForCalls()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.GetString("Home.Title");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation(key: "Home.Title", message: "Welcome").For("da", "Velkommen").For("es", "Bienvenido");
                }
            }
            """;

        var test = new CSharpCodeFixTest<MicrosoftLocalizationAnalyzer, UseTranslationApiCodeFix, DefaultVerifier>
        {
            TestState = { Sources = { source } },
            FixedState = { Sources = { fixedSource } },
            CodeActionIndex = 2, // Variant 3: with translations
            CodeActionEquivalenceKey = "BL0002_WithTranslations",
        };
        test.TestState.Sources.Add(("IStringLocalizerStub.cs", RichIStringLocalizerStub));
        test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.FixedState.Sources.Add(("IStringLocalizerStub.cs", RichIStringLocalizerStub));
        test.FixedState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.TestState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.TestState.AdditionalFiles.Add(("Translations/Home.da.resx", MakeResx("Home.Title", "Velkommen")));
        test.TestState.AdditionalFiles.Add(("Translations/Home.es.resx", MakeResx("Home.Title", "Bienvenido")));
        test.FixedState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.FixedState.AdditionalFiles.Add(("Translations/Home.da.resx", MakeResx("Home.Title", "Velkommen")));
        test.FixedState.AdditionalFiles.Add(("Translations/Home.es.resx", MakeResx("Home.Title", "Bienvenido")));
        test.ExpectedDiagnostics.Add(ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_ConflictedKey_OffersOnlyKeyOnly()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.GetString("Home.Title");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation(key: "Home.Title", message: "");
                }
            }
            """;

        // Two resx with conflicting values — only Variant 1 should be offered
        var test = new CSharpCodeFixTest<MicrosoftLocalizationAnalyzer, UseTranslationApiCodeFix, DefaultVerifier>
        {
            TestState = { Sources = { source } },
            FixedState = { Sources = { fixedSource } },
            CodeActionEquivalenceKey = "BL0002_KeyOnly",
        };
        test.TestState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.FixedState.Sources.Add(("IStringLocalizerStub.cs", IStringLocalizerStub));
        test.FixedState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.TestState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.TestState.AdditionalFiles.Add(("Translations/Common.resx", MakeResx("Home.Title", "Hello!")));
        test.FixedState.AdditionalFiles.Add(("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        test.FixedState.AdditionalFiles.Add(("Translations/Common.resx", MakeResx("Home.Title", "Hello!")));
        test.ExpectedDiagnostics.Add(ExpectBL0002(6, 9).WithArguments("GetString"));
        await test.RunAsync();
    }
}
