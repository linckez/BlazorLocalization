# Contributing to BlazorLocalization.Extensions

> For user-facing docs, see [`/docs/`](../../docs/). This file is for maintainers.

## Philosophy

This library does one thing: replace Microsoft's `ResourceManager` / `.resx` backend behind `IStringLocalizer` with something that supports over-the-air updates, distributed caching, and plurals. That's it.

We don't reinvent localization. Microsoft's `IStringLocalizer`, `IStringLocalizerFactory`, `UseRequestLocalization()`, and the entire culture detection pipeline stay exactly as shipped. We replace `AddLocalization()` with `AddProviderBasedLocalization()` — same interface, better backend.

Dependencies are rock-solid and actively maintained by large communities:

- **FusionCache** — cache layer (L1 memory + optional L2 distributed)
- **SmartFormat.NET** — named placeholder resolution
- **Microsoft.Extensions.Localization.Abstractions** — the `IStringLocalizer` contract itself

If Microsoft ships a capability, we use it. If a well-maintained library already solves a problem, we depend on it. No clever reimplementations.

## The Two-Layer Split

Microsoft designed `IStringLocalizer` as a minimal contract: two indexers + `GetAllStrings()`. This library adds a second layer on top:

| Layer | Type | Owns |
|-------|------|------|
| **Infrastructure** | `ProviderBasedStringLocalizer` | Implements `IStringLocalizer`. Cache lookup, provider chain, culture fallback, error handling. Knows nothing about source-text fallback, placeholders, or plurals. |
| **Application API** | `StringLocalizerExtensions.Translation()` / `.Display()` | Extension methods on `IStringLocalizer`. Fluent builders (`SimpleBuilder`, `PluralBuilder`, `SelectBuilder<T>`, `SelectPluralBuilder<T>`), SmartFormat placeholders, CLDR plural categories, inline translations. Resolves via `ToString()`. |
| **Reusable Definitions** | `Translations.DefineSimple()` / `DefinePlural()` / `DefineSelect<T>()` / `DefineSelectPlural<T>()` | Pure data holders (`SimpleDefinition`, `PluralDefinition`, `SelectDefinition<T>`, `SelectPluralDefinition<T>`) that capture key + source messages + inline translations. Resolved at call-site by `StringLocalizerExtensions` which replays the stored config into the corresponding runtime builder. |

**Why separate?** `Translation()` works with *any* `IStringLocalizer` implementation — not just ours. Someone using stock `ResourceManager` can still get source-text fallback, plurals, and placeholders by calling `Translation()` on their existing localizer. The infrastructure layer is invisible to them.

## Provider Contract

Every translation provider implements one method:

```csharp
Task<string?> GetTranslationAsync(string culture, string key, CancellationToken ct)
```

Three outcomes:
- Return a string → this provider has the translation
- Return `null` → this provider doesn't have it, try the next one
- Throw `TranslationProviderTransientException` → retriable failure (network, timeout). FusionCache serves the last known good value
- Throw `TranslationProviderConfigurationException` → permanent failure (wrong credentials, bad config). Requires developer intervention

Providers don't cache. FusionCache owns TTL, eviction, fail-safe, and L2 persistence. The provider is a pure fetcher.

Providers are tried in registration order. First non-null result wins.

## Sentinel + Fan-Out Pattern

All built-in providers (JsonFile, PoFile) use the same caching strategy to bridge "one file per culture" sources to "one cache entry per key" lookups:

1. **Sentinel key** gates bulk I/O (e.g. `jsonfile:JsonFile:culture:da`)
2. On first miss → read the entire culture file
3. **Fan out** every key into FusionCache as individual entries
4. Subsequent lookups are O(1) cache hits — zero I/O until TTL expires
5. FusionCache stampede protection ensures one file read per culture per TTL cycle

New providers that fetch per-culture (not per-key) should follow this pattern.

## Where Things Go

| You're adding... | Put it in... |
|-----------------|-------------|
| New translation provider | `Providers/{Name}/` subfolder — provider class + options class + `IServiceCollection` extension on `ProviderBasedLocalizationBuilder` |
| New builder feature (e.g. a new fluent method) | `Translation/` — the relevant builder class |
| New cache/config option | `ProviderBasedLocalizationOptions` |
| New exception type | `Exceptions/` — inherit from `TranslationProviderException` |
