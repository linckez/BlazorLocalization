# Contributing to BlazorLocalization.Extractor

> For user-facing docs, see [`/docs/Extractor.md`](../../docs/Extractor.md). This file is for maintainers.

## What This Does

A Roslyn-based CLI tool (`blazor-loc`) that scans `.razor`, `.cs`, and `.resx` files and exports every translation string to file formats that translation management platforms understand (Crowdin i18next JSON, GNU Gettext PO, generic JSON).

It's a convenience tool — saves context-switching between code and translation platforms. Not a runtime dependency.

## Hexagonal Architecture

Four folders, strict dependency direction:

```
Domain/        ← Core ring: pure types, enums, business rules. ZERO external dependencies.
Scanning/      ← Input adapter: filesystem & Roslyn → domain types. Depends on Domain only.
Exporters/     ← Output adapter: domain types → file formats. Depends on Domain only.
Cli/           ← Presentation: Spectre.Console commands, wizard, renderers. Depends on everything.
```

### Dependency Rule

| Layer | May reference | Must NOT reference |
|-------|--------------|-------------------|
| `Domain/` | Nothing (BCL only) | Scanning, Exporters, Cli, Spectre, Roslyn |
| `Scanning/` | Domain | Exporters, Cli, Spectre |
| `Exporters/` | Domain | Scanning, Cli, Spectre |
| `Cli/` | Domain, Scanning, Exporters, Spectre | — |

If you're adding a `using` that violates this table, the type is in the wrong layer.

## Where Things Go

| You're adding... | Put it in... |
|-----------------|-------------|
| New export format | `Domain/ExportFormat.cs` (enum member) + `Exporters/` (exporter class) + `ExporterFactory` (mapping). The exhaustive `switch` in `ExporterFactory.Create()` produces a compiler warning if you forget. |
| New source type (e.g. `.cshtml`) | `Scanning/Providers/` — implement `ISourceProvider.GetDocuments()` + wire in `ProjectScanner` |
| New domain type | `Domain/` — must have zero external deps. Sealed record. |
| New CLI option | `Cli/Commands/` (settings property) + `Cli/InteractiveWizard.cs` (wizard prompt) |
| New CLI command | `Cli/Commands/` (command + settings classes) + `Domain/Requests/` (request value object) + `Program.cs` (registration) |
| New domain enum | `Domain/` — with `[Description]` from `System.ComponentModel` if user-facing (read by both `--help` and the wizard automatically) |
| New validation guard | `Domain/Requests/XxxRequest.Validate()` — pure, returns error list. Never in commands. |
| Shared scanning logic | `Scanning/ProjectScanner.cs` — single pipeline for providers → scanner → resx → merge. |
| Shared locale logic | `Domain/Entries/LocaleDiscovery.cs` — locale enumeration, filtering, per-locale entry rewriting. |

## Anti-Patterns

- **Enums in `Cli/`** — If it defines *what* the tool does (not *how* the user interacts), it belongs in `Domain/`.
- **Infrastructure in presentation** — Filesystem scanning, exporter instantiation, project discovery are not CLI concerns.
- **Commands with business logic** — Commands orchestrate; domain types enforce rules (e.g. `MergedTranslationEntry.FromRaw()` owns conflict detection).
- **Duplicated pure logic in commands** — Path relativization, locale discovery, project resolution, and validation belong in `Domain/` or `Scanning/`, not copy-pasted across commands.
- **Validation in commands** — Guards and input validation belong in `Domain/Requests/XxxRequest.Validate()`. Commands only build the request and check the result.
- **Manual validation of enum CLI options** — Spectre.Console.Cli validates enum-typed properties automatically. Don't add string checks.
- **Inline dictionaries in wizard for enum options** — Use `PromptEnum<T>()` which reads `[Description]` attributes via reflection. Prevents wizard/enum drift.

---

## Scanner Quality Contract

This is the most important section in this file. Every Scanner bug we've had traces back to violating one of these rules.

### Background

The Scanner interprets fluent builder chains from `BlazorLocalization.Extensions`. It recognises two entry points: `IStringLocalizer.Translation()` calls (inline usage) and `TranslationDefinitions.DefineSimple()` / `DefinePlural()` / `DefineSelect<T>()` / `DefineSelectPlural<T>()` factory calls (reusable definitions). The Extractor has a direct `ProjectReference` to Extensions, so `typeof()`, `nameof()`, and reflection all work against the real assembly at compile time and runtime. Roslyn's compilation also loads the same assembly via `MetadataReference.CreateFromFile()`.

Both lenses — reflection and Roslyn — describe the same binary on disk. `typeof(PluralBuilder).FullName!` is the bridge key that connects them.

### Rules

**1. No string literals for identity.**

Every type, method, and parameter the Scanner matches against must be derived from the Extensions assembly:

| What | How | Guarantor |
|------|-----|-----------|
| Builder types | `typeof(PluralBuilder)` → `compilation.GetTypeByMetadataName(typeof(PluralBuilder).FullName!)` | Compiler — rename breaks build |
| Definition types | `typeof(SimpleDefinition)` → `compilation.GetTypeByMetadataName(typeof(SimpleDefinition).FullName!)` | Compiler — rename breaks build |
| Methods | `nameof(PluralBuilder.For)` → `roslynType.GetMembers(nameof(...))` | Compiler — rename breaks build |
| Parameter names | `typeof(PluralBuilder).GetMethod(nameof(PluralBuilder.For))!.GetParameters()[0].Name` | Reflection — reads the real assembly, auto-tracks renames |

**2. Identity, not names.**

Dispatch on symbol identity (`SymbolEqualityComparer.Default.Equals`), not `.Name` string comparisons. Resolve Roslyn symbols at init time via `typeof()`/`nameof()`, then compare symbol-to-symbol at runtime.

**3. Same binary, two lenses.**

Reflection (`typeof()`) and Roslyn (`GetTypeByMetadataName()`) both describe the Extensions DLL. They always agree because they read the same file on disk.

**4. Fail fast, never silently drift.**

At Scanner initialization, cross-validate Roslyn symbols against reflection. Every `GetTypeByMetadataName()` must return non-null. Every `GetMembers(nameof(...))` must resolve. Parameter counts must match. If anything disagrees, crash immediately — not a silent wrong extraction.

**5. Strings flow out, never in.**

Roslyn-derived strings (argument text, expression text, literal values) flow *out* to `ExtractedCall`, `TranslationEntry`, and export formats. They are *never* used as match targets for interpretation logic. The Scanner decides what something *is* by symbol identity, then reads what it *contains* as output data.

**6. Two permitted hardcoded strings.**

Only two are acceptable:

- `"Translation"` — entry-point method on `IStringLocalizer` (Microsoft's assembly, not ours)
- `"ToString"` — chain terminator (from `System.Object`)

Everything from the Extensions assembly uses `typeof()`/`nameof()`/reflection.

### Forbidden Patterns

Every example below has caused a real bug in this project. If you find yourself writing code that looks like these, stop.

**F1: Hardcoded method name comparison**

```csharp
// FORBIDDEN — "For" is a magic string. Rename For() to ForLocale() and this silently stops matching.
if (call.MethodName == "For")

// CORRECT — nameof() breaks at compile time if For() is renamed.
if (SymbolEqualityComparer.Default.Equals(calledMethod, _pluralForSymbol))
// where _pluralForSymbol was resolved at init via nameof(PluralBuilder.For)
```

**F2: Hardcoded parameter name lookup**

```csharp
// FORBIDDEN — "message" is a magic string. If the parameter is ever renamed,
// the mapper silently returns null and the localizer breaks at runtime.
var text = FindArgumentValue(call, "message");

// CORRECT — parameter name derived from reflection. Auto-tracks renames.
var paramName = typeof(SimpleBuilder).GetMethod(nameof(SimpleBuilder.For))!
    .GetParameters()[1].Name;
```

**F3: Hardcoded type name comparison**

```csharp
// FORBIDDEN — if PluralBuilder is renamed or moved to a different namespace, this silently fails.
if (returnType.Name == "PluralBuilder")

// CORRECT — symbol identity via typeof(). Rename breaks the build.
if (SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, _pluralBuilderSymbol))
```

**F4: String set for method classification**

```csharp
// FORBIDDEN — a HashSet of magic strings. Add a new CLDR category method and forget to update → silent miss.
private static readonly HashSet<string> PluralCategoryMethods = ["Zero", "One", "Two", "Few", "Many", "Other"];
if (PluralCategoryMethods.Contains(call.MethodName))

// CORRECT — resolve each method symbol at init via nameof(), store in a HashSet<IMethodSymbol>.
private readonly HashSet<IMethodSymbol> _pluralCategorySymbols = new(SymbolEqualityComparer.Default)
{
    Resolve(nameof(PluralBuilder.Zero)),
    Resolve(nameof(PluralBuilder.One)),
    // ... each one is compile-time checked
};
if (_pluralCategorySymbols.Contains(calledMethod))
```

**F5: String-based parameter value as logic key**

```csharp
// FORBIDDEN — using the string content of a parameter to decide what type of entry to create.
// This is "strings flow IN" — the extracted text is driving interpretation.
if (args.Any(a => a.ParameterName == "count"))
    return BuildPlural(call);

// CORRECT — the return type of Translation() already tells you it's plural.
// Symbol identity decides the builder type, not what arguments happen to be present.
if (SymbolEqualityComparer.Default.Equals(returnType, _pluralBuilderSymbol))
    return BuildPlural(call);
```

**F6: Hardcoded sentinel values**

```csharp
// FORBIDDEN — "__otherwise__" is a magic string coupling the extractor to SelectBuilder's internal convention.
if (key == "__otherwise__")

// CORRECT — read the sentinel from the source via reflection.
private static readonly string OtherwiseSentinel = (string)typeof(SelectBuilder<>)
    .GetField("OtherwiseSentinel", BindingFlags.NonPublic | BindingFlags.Static)!
    .GetValue(null)!;
```

### The Grep Test

If you can `grep -r` the Scanner for any quoted string that matches the name of a type, method, or parameter in the Extensions project, the contract is violated.

## Build & Run

```bash
dotnet build src/BlazorLocalization.Extractor
dotnet run --project src/BlazorLocalization.Extractor -- extract tests/SampleBlazorApp -f po
dotnet run --project src/BlazorLocalization.Extractor -- inspect tests/SampleBlazorApp
dotnet run --project src/BlazorLocalization.Extractor -- extract --help
```
