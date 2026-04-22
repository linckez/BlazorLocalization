using BlazorLocalization.Analyzers.Analyzers;
using BlazorLocalization.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace BlazorLocalization.Analyzers.Tests;

using Verify = CSharpAnalyzerVerifier<EmptyKeyAnalyzer>;

public class EmptyKeyAnalyzerTests
{
    /// <summary>
    /// Stubs for IStringLocalizer. Analyzer matches by metadata name,
    /// so namespace/class names must be exact.
    /// </summary>
    private const string Stubs = """
        namespace Microsoft.Extensions.Localization
        {
            public interface IStringLocalizer
            {
                string this[string name] { get; }
                string this[string name, params object[] arguments] { get; }
                string GetString(string name);
                string Translation(string key, string sourceMessage, object replaceWith = null);
                string Translation(string key, int howMany, bool ordinal = false, object replaceWith = null);
            }
            public interface IStringLocalizer<T> : IStringLocalizer { }
        }
        """;

    private static CSharpAnalyzerTest<EmptyKeyAnalyzer, DefaultVerifier> CreateTest(
        string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<EmptyKeyAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
        };
        test.TestState.Sources.Add(("Stubs.cs", Stubs));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static DiagnosticResult ExpectBL0001(int line, int column) =>
        Verify.Diagnostic("BL0001").WithLocation(line, column);

    // === Fires on ===

    [Fact]
    public async Task Translation_EmptyKey_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(key: "", sourceMessage: "Welcome");
                }
            }
            """;

        var test = CreateTest(source, ExpectBL0001(6, 25));
        await test.RunAsync();
    }

    [Fact]
    public async Task Translation_EmptyKey_FirstPositional_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.Translation("", "Welcome");
                }
            }
            """;

        var test = CreateTest(source, ExpectBL0001(6, 25));
        await test.RunAsync();
    }

    [Fact]
    public async Task GetString_EmptyKey_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.GetString("");
                }
            }
            """;

        var test = CreateTest(source, ExpectBL0001(6, 23));
        await test.RunAsync();
    }

    [Fact]
    public async Task GetString_EmptyKey_WithArgs_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.GetString("");
                }
            }
            """;

        var test = CreateTest(source, ExpectBL0001(6, 23));
        await test.RunAsync();
    }

    [Fact]
    public async Task Indexer_EmptyKey_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    var x = Loc[""];
                }
            }
            """;

        var test = CreateTest(source, ExpectBL0001(6, 21));
        await test.RunAsync();
    }

    [Fact]
    public async Task Indexer_EmptyKey_WithArgs_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    var x = Loc["", 1];
                }
            }
            """;

        var test = CreateTest(source, ExpectBL0001(6, 21));
        await test.RunAsync();
    }

    [Fact]
    public async Task Multiple_EmptyKeys_AllFire()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(key: "", sourceMessage: "A");
                    Loc.GetString("");
                    var x = Loc[""];
                }
            }
            """;

        var test = CreateTest(source,
            ExpectBL0001(6, 25),
            ExpectBL0001(7, 23),
            ExpectBL0001(8, 21));
        await test.RunAsync();
    }

    // === Does NOT fire on ===

    [Fact]
    public async Task Translation_NonEmptyKey_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(key: "Home.Title", sourceMessage: "Welcome");
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task GetString_NonEmptyKey_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.GetString("Home.Title");
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task Indexer_NonEmptyKey_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    var x = Loc["Home.Title"];
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task Translation_VariableKey_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc, string key)
                {
                    Loc.Translation(key: key, sourceMessage: "Welcome");
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task UnrelatedMethod_EmptyString_NoDiagnostic()
    {
        const string source = """
            class C
            {
                void M()
                {
                    var x = "".ToString();
                    System.Console.WriteLine("");
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task EmptyCode_NoDiagnostic()
    {
        const string source = "";
        await Verify.VerifyAnalyzerAsync(source);
    }
}
