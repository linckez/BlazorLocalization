# Contributing to BlazorLocalization.Extractor

> For user-facing docs, see [`/docs/Extractor.md`](../../docs/Extractor.md). This file is for maintainers.

## What This Does

A Roslyn-based CLI tool (`blazor-loc`) that scans `.razor`, `.cs`, and `.resx` files and exports every translation string to file formats that translation management platforms understand (Crowdin i18next JSON, GNU Gettext PO, generic JSON).

It's a convenience tool — saves context-switching between code and translation platforms. Not a runtime dependency.

## Who Uses This and Why

Everything this tool produces is for **end users** — developers working on their application. Not for maintainers of this repository. Every output format, every CLI flag, every table column must be evaluated from the user's perspective: *does this help them accomplish what they came here to do?*

There are two commands, each serving a distinct purpose:

### `extract` — Send strings to translators

The user runs `blazor-loc extract ./src -f i18next -o ./translations` because they want to upload their translation strings to Crowdin, Lokalise, or another translation platform. The output is a file that a translator (or translation platform) will consume.

**What matters to the translator:** The translation key, the source text (what to translate), and *context* — where in the application this string is used, so the translator understands its meaning. "Save" on a toolbar means something different than "Save" on a banking page.

**What doesn't matter to the translator:** Which `.resx` file the string was imported from, what line number it sits on in an XML file, or any other storage metadata. RESX files are an implementation detail of how the developer stored strings — the translator neither knows nor cares.

This means: in the generic JSON export format (`-f json`), the `sources` array is **translator context** — only code call sites (`.razor`, `.cs` files with their line numbers and method context). RESX file locations are excluded because they don't help the translator understand meaning.

### `inspect` — Translation health audit

The user runs `blazor-loc inspect ./src` because they need to answer questions about their own codebase:

- *"Did I wire all my translations correctly?"*
- *"Are there keys in my RESX files that nothing in my code references anymore?"*
- *"Did I typo a key name somewhere?"*
- *"Are there gaps in my Danish or Spanish coverage?"*
- *"Am I duplicating the same translation across multiple files?"*

In a codebase with 900+ translated strings across 10 languages, these questions are humanly impossible to answer by visiting every call site and cross-referencing every RESX file by hand. The inspect command provides a **complete overview** in seconds.

This serves two kinds of users equally:

1. **Vanilla Microsoft localization users** (using `IStringLocalizer["key"]` + `.resx` files, never adopted `.Translation()`). They get a full inventory of their call sites vs RESX entries, cross-referenced to find orphans, missing keys, and typos. They may never intend to adopt the Extensions framework — that's fine. The tool still helps them audit their translation wiring.

2. **BlazorLocalization.Extensions adopters** (using `.Translation()`, `.For()`, reusable definitions). They get a complete overview of all translation entries, duplicate detection, culture coverage gaps, and conflict warnings.

The inspect JSON output (`--json` or piped) is a **dumb 1:1 conversion** of what the console tables show. It exists so users can pipe to `jq`, feed into other tools, or do ad-hoc analysis. It has no special purpose beyond being the machine-readable mirror of the console output.

### Design litmus test

Before adding or changing any output, ask:

1. **Who will read this?** A translator? A developer auditing their codebase? A CI pipeline?
2. **Does this help them do their job?** If the answer is "it's technically accurate but nobody asked for it" — don't add it.
3. **Would a non-technical translator understand why this is here?** (For extract output.)
4. **Would a developer skimming 900 entries find this useful or noise?** (For inspect output.)

If you can't answer these questions, you're making a technical decision disguised as a feature. Stop and think about the user's journey first.

## Definitions vs References

The extractor distinguishes two fundamentally different things about a translation key:

- **Definition** — where the source text is authored. A `TranslationDefinition` carries the key, source text, form, and one or more `DefinitionSite`s.
- **Reference** — where the key is consumed at runtime. A `TranslationReference` carries the key and one or more `ReferenceSite`s.

The `DefinitionKind` enum on each `DefinitionSite` records *how* the source text was defined:

| Kind | Example | Creates Definition? | Creates Reference? |
|------|---------|--------------------|--------------------|
| `InlineTranslation` | `Loc.Translation("Key", "Welcome")` | Yes | Yes — the expression runs at render time |
| `ReusableDefinition` | `TranslationDefinitions.DefineSimple("Key", "Save")` | Yes | No — a field initializer is storage, not usage |
| `EnumAttribute` | `[Translation("Delayed")] Delayed` | Yes | No — a declaration, not consumption |
| `ResourceFile` | `.resx` entry with key-value | Yes | No — a data file, not a call site |

This matters for the inspect command's **Usage** and **Source** columns:

- **Usage** shows only genuine `ReferenceSite`s — places in `*.razor`/`*.cs` where the key is consumed by `Loc["Key"]`, `Loc.Translation(definition: x)`, or `Loc.Display(enum)`.
- **Source** shows the `DefinitionKind` label (e.g. `DefineSimple()`, `[Translation]`, `.Translation()`) plus an indented clickable file reference to where the source text is defined.

**Status** follows from this: a definition with a real reference (or an `InlineTranslation`, which is inherently both) is `Resolved`. A definition with no reference is `Review` — it may be unused. A reference with no definition is `Missing`.

### Why only .resx?

The extractor scans `.resx` files as the only file-based translation format. It does not scan JSON, PO, or other translation files — even though those are valid provider formats at runtime.

This is deliberate: the extractor is a **migration aid**. Its purpose is to help teams transition from the `ResourceManager`/RESX infrastructure onto BlazorLocalization's provider-based architecture. RESX is scoped because that's what people are migrating *from*. If we tried to support every possible provider/data source, the tool would have no natural boundary.

## Hexagonal Architecture

Five layers, strict dependency direction:

```
Domain/                  ← Core ring: pure types, enums, business rules. ZERO external dependencies.
Ports/                   ← Contracts: IScannerOutput, ScanDiagnostic. Depends on Domain only.
Application/             ← Orchestration: pipeline, merge, locale discovery. Depends on Domain + Ports.
Adapters/Roslyn/         ← Input adapter: Roslyn IOperation walking → domain types.
Adapters/Resx/           ← Input adapter: .resx XML parsing → domain types.
Adapters/Export/         ← Output adapter: domain types → file formats (i18next JSON, PO, generic JSON).
Adapters/Cli/            ← Driving adapter: Spectre.Console commands, wizard, renderers.
```

### Dependency Rule

| Layer | May reference | Must NOT reference |
|-------|--------------|-------------------|
| `Domain/` | Nothing (BCL only) | Ports, Application, Adapters, Spectre, Roslyn |
| `Ports/` | Domain | Application, Adapters, Spectre, Roslyn |
| `Application/` | Domain, Ports | Adapters (except `ProjectScanner` — see note), Spectre |
| `Adapters/Roslyn/` | Domain, Ports | other Adapters, Spectre |
| `Adapters/Resx/` | Domain, Ports | other Adapters, Spectre, Roslyn |
| `Adapters/Export/` | Domain | Ports, Application, other Adapters, Spectre, Roslyn |
| `Adapters/Cli/` | Domain, Ports, Application, Adapters, Spectre | — |

**Note:** `ProjectScanner` in Application directly references the concrete Roslyn and Resx adapters to compose them. This is a pragmatic trade-off — it acts as the composition root. If a second driving adapter (e.g. REST API) is added, this is the one class to extract into a shared composition root.

If you're adding a `using` that violates this table, the type is in the wrong layer.

## Where Things Go

| You're adding... | Put it in... |
|-----------------|-------------|
| New export format | `Domain/ExportFormat.cs` (enum member) + `Adapters/Export/` (exporter class) + `ExporterFactory` (mapping). The exhaustive `switch` in `ExporterFactory.Create()` produces a compiler warning if you forget. |
| New source type (e.g. `.cshtml`) | `Adapters/Roslyn/` — implement a new source provider like `CSharpFileProvider` / `RazorSourceProvider`, wire in `ProjectScanner` |
| New scanner (e.g. XLIFF) | `Adapters/<Name>/` — implement `IScannerOutput` from `Ports/`, wire in `ProjectScanner` |
| New domain type | `Domain/` — must have zero external deps. Sealed record. |
| New CLI option | `Adapters/Cli/Commands/` (settings property) + `Adapters/Cli/InteractiveWizard.cs` (wizard prompt) |
| New CLI command | `Adapters/Cli/Commands/` (command + settings classes + request value object) + `Program.cs` (registration) |
| New domain enum | `Domain/` — with `[Description]` from `System.ComponentModel` if user-facing (read by both `--help` and the wizard automatically) |
| New definition mechanism | `Domain/DefinitionKind.cs` (enum member) + producing adapter + `TranslationEntryRenderer.FormatSource()` (display) |
| Shared scanning logic | `Application/ProjectScanner.cs` — single pipeline for providers → scanner → resx → merge. |
| Shared locale logic | `Application/LocaleDiscovery.cs` — locale enumeration, filtering, per-locale entry rewriting. |

## Anti-Patterns

- **Enums in `Adapters/Cli/`** — If it defines *what* the tool does (not *how* the user interacts), it belongs in `Domain/`.
- **Infrastructure in presentation** — Filesystem scanning, exporter instantiation, project discovery are not CLI concerns. Scanning lives in `Application/`, export logic in `Adapters/Export/`.
- **Commands with business logic** — Commands orchestrate; domain types and the pipeline enforce rules.
- **Duplicated pure logic in commands** — Path relativization, locale discovery, project resolution belong in `Domain/` or `Application/`, not copy-pasted across commands.
- **Presentation markup in the Domain** — Spectre.Console rendering (e.g. `[link=...][cyan]...[/][/]`) belongs in `Adapters/Cli/Rendering/`, not on domain types. `SourceFilePath.Display(PathStyle)` (plain text) is fine in Domain; the markup wrapper `SourceFileMarkup.DisplayMarkup()` is a CLI extension method.
- **Parsing `DefinitionSite.Context` strings for rendering logic** — Use `DefinitionSite.Kind` (the `DefinitionKind` enum) as the machine-readable discriminator. `Context` is supplementary human-readable detail, never a switch target.
- **Fake references** — Don't create a `TranslationReference` at the same site as a `TranslationDefinition` just to make the entry appear "Resolved". Only `InlineTranslation` (`.Translation()` in code) genuinely defines AND uses in one expression. `ReusableDefinition`, `EnumAttribute`, and `ResourceFile` produce definitions only.
- **Manual validation of enum CLI options** — Spectre.Console.Cli validates enum-typed properties automatically. Don't add string checks.
- **Inline dictionaries in wizard for enum options** — Use `PromptEnum<T>()` which reads `[Description]` attributes via reflection. Prevents wizard/enum drift.

---

## Scanner Quality Contract

This is the most important section in this file. Every Scanner bug we've had traces back to violating one of these rules.

### Background

The Roslyn scanner interprets fluent builder chains from `BlazorLocalization.Extensions`. It recognises two entry points: `IStringLocalizer.Translation()` calls (inline usage) and `TranslationDefinitions.DefineSimple()` / `DefinePlural()` / `DefineSelect<T>()` / `DefineSelectPlural<T>()` factory calls (reusable definitions). The scanner walks Roslyn `IOperation` trees via `LocalizerOperationWalker` to find these calls, then `CallInterpreter` maps the captured arguments to Domain types.

The Extractor has a direct `ProjectReference` to Extensions. All references to Extensions types, methods, and parameters are consolidated in a single file: **`Adapters/Roslyn/ExtensionsContract.cs`**. This file uses `typeof().FullName!` for metadata names, `nameof()` for method and property names, and documented string constants (with `<see cref>` back-links) for the few things C# can't verify at compile time (C#14 extension block members, parameter names).

### ExtensionsContract.cs — the single coupling surface

Every reference from the Extractor to the Extensions project flows through this file:

| What | Technique | Guarantor |
|------|-----------|-----------|
| Metadata names (for `GetTypeByMetadataName`) | `typeof(T).FullName!` | Compiler — rename/move breaks build |
| Factory method names | `nameof(TranslationDefinitions.DefineSimple)` | Compiler — rename breaks build |
| Builder chain methods | `nameof(SimpleBuilder.For)`, `nameof(PluralBuilder.One)`, etc. | Compiler — rename breaks build |
| Builder/Definition type names | `nameof(SimpleBuilder)`, `nameof(PluralDefinition)`, etc. | Compiler — rename breaks build |
| Attribute property names | `nameof(TranslationAttribute.Locale)`, `nameof(TranslationAttribute.Key)` | Compiler — rename breaks build |
| Extension method names (`Translation`, `Display`) | String constants with comment | C#14 `extension` block limitation — `nameof` can't reach these |
| Parameter names (`key`, `message`, `howMany`, etc.) | String constants with `<see cref>` | Validated by `ExtensionsContractTests` via reflection |

If Extensions renames something compiler-verified, the Extractor won't build. If Extensions renames a parameter, `ExtensionsContractTests` fails. The only truly unguarded coupling is the two C#14 extension method names (`"Translation"`, `"Display"`), which are public API and unlikely to change without a major version.

### Rules

**1. All Extensions references go through ExtensionsContract.**

Never use a bare string literal that matches a type, method, or parameter name from Extensions. Reference the constant in `ExtensionsContract`. If the constant doesn't exist, add it there first.

**2. Strings flow out, never in.**

Roslyn-derived strings (argument text, expression text, literal values) flow *out* to `TranslationDefinition`, `TranslationReference`, and export formats. They are *never* used as match targets for interpretation logic. The scanner decides what something *is* by method/type name dispatch, then reads what it *contains* as output data.

**3. DefinitionKind is the discriminator.**

The `DefinitionKind` enum (`InlineTranslation`, `ReusableDefinition`, `EnumAttribute`, `ResourceFile`) is the machine-readable discriminator for how a translation was defined. Renderers switch on `Kind`, never parse `Context` strings. Each scanner adapter sets `Kind` explicitly when constructing a `DefinitionSite`.

**4. Only InlineTranslation creates a reference.**

`.Translation(key, message)` in a Razor/code file genuinely defines source text AND uses the key at runtime — so it produces both a `TranslationDefinition` and a `TranslationReference`. All other mechanisms (`DefineXxx`, `[Translation]`, `.resx`) produce only a `TranslationDefinition`. This prevents "phantom resolved" status on unused definitions.

**5. Fail visibly.**

`ScanTargets.ResolveLocalizer()` / `ResolveTranslationAttribute()` / `ResolveDefinitionFactory()` return `null` if the type isn't found in compilation references. Callers must handle the null case — skip gracefully or emit a diagnostic, never silently produce wrong output.

### Forbidden Patterns

**F1: Bare string literal for an Extensions name**

```csharp
// FORBIDDEN — "For" is a magic string. If renamed in Extensions, silently breaks.
if (link.MethodName == "For")

// CORRECT — reference ExtensionsContract constant (backed by nameof).
if (link.MethodName == ExtensionsContract.ChainFor)
```

**F2: Parsing DefinitionSite.Context for rendering decisions**

```csharp
// FORBIDDEN — fragile string matching.
if (def.Context?.Contains("DefineSimple") == true)
    ShowAsReusable();

// CORRECT — machine-readable discriminator.
if (def.Kind == DefinitionKind.ReusableDefinition)
    ShowAsReusable();
```

**F3: Fake references to inflate status**

```csharp
// FORBIDDEN — creating a TranslationReference at the definition site to make it look "Resolved".
var def = new TranslationDefinition(...);
var fakeRef = new TranslationReference(...); // same file:line as def

// CORRECT — only InlineTranslation is genuinely both define + use.
// MergedTranslation.Status already handles this: InlineTranslation counts as having a reference.
```

**F4: String-based parameter value as logic key**

```csharp
// FORBIDDEN — using the extracted text to decide what type of entry to create.
if (args.Any(a => a.ParameterName == "count"))
    return BuildPlural(call);

// CORRECT — the method name or return type tells you the form.
if (call.MethodName.StartsWith(ExtensionsContract.DefinePrefix))
    return InterpretDefinitionFactory(call, file);
```

### The Grep Test

If you can `grep -r` the Roslyn adapter for any quoted string that matches a type, method, or parameter name in the Extensions project **and** that string is not defined in `ExtensionsContract.cs`, the contract is violated.

## Build & Run

```bash
dotnet build src/BlazorLocalization.Extractor
dotnet run --project src/BlazorLocalization.Extractor -- extract tests/SampleBlazorApp -f po
dotnet run --project src/BlazorLocalization.Extractor -- inspect tests/SampleBlazorApp
dotnet run --project src/BlazorLocalization.Extractor -- extract --help
```
