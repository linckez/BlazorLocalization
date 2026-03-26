[< Back to README](../../README.md)

# Crowdin

`BlazorLocalization.TranslationProvider.Crowdin` fetches translations from [Crowdin](https://crowdin.com/) at runtime — no redeployment needed when translators update strings.

Crowdin offers two delivery methods: **CDN Distributions** (OTA) and **API**. This provider supports CDN Distributions. API support is planned.

> **Scope:** Covers CDN Distributions (Over-The-Air Content Delivery). [API Exported File Bundles](https://support.crowdin.com/bundles/) are a different feature and not yet supported.

## Quick Start

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider();
```

```json
{
  "Localization": {
    "TranslationProviders": {
      "Crowdin": {
        "DistributionHash": "your-distribution-hash"
      }
    }
  }
}
```

Options bind from `Localization:TranslationProviders:Crowdin` automatically. See [Configuration](../Configuration.md) for cache settings, custom sections, and L2 setup.

> For all available options, explore `CrowdinTranslationProviderOptions` in your IDE — XML docs describe each property.

## Getting Your Distribution Hash

1. Open your Crowdin project → **Content Delivery** (or **Over-The-Air Content Delivery** in Enterprise).
2. Create a new distribution (or use an existing one).
3. The export format is **Android XML** — one of the three system default exporters.
4. Copy the **Distribution Hash** — it looks like `e-abc123def456`.

### Android XML Exporter Settings

Crowdin's Android XML exporter has three settings. The recommended configuration:

| Setting | Recommended | Why |
|---|---|---|
| **Placeholder conversion** | Leave default | SmartFormat named placeholders (`{count}`) pass through as-is. |
| **CDATA** | Either | XDocument handles CDATA transparently — no difference at runtime. |
| **Line break conversion** | **OFF** | Leave off. If enabled, Crowdin converts `\n` to `\\n` literals which appear as backslash-n in your UI. |

## Uploading Source Strings

**Use PO.** When you run the Extractor with `--format po`, the output includes metadata that Crowdin surfaces to translators:

```bash
blazor-loc extract ./src --format po --output ./translations
```

The generated `.po` file carries:

- **`#:` source references** — `Components/Pages/Home.razor:42` tells the translator exactly which component and line the string comes from
- **`#.` extracted comments** — additional context from your code, when present
- **`msgid_plural`** — native plural support without key-suffix hacks

Translators see file locations and context comments directly in the Crowdin editor. This is the single biggest quality improvement you can give them — a string like `"Cycle"` means completely different things in a machine settings page vs. a billing page.

> **Key insight:** Upload format and download format are independent — **PO for upload** (rich translator context), **Android XML for download** (lightweight runtime).

## How It Works

```
IStringLocalizer["key"]
    └─► FusionCache (L1/L2)
            └─► CrowdinTranslationProvider
                    └─► CrowdinOtaClient
                            ├─► GET /manifest.json   (cached, refreshes per TTL)
                            └─► GET /{culture}.xml   (once per culture per TTL)
```

- **Manifest** maps culture codes to CDN content paths. Cached with the same TTL as translations.
- **Translation files** are fetched once per culture on first demand. Concurrent requests share a single HTTP call (stampede protection via FusionCache).
- **FusionCache** owns TTL, eviction, fail-safe, and L2 persistence. The provider stores nothing itself.

## Multiple Crowdin Distributions

Register multiple with unique names. Each name maps to a `TranslationProviders:{name}` section:

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider()                  // "Crowdin" (default name)
    .AddCrowdinTranslationProvider("InternalDocs");   // resolves from TranslationProviders:InternalDocs
```

Providers are tried in registration order — first non-null result wins.

## Error Handling

Provider exceptions are classified so you can tell "wait it out" from "fix your config":

| Exception | Cause | Self-heals? |
|---|---|---|
| `TranslationProviderTransientException` | CDN timeout, network down, HTTP 5xx | Yes — fail-safe serves stale |
| `TranslationProviderConfigurationException` | Wrong hash, bad distribution config | No — developer must fix |

Both carry a `ProviderName` property for log correlation.

- **Transient:** Nothing at default log levels. FusionCache serves the last known good value silently.
- **Configuration:** A Warning surfaces immediately — won't self-heal.

## FAQ

**What happens if Crowdin is down?**
FusionCache's fail-safe returns the last known good value indefinitely (default). Your app never shows broken strings.

**Does the provider cache translations internally?**
No. FusionCache handles all caching. The provider is a pure fetcher.

**Can I use other export formats (PO, RESX, i18next JSON)?**
Not with CDN Distributions. Those formats require installing community exporter apps from the [Crowdin Store](https://store.crowdin.com/). CDN Distributions only support the three system default exporters (Android XML, iOS Strings, XLIFF), and this provider uses Android XML.

---

**See also:** [Configuration](../Configuration.md) for cache settings and multi-provider setup · [Extractor CLI](../Extractor.md) for generating source files to upload
