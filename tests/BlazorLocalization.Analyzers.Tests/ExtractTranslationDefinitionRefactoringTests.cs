using BlazorLocalization.Analyzers.Refactorings;
using BlazorLocalization.Analyzers.Tests.Verifiers;

namespace BlazorLocalization.Analyzers.Tests;

using Verify = CSharpCodeRefactoringVerifier<ExtractTranslationDefinitionRefactoring>;

public class ExtractTranslationDefinitionRefactoringTests
{
    private const string Stubs = """
        namespace BlazorLocalization.Extensions.Translation.Definitions
        {
            public class SimpleDefinition { }
            public static class TranslationDefinitions
            {
                public static SimpleDefinition DefineSimple(string key, string sourceMessage) => null;
            }
        }
        namespace Microsoft.Extensions.Localization
        {
            public interface IStringLocalizer
            {
                string this[string name] { get; }
                string GetString(string name);
                string Translation(string key, string sourceMessage, object replaceWith = null);
                string Translation(string key, int howMany, bool ordinal = false, object replaceWith = null);
                string Translation(BlazorLocalization.Extensions.Translation.Definitions.SimpleDefinition definition, object replaceWith = null);
            }
            public interface IStringLocalizer<T> : IStringLocalizer { }
        }
        """;

    [Fact]
    public async Task Basic_Extraction()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.[||]Translation("Obs.Save", "Save");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition ObsSave = TranslationDefinitions.DefineSimple("Obs.Save", "Save");

                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(ObsSave);
                }
            }
            """;

        await Verify.VerifyRefactoringAsync(source, fixedSource, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task Named_Arguments()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.[||]Translation(key: "Home.Title", sourceMessage: "Welcome");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition HomeTitle = TranslationDefinitions.DefineSimple("Home.Title", "Welcome");

                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(HomeTitle);
                }
            }
            """;

        await Verify.VerifyRefactoringAsync(source, fixedSource, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task Key_With_Underscores()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.[||]Translation("save_button", "Save");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition SaveButton = TranslationDefinitions.DefineSimple("save_button", "Save");

                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(SaveButton);
                }
            }
            """;

        await Verify.VerifyRefactoringAsync(source, fixedSource, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task With_ReplaceWith_Preserved()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.[||]Translation("Obs.Hello", "Hello {Name}", replaceWith: new { Name = "World" });
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition ObsHello = TranslationDefinitions.DefineSimple("Obs.Hello", "Hello {Name}");

                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(ObsHello, replaceWith: new { Name = "World" });
                }
            }
            """;

        await Verify.VerifyRefactoringAsync(source, fixedSource, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task Name_Collision_Appends_Suffix()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition ObsSave = TranslationDefinitions.DefineSimple("Other.Key", "Other");
                void M(IStringLocalizer Loc)
                {
                    Loc.[||]Translation("Obs.Save", "Save");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition ObsSave = TranslationDefinitions.DefineSimple("Other.Key", "Other");
                static readonly SimpleDefinition ObsSave1 = TranslationDefinitions.DefineSimple("Obs.Save", "Save");

                void M(IStringLocalizer Loc)
                {
                    Loc.Translation(ObsSave1);
                }
            }
            """;

        await Verify.VerifyRefactoringAsync(source, fixedSource, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task No_Refactoring_NonTranslation_Method()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc)
                {
                    Loc.[||]GetString("key");
                }
            }
            """;

        await Verify.VerifyNoRefactoringAsync(source, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task No_Refactoring_NonLiteral_Key()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            class C
            {
                void M(IStringLocalizer Loc, string k)
                {
                    Loc.[||]Translation(k, "msg");
                }
            }
            """;

        await Verify.VerifyNoRefactoringAsync(source, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task No_Refactoring_NonStringLocalizer_Receiver()
    {
        const string source = """
            class Other
            {
                public string Translation(string key, string sourceMessage) => "";
            }
            class C
            {
                void M(Other Loc)
                {
                    Loc.[||]Translation("key", "msg");
                }
            }
            """;

        await Verify.VerifyNoRefactoringAsync(source, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task No_Refactoring_Already_Definition()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                static readonly SimpleDefinition Def = TranslationDefinitions.DefineSimple("K", "M");
                void M(IStringLocalizer Loc)
                {
                    Loc.[||]Translation(Def);
                }
            }
            """;

        await Verify.VerifyNoRefactoringAsync(source, ("Stubs.cs", Stubs));
    }

    [Fact]
    public async Task Field_Inserted_Above_Containing_Method()
    {
        const string source = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                void First() { }

                void Second(IStringLocalizer Loc)
                {
                    Loc.[||]Translation("Test.Key", "Test");
                }
            }
            """;

        const string fixedSource = """
            using Microsoft.Extensions.Localization;
            using BlazorLocalization.Extensions.Translation.Definitions;
            class C
            {
                void First() { }

                static readonly SimpleDefinition TestKey = TranslationDefinitions.DefineSimple("Test.Key", "Test");

                void Second(IStringLocalizer Loc)
                {
                    Loc.Translation(TestKey);
                }
            }
            """;

        await Verify.VerifyRefactoringAsync(source, fixedSource, ("Stubs.cs", Stubs));
    }
}
