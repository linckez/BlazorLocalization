# Contributing to BlazorLocalization.TranslationProvider.Crowdin

> For user-facing docs, see [`/docs/Providers/Crowdin.md`](../../docs/Providers/Crowdin.md). This file is for maintainers.

## What This Does

Bridges the Crowdin OTA (Over-the-Air) CDN to `ITranslationProvider`. Crowdin's CDN delivers one file per culture containing every translation. `ITranslationProvider` is a per-key API. This package handles the impedance mismatch.

## OTA Client Flow

```
manifest.json  →  culture file URL  →  HTTP GET  →  ICrowdinFileParser  →  fan-out to per-key cache entries
```

1. **Manifest** — `{BaseUrl}/{DistributionHash}/manifest.json` lists every language and its CDN content paths. Cached with the same TTL as translations.
2. **Culture resolution** — Tries exact match (e.g. `es-MX`), then falls back to the two-letter code (`es`).
3. **HTTP fetch** — Downloads the translation file for the matched culture path.
4. **Parse** — `ICrowdinFileParser.Parse()` turns the raw response into a flat `Dictionary<string, string>`.
5. **Fan-out** — Each key/value pair is stored as an individual FusionCache entry (`crowdin:{providerName}:{culture}:{key}`).

## Parsing

`ICrowdinFileParser` is the extension point. Currently one implementation:

- **`AndroidXmlParser`** — Parses `<resources><string name="key">value</string></resources>`. Strips Crowdin's quoted-key quirk: dotted keys like `RC.Title` arrive wrapped in literal `"` quotes (`"RC.Title"`) and need unquoting.

To add a new format (e.g. JSON, iOS Strings): implement `ICrowdinFileParser`, then wire it into `CrowdinOtaClient`. The parser is currently hardcoded there — if a second format is needed, promote it to an option on `CrowdinTranslationProviderOptions`.

## Caching Strategy

Three tiers, all using the shared FusionCache instance from `ProviderBasedLocalizationOptions.CacheName`:

| What | Cache key pattern | Purpose |
|------|------------------|---------|
| Manifest | `crowdin:{providerName}:manifest` | Avoids re-fetching the language/path list on every request |
| Culture sentinel | `crowdin:{providerName}:culture:{culture}` | Gates the bulk HTTP fetch — at most one per culture per TTL cycle |
| Individual keys | `crowdin:{providerName}:{culture}:{key}` | O(1) per-key lookups after the culture is loaded |

The sentinel pattern means: first request for a culture triggers one HTTP call that fans out all keys. Subsequent requests are cache hits. FusionCache's stampede protection ensures concurrent requests don't pile up into duplicate HTTP calls.

TTL is inherited from `ProviderBasedLocalizationOptions.TranslationDuration` — one knob controls the end-to-end CDN refresh rate. L2 persistence (Redis, SQLite, etc.) is inherited automatically, so translations survive app restarts without a cold-start HTTP storm.

## Error Handling

All runtime errors are `TranslationProviderTransientException` — the root cause (Crowdin dashboard settings, CDN availability) is external and can self-heal without redeploying the app.

On failure the culture sentinel is still set to `true`, so retries happen only after the TTL expires — the TTL is the natural backoff window. FusionCache's fail-safe keeps serving previously cached translations in the meantime.

Log levels vary by root cause:
- **Debug** — Network errors (expected, temporary)
- **Warning** — Format/parse errors (someone changed the CDN Distribution export format and needs to fix it)

## Registration API

Six overloads on `ProviderBasedLocalizationBuilder`, all extension methods:

| Overload | What it does |
|----------|-------------|
| `AddCrowdinTranslationProvider()` | Default name `"Crowdin"`, binds from `TranslationProviders:Crowdin` config section |
| `AddCrowdinTranslationProvider(string providerName)` | Named, binds from `TranslationProviders:{providerName}` config section |
| `AddCrowdinTranslationProvider(Action<opts>)` | Default name, inline configuration |
| `AddCrowdinTranslationProvider(IConfiguration)` | Default name, explicit config section |
| `AddCrowdinTranslationProvider(string, Action<opts>)` | Named, inline configuration |
| `AddCrowdinTranslationProvider(string, IConfiguration)` | Named, explicit config section |

The `providerName` does quadruple duty: named-options key, cache key prefix, named `HttpClient` name (`Crowdin:{providerName}`), and log/exception identification.

Multiple Crowdin providers can coexist — each gets its own `CrowdinOtaClient` instance, `HttpClient`, and cache key namespace.
