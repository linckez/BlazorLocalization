using BlazorLocalization.Analyzers.Analyzers;
using BlazorLocalization.Analyzers.CodeFixes;
using BlazorLocalization.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace BlazorLocalization.Analyzers.Tests;

using AnalyzerVerify = CSharpAnalyzerVerifier<GenericLocalizerAnalyzer>;
using CodeFixVerify = CSharpCodeFixVerifier<GenericLocalizerAnalyzer, RemoveGenericTypeParamCodeFix>;

public class GenericLocalizerAnalyzerTests
{
    private const string LocalizerStub = """
        namespace Microsoft.Extensions.Localization
        {
            public interface IStringLocalizer { }
            public interface IStringLocalizer<T> : IStringLocalizer { }
        }
        """;

    private const string FactoryStub = """
        namespace BlazorLocalization.Extensions
        {
            public interface IProviderBasedStringLocalizerFactory { }
        }
        """;

    private const string InjectStub = """
        namespace Microsoft.AspNetCore.Components
        {
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class InjectAttribute : System.Attribute { }
        }
        """;

    private static DiagnosticResult ExpectBL0004(int line, int column) =>
        AnalyzerVerify.Diagnostic("BL0004").WithLocation(line, column);

    private static CSharpAnalyzerTest<GenericLocalizerAnalyzer, DefaultVerifier> CreateAnalyzerTest(
        string source, bool includeFactory, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<GenericLocalizerAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { source },
            },
        };
        test.TestState.Sources.Add(("LocalizerStub.cs", LocalizerStub));
        if (includeFactory)
            test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.TestState.Sources.Add(("InjectStub.cs", InjectStub));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static CSharpCodeFixTest<GenericLocalizerAnalyzer, RemoveGenericTypeParamCodeFix, DefaultVerifier> CreateCodeFixTest(
        string source, string fixedSource, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<GenericLocalizerAnalyzer, RemoveGenericTypeParamCodeFix, DefaultVerifier>
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
        test.TestState.Sources.Add(("LocalizerStub.cs", LocalizerStub));
        test.TestState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.TestState.Sources.Add(("InjectStub.cs", InjectStub));
        test.FixedState.Sources.Add(("LocalizerStub.cs", LocalizerStub));
        test.FixedState.Sources.Add(("FactoryStub.cs", FactoryStub));
        test.FixedState.Sources.Add(("InjectStub.cs", InjectStub));
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    // === Fires on ===

    [Fact]
    public async Task CtorParam_GenericLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class Foo
            {
                public Foo(IStringLocalizer<Foo> loc) { }
            }
            """;

        var test = CreateAnalyzerTest(source, includeFactory: true,
            ExpectBL0004(4, 16).WithArguments("Foo"));
        await test.RunAsync();
    }

    [Fact]
    public async Task PrimaryCtorParam_GenericLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            public class Foo(IStringLocalizer<Foo> loc) { }
            """;

        var test = CreateAnalyzerTest(source, includeFactory: true,
            ExpectBL0004(2, 18).WithArguments("Foo"));
        await test.RunAsync();
    }

    [Fact]
    public async Task InjectProperty_GenericLocalizer_Fires()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using Microsoft.AspNetCore.Components;
            class Foo
            {
                [Inject]
                public IStringLocalizer<Foo> Loc { get; set; }
            }
            """;

        var test = CreateAnalyzerTest(source, includeFactory: true,
            ExpectBL0004(6, 12).WithArguments("Foo"));
        await test.RunAsync();
    }

    // === Does NOT fire on ===

    [Fact]
    public async Task NonGenericLocalizer_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class Foo
            {
                public Foo(IStringLocalizer loc) { }
            }
            """;

        var test = CreateAnalyzerTest(source, includeFactory: true);
        await test.RunAsync();
    }

    [Fact]
    public async Task OpenTypeParam_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class Bar<T>
            {
                public Bar(IStringLocalizer<T> loc) { }
            }
            """;

        var test = CreateAnalyzerTest(source, includeFactory: true);
        await test.RunAsync();
    }

    [Fact]
    public async Task NoFactory_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class Foo
            {
                public Foo(IStringLocalizer<Foo> loc) { }
            }
            """;

        // No ProviderBasedStringLocalizerFactory — simulates non-BlazorLocalization project
        var test = CreateAnalyzerTest(source, includeFactory: false);
        await test.RunAsync();
    }

    [Fact]
    public async Task Field_GenericLocalizer_NoDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class Foo
            {
                private readonly IStringLocalizer<Foo> _loc;
            }
            """;

        var test = CreateAnalyzerTest(source, includeFactory: true);
        await test.RunAsync();
    }

    // === Code Fix ===

    [Fact]
    public async Task CodeFix_RemovesTypeParam()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class Foo
            {
                public Foo(IStringLocalizer<Foo> loc) { }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class Foo
            {
                public Foo(IStringLocalizer loc) { }
            }
            """;

        var test = CreateCodeFixTest(source, fixedSource,
            ExpectBL0004(4, 16).WithArguments("Foo"));
        await test.RunAsync();
    }

    [Fact]
    public async Task CodeFix_InjectProperty_RemovesTypeParam()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using Microsoft.AspNetCore.Components;
            class Foo
            {
                [Inject]
                public IStringLocalizer<Foo> Loc { get; set; }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            using Microsoft.AspNetCore.Components;
            class Foo
            {
                [Inject]
                public IStringLocalizer Loc { get; set; }
            }
            """;

        var test = CreateCodeFixTest(source, fixedSource,
            ExpectBL0004(6, 12).WithArguments("Foo"));
        await test.RunAsync();
    }

    [Fact]
    public async Task MultipleCtors_EachFlagged()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class Foo
            {
                public Foo(IStringLocalizer<Foo> loc) { }
                public Foo(IStringLocalizer<Foo> loc, int x) { }
            }
            """;

        var test = CreateAnalyzerTest(source, includeFactory: true,
            ExpectBL0004(4, 16).WithArguments("Foo"),
            ExpectBL0004(5, 16).WithArguments("Foo"));
        await test.RunAsync();
    }
}
