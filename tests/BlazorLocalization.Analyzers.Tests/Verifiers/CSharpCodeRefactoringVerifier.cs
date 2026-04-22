using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace BlazorLocalization.Analyzers.Tests.Verifiers;

internal static class CSharpCodeRefactoringVerifier<TRefactoring>
    where TRefactoring : CodeRefactoringProvider, new()
{
    public static async Task VerifyRefactoringAsync(
        string source,
        string fixedSource,
        params (string filename, string content)[] additionalFiles)
    {
        var test = new CSharpCodeRefactoringTest<TRefactoring, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        foreach (var (filename, content) in additionalFiles)
        {
            test.TestState.Sources.Add((filename, content));
            test.FixedState.Sources.Add((filename, content));
        }

        await test.RunAsync(CancellationToken.None);
    }

    public static async Task VerifyNoRefactoringAsync(
        string source,
        params (string filename, string content)[] additionalFiles)
    {
        var test = new CSharpCodeRefactoringTest<TRefactoring, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
        };

        foreach (var (filename, content) in additionalFiles)
        {
            test.TestState.Sources.Add((filename, content));
            test.FixedState.Sources.Add((filename, content));
        }

        test.OffersEmptyRefactoring = false;
        await test.RunAsync(CancellationToken.None);
    }
}
