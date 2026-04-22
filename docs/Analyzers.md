[< Back to README](../README.md)

# Analyzers

`BlazorLocalization.Analyzers` catches common localization mistakes at compile time — before your app ships. It also nudges migration from the vanilla `IStringLocalizer` indexer toward the `Translation()` API.

## Install

```bash
dotnet add package BlazorLocalization.Analyzers
```

## Rules

| Rule | What it catches | Default | Code fix |
|------|----------------|---------|----------|
| [BL0001](analyzers/BL0001-empty-key.md) | Empty translation key (`""`) | Warning | — |
| [BL0002](analyzers/BL0002-use-translation-api.md) | `Loc["key"]` or `GetString()` instead of `Translation()` | Info | Yes — wraps in `Translation()` |
| [BL0003](analyzers/BL0003-duplicate-key.md) | Same key with different source texts | Warning | — |
| [BL0004](analyzers/BL0004-unscoped-generic.md) | `IStringLocalizer<T>` where `<T>` has no effect | Info | Yes — removes `<T>` |
| [BL0005](analyzers/BL0005-undefined-key.md) | Key-only `Translation("key")` with no definition | Warning | — |
| [BL0006](analyzers/BL0006-translation-file-conflict.md) | Same key with different values across translation files | Warning | — |

## Refactorings

These appear in the screwdriver / hammer menu (right-click → Refactor), not the lightbulb.

| Refactoring | What it does |
|-------------|-------------|
| [Extract Translation Definition](analyzers/extract-translation-definition.md) | Splits an inline `Translation("key", "message")` into a reusable `DefineSimple` field + `Translation(field)` call |
| [Enrich with Translations](analyzers/enrich-with-translations.md) | Appends `.For("culture", "text")` calls from your `.resx` files to an existing `Translation()` call |

## Customize Severity

All rules are configurable via `.editorconfig`. Promote Info-level rules to Warning so they show up in `dotnet build` output:

```ini
[*.cs]
dotnet_diagnostic.BL0002.severity = warning
dotnet_diagnostic.BL0004.severity = warning
```

Or suppress a rule entirely:

```ini
[*.cs]
dotnet_diagnostic.BL0002.severity = none
```

**I want to keep using `Loc["key"]`** — no problem. Suppress BL0002 and the indexer works exactly as before. BL0001 and BL0003 still catch empty keys and duplicates regardless of which API style you use.

---

**See also:** [Examples](Examples.md) for `Translation()` usage · [Configuration](Configuration.md) for setup · [Extractor CLI](Extractor.md) for string extraction
