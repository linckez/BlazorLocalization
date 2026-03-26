[< Back to README](../../README.md)

# PO File Translation Provider

Load translations from GNU gettext PO files on disk. Ships built into `BlazorLocalization.Extensions` — no extra NuGet package needed.

## Quick Start

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddPoFileTranslationProvider(opts =>
        opts.TranslationsPath = Path.Combine(builder.Environment.ContentRootPath, "Translations"));
```

Or via `appsettings.json`:

```csharp
builder.Services.AddProviderBasedLocalization(builder.Configuration)
    .AddPoFileTranslationProvider();
```

```json
{
  "Localization": {
    "TranslationProviders": {
      "PoFile": {
        "TranslationsPath": "Translations"
      }
    }
  }
}
```

One file per culture in your configured `TranslationsPath`. Any culture without a file falls back through the culture chain (see [Configuration](../Configuration.md#culture--request-pipeline)).

```
Translations/
├── da.po
├── es-MX.po
└── vi.po
```

See [Configuration](../Configuration.md) for cache settings, custom sections, and L2 setup.

## Options

| Property | Type | Default | Description |
|---|---|---|---|
| `TranslationsPath` | string | **required** | Directory containing translation PO files. Absolute or relative to the app's content root. |
| `FilePattern` | string | `"{culture}.po"` | File naming pattern. `{culture}` is replaced with the culture name (e.g. `da`, `es-MX`). |

> For all available options, explore `PoFileTranslationProviderOptions` in your IDE — XML docs describe each property.

## PO Format

Standard GNU gettext format with `msgid` / `msgstr` pairs:

```po
msgid "Home.Title"
msgstr "Velkommen"

msgid "Home.Greeting"
msgstr "Hej, {Name}!"
```

### Plural Support

PO files express plurals natively with `msgid_plural` and indexed `msgstr[N]` entries:

```po
msgid "Home.ItemCount"
msgid_plural "Home.ItemCount"
msgstr[0] "{Quantity} genstand"
msgstr[1] "{Quantity} genstande"
```

The provider maps these to `_one` / `_other` key suffixes:
- `msgstr[0]` → `Home.ItemCount_one`
- `msgstr[1]` → `Home.ItemCount_other`

This matches SmartFormat's plural resolution convention used by `Translation()`.

### Generating PO Files

The Extractor CLI can generate PO source files from your code:

```bash
blazor-loc extract ./src --format po --output ./translations
```

The generated `.pot` file includes `#:` source references and `#.` extracted comments — rich context for translators. Upload to your translation provider, then download the translated `.po` files and place them in your `TranslationsPath`.

## Multiple Providers

Register multiple with unique names:

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddPoFileTranslationProvider(opts =>
        opts.TranslationsPath = "Translations/Main")
    .AddPoFileTranslationProvider("Shared", opts =>
        opts.TranslationsPath = "Translations/Shared");
```

Providers are tried in registration order — first non-null result wins.

## How It Works

Each culture's PO file is loaded once per TTL cycle via a FusionCache sentinel. Individual keys are fanned out into cache entries, so subsequent lookups are O(1) with zero disk I/O.

Missing files are not an error — the localizer walks the culture fallback chain (`es-MX` → `es` → source text) automatically.

## Error Handling

| Scenario | Behavior |
|---|---|
| File doesn't exist | Debug log. No translations for this culture — localizer falls back. |
| I/O error (permissions, disk) | Warning log. Sentinel set — retries after TTL. Stale translations served if available. |

No exceptions propagate to callers — the provider absorbs all I/O errors at the sentinel level.
