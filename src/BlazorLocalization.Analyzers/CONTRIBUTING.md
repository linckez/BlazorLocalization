# Contributing to BlazorLocalization.Analyzers

> For user-facing diagnostic docs, see the [`docs/`](docs/) folder. This file is for maintainers.

## What This Does

A Roslyn analyzer NuGet that ships as a `DevelopmentDependency` — zero runtime footprint. It runs inside the compiler and the IDE, flagging localization mistakes at edit time and build time. It also provides code fixes (lightbulb menu) and refactorings (hammer menu) that rewrite code automatically.

Analyzers have a fundamental constraint the Extractor doesn't: **no runtime reference to Extensions.** The analyzer DLL loads into the compiler host, not the user's application. It can't call `typeof(PluralBuilder)` against the Extensions assembly the way the Extractor can. Instead, it resolves symbols from the user's compilation via `GetTypeByMetadataName()`.

## The DLL-Packing Strategy

To use `typeof()` and `nameof()` against real types — not magic strings — we pack three DLLs into the NuGet's `analyzers/dotnet/cs` folder:

| DLL | Why |
|-----|-----|
| `BlazorLocalization.Analyzers.dll` | The analyzer itself |
| `BlazorLocalization.Extensions.Abstractions.dll` | Provides `typeof(TranslationDefinitions)`, `typeof(SimpleDefinition)`, `nameof(TranslationDefinitions.DefineSimple)`, etc. |
| `Microsoft.Extensions.Localization.Abstractions.dll` | Provides `typeof(IStringLocalizer)`, `typeof(IStringLocalizer<>)` |

The `.csproj` declares `IncludeBuildOutput=false` and manually packs each DLL. If you add a type reference to a new assembly, you must also pack that assembly's DLL — otherwise the analyzer loads fine in dev but crashes at consumer build time.

## Three Categories of Symbol Names

Not everything can use `typeof()` or `nameof()`. The rule:

| Category | Example | Mechanism | Safety net |
|----------|---------|-----------|------------|
| **Types** | `IStringLocalizer`, `TranslationDefinitions`, `SimpleDefinition` | `typeof(T).FullName!` → `GetTypeByMetadataName()` | Compiler — rename breaks the build |
| **Methods on types we own** | `DefineSimple`, `DefinePlural` | `nameof(TranslationDefinitions.DefineSimple)` | Compiler — rename breaks the build |
| **Extension methods / parameter names** | `"Translation"`, `"key"`, `"replaceWith"` | `const string` in symbol classes | Contract tests that reflect over the real assembly |

Extension method names can't use `nameof()` because `StringLocalizerExtensions` lives in the Extensions assembly (not Abstractions). Parameter names can't use `nameof()` at all (C# limitation). For these, we centralize the strings in `BlazorLocalizationSymbols` / `MicrosoftLocalizationSymbols` and verify them with reflection-based contract tests in the test project.

**If you add a new const string to a symbol class, add a matching contract test.** The existing tests in `SymbolNameContractTests.cs` show the pattern.

## Where Things Go

| You're adding... | Put it in... |
|-----------------|-------------|
| New diagnostic rule | `Analyzers/` (analyzer class) + `DiagnosticDescriptors.cs` (descriptor) + `docs/` (user-facing doc) + `AnalyzerReleases.Unshipped.md` |
| New code fix | `CodeFixes/` — derive equivalence key from `DiagnosticDescriptors.{Rule}.Id` |
| New refactoring | `Refactorings/` |
| New type/method name constant | `Scanning/BlazorLocalizationSymbols.cs` or `MicrosoftLocalizationSymbols.cs` — prefer `typeof()`/`nameof()`, fall back to const + contract test |
| Argument extraction logic | `Scanning/TranslationCallExtractor.cs` — shared by analyzers and refactorings |

## Anti-Patterns

- **Hardcoded metadata name strings** — Use `typeof(T).FullName!` for types we can reference. If you can't reference the type, centralize the string in a symbol class and add a contract test.
- **Duplicate argument-resolution logic** — `TranslationCallExtractor.FindArgumentForParameter()` handles named + positional argument matching. Don't rewrite it in your analyzer.
- **Hardcoded equivalence keys** — Derive from `DiagnosticDescriptors.{Rule}.Id` so the key tracks if a diagnostic ID changes.
- **`typeof()` in a `const`** — `typeof()` isn't const-evaluable. Use `static readonly` fields for type-derived strings, `const` only for true literals validated by contract tests.

## Razor Compatibility

Diagnostics work in `.razor` files (inline expressions, `@code` blocks, code-behind). Code fixes work in `.cs` and `.razor.cs` only — Razor's language server has a hardcoded allowlist that blocks custom code fixes in `.razor` files. This is a known platform limitation with no timeline for removal.

**Requirement:** Every analyzer must opt into generated code analysis:
```csharp
context.ConfigureGeneratedCodeAnalysis(
    GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
```
Without this, diagnostics silently disappear in `.razor` files.

## Build & Test

```bash
dotnet build src/BlazorLocalization.Analyzers
dotnet test tests/BlazorLocalization.Analyzers.Tests
dotnet pack src/BlazorLocalization.Analyzers -o /tmp/nupkg-verify
```
