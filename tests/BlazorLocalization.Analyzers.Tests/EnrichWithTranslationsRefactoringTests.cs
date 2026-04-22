using BlazorLocalization.Analyzers.Refactorings;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace BlazorLocalization.Analyzers.Tests;

public sealed class EnrichWithTranslationsRefactoringTests
{
    private const string Stubs = """
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
                BlazorLocalization.Extensions.Translation.SimpleBuilder Translation(string key, string sourceMessage, object replaceWith = null);
                BlazorLocalization.Extensions.Translation.SimpleBuilder Translation(string key);
            }
            public interface IStringLocalizer<T> : IStringLocalizer { }
        }
        namespace BlazorLocalization.Extensions
        {
            public interface IProviderBasedStringLocalizerFactory { }
        }
        """;

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

    private static CSharpCodeRefactoringTest<EnrichWithTranslationsRefactoring, DefaultVerifier> CreateTest(
        string source, string fixedSource, params (string path, string content)[] resxFiles)
    {
        var test = new CSharpCodeRefactoringTest<EnrichWithTranslationsRefactoring, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
        };
        test.TestState.Sources.Add(("Stubs.cs", Stubs));
        test.FixedState.Sources.Add(("Stubs.cs", Stubs));

        foreach (var (path, content) in resxFiles)
        {
            test.TestState.AdditionalFiles.Add((path, content));
            test.FixedState.AdditionalFiles.Add((path, content));
        }

        return test;
    }

    private static CSharpCodeRefactoringTest<EnrichWithTranslationsRefactoring, DefaultVerifier> CreateNoRefactoringTest(
        string source, params (string path, string content)[] resxFiles)
    {
        var test = new CSharpCodeRefactoringTest<EnrichWithTranslationsRefactoring, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            OffersEmptyRefactoring = false,
        };
        test.TestState.Sources.Add(("Stubs.cs", Stubs));
        test.FixedState.Sources.Add(("Stubs.cs", Stubs));

        foreach (var (path, content) in resxFiles)
        {
            test.TestState.AdditionalFiles.Add((path, content));
            test.FixedState.AdditionalFiles.Add((path, content));
        }

        return test;
    }

    [Fact]
    public async Task Enrich_WithCultures_AppendsForCalls()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation([|"Home.Title"|], "Welcome");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome").For("da", "Velkommen").For("es", "Bienvenido");
                }
            }
            """;

        var test = CreateTest(source, fixedSource,
            ("Translations/Home.resx", MakeResx("Home.Title", "Welcome")),
            ("Translations/Home.da.resx", MakeResx("Home.Title", "Velkommen")),
            ("Translations/Home.es.resx", MakeResx("Home.Title", "Bienvenido")));
        await test.RunAsync();
    }

    [Fact]
    public async Task Enrich_NoCultures_NotOffered()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation([|"Home.Title"|], "Welcome");
                }
            }
            """;

        // Only neutral resx, no cultures — refactoring not offered
        var test = CreateNoRefactoringTest(source,
            ("Translations/Home.resx", MakeResx("Home.Title", "Welcome")));
        await test.RunAsync();
    }

    [Fact]
    public async Task Enrich_ConflictedKey_NotOffered()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation([|"Home.Title"|], "Welcome");
                }
            }
            """;

        // Conflicting resx values — refactoring not offered
        var test = CreateNoRefactoringTest(source,
            ("Translations/Home.resx", MakeResx("Home.Title", "Welcome")),
            ("Translations/Common.resx", MakeResx("Home.Title", "Hello!")),
            ("Translations/Home.da.resx", MakeResx("Home.Title", "Velkommen")));
        await test.RunAsync();
    }

    [Fact]
    public async Task Enrich_KeyNotInResx_NotOffered()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation([|"Missing.Key"|], "Unknown");
                }
            }
            """;

        var test = CreateNoRefactoringTest(source,
            ("Translations/Home.resx", MakeResx("Home.Title", "Welcome")),
            ("Translations/Home.da.resx", MakeResx("Home.Title", "Velkommen")));
        await test.RunAsync();
    }

    [Fact]
    public async Task Enrich_ExistingForCalls_SkipsPresent()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation([|"Home.Title"|], "Welcome").For("da", "Velkommen");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer loc)
                {
                    loc.Translation("Home.Title", "Welcome").For("da", "Velkommen").For("es", "Bienvenido");
                }
            }
            """;

        var test = CreateTest(source, fixedSource,
            ("Translations/Home.resx", MakeResx("Home.Title", "Welcome")),
            ("Translations/Home.da.resx", MakeResx("Home.Title", "Velkommen")),
            ("Translations/Home.es.resx", MakeResx("Home.Title", "Bienvenido")));
        await test.RunAsync();
    }
}
