[< Back to README](../../README.md)

# JSON File Translation Provider

Load translations from flat JSON files on disk. Ships built into `BlazorLocalization.Extensions` — no extra NuGet package needed.

## Quick Start

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddJsonFileTranslationProvider(opts =>
        opts.TranslationsPath = Path.Combine(builder.Environment.ContentRootPath, "Translations"));
```

Or via `appsettings.json`:

```csharp
builder.Services.AddProviderBasedLocalization(builder.Configuration)
    .AddJsonFileTranslationProvider();
```

```json
{
  "Localization": {
    "TranslationProviders": {
      "JsonFile": {
        "TranslationsPath": "Translations"
      }
    }
  }
}
```

One file per culture in your configured `TranslationsPath`. Any culture without a file falls back through the culture chain (see [Configuration](../Configuration.md#culture--request-pipeline)).

```
Translations/
├── da.json
├── es-MX.json
└── vi.json
```

Each file is flat key/value:

```json
{
  "Home.Title": "Velkommen",
  "Home.Greeting": "Hej, {Name}!",
  "Home.ItemCount_one": "{Quantity} genstand",
  "Home.ItemCount_other": "{Quantity} genstande"
}
```

See [Configuration](../Configuration.md) for cache settings, custom sections, and L2 setup.

## Options

| Property | Type | Default | Description |
|---|---|---|---|
| `TranslationsPath` | string | **required** | Directory containing translation JSON files. Absolute or relative to the app's content root. |
| `FilePattern` | string | `"{culture}.json"` | File naming pattern. `{culture}` is replaced with the culture name (e.g. `da`, `es-MX`). |

> For all available options, explore `JsonFileTranslationProviderOptions` in your IDE — XML docs describe each property.

## File Naming

The default pattern `{culture}.json` looks for files like `da.json`, `es-MX.json`. Override for custom conventions:

```csharp
.AddJsonFileTranslationProvider(opts =>
{
    opts.TranslationsPath = "Translations";
    opts.FilePattern = "translations.{culture}.json";
});
```

This looks for `translations.da.json`, `translations.es-MX.json`, etc.

## JSON Format

Flat `"key": "value"` objects. Non-string values are ignored.

**Singular keys:**
```json
{
  "Home.Title": "Velkommen",
  "Home.Greeting": "Hej, {Name}!"
}
```

**Plural keys** use `_one` / `_other` suffixes, matching the convention used by SmartFormat:
```json
{
  "Home.ItemCount_one": "{Quantity} genstand",
  "Home.ItemCount_other": "{Quantity} genstande"
}
```

This is the same flat format that Crowdin exports when configured for Crowdin i18next JSON output.

> **Tip:** You can download translations from Crowdin during CI and serve them from disk at runtime — no need for the Crowdin OTA provider if you prefer file-based deployments.

## Multiple Providers

Register multiple with unique names:

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddJsonFileTranslationProvider(opts =>
        opts.TranslationsPath = "Translations/Main")
    .AddJsonFileTranslationProvider("Shared", opts =>
        opts.TranslationsPath = "Translations/Shared");
```

Providers are tried in registration order — first non-null result wins.

## How It Works

Each culture's JSON file is loaded once per cache duration cycle (default 1 hour — see [Configuration](../Configuration.md#cache-options)) via a FusionCache sentinel. Individual keys are fanned out into cache entries, so subsequent lookups are O(1) with zero disk I/O.

Missing files are not an error — the localizer walks the culture fallback chain (`es-MX` → `es` → source text) automatically. This means you only need to provide files for cultures you've actually translated.

## Error Handling

| Scenario | Behavior |
|---|---|
| File doesn't exist | Debug log. No translations for this culture — localizer falls back. |
| File not valid JSON | Warning log. Sentinel set — retries after cache expiry. Stale translations served if available. |
| I/O error (permissions, disk) | Warning log. Same retry-after-expiry behavior. |

No exceptions propagate to callers — the provider absorbs all I/O and parse errors at the sentinel level.

---

**See also:** [Configuration](../Configuration.md) for cache settings and multi-provider setup · [PO File Provider](PoFile.md) for an alternative file format
