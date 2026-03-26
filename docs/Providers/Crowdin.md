[< Back to README](../../README.md)

# Crowdin

`BlazorLocalization.TranslationProvider.Crowdin` fetches translations from [Crowdin](https://crowdin.com/) at runtime — no redeployment needed when translators update strings.

This provider uses Crowdin's CDN Distributions (Over-The-Air Content Delivery). [API Exported File Bundles](https://support.crowdin.com/bundles/) are not yet supported.

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
| **Placeholder conversion** | Leave default | Your placeholders (`{count}`) will work as-is. |
| **CDATA** | Either | Both work — no difference at runtime. |
| **Line break conversion** | **OFF** | If enabled, `\n` becomes literal backslash-n in your UI. |

## Uploading Source Strings

**Use PO format.** It gives translators file locations and context comments directly in the Crowdin editor — the single biggest quality improvement you can give them.

```bash
blazor-loc extract ./src --format po --output ./translations
```

> **Key insight:** Upload format and download format are independent — **PO for upload** (rich translator context), **Android XML for download** (lightweight runtime).

## How It Works

Translations are fetched from the Crowdin CDN once per culture, then cached. After the cache expires (default 1 hour — see [Configuration](../Configuration.md#cache-options)), a background refresh fetches updated translations without blocking your app.

If Crowdin is unreachable, the last known good translations are served until Crowdin comes back.

## Multiple Crowdin Distributions

Register multiple with unique names. Each name maps to a `TranslationProviders:{name}` section:

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider()                  // "Crowdin" (default name)
    .AddCrowdinTranslationProvider("InternalDocs");   // resolves from TranslationProviders:InternalDocs
```

Providers are tried in registration order — first non-null result wins.

## Error Handling

| Situation | What you see | What to do |
|---|---|---|
| Crowdin is down, network issue, timeout | Nothing — last known good translations are served silently | Wait it out. Self-heals when connectivity returns. |
| Wrong distribution hash, bad config | Warning in logs immediately | Fix your `DistributionHash` in `appsettings.json`. |

Your app never shows broken strings — stale translations are served until the issue resolves or you fix the config.

## FAQ

**Does the provider cache translations internally?**
No. All caching is handled by FusionCache — configured in [Configuration](../Configuration.md#cache-options).

**Can I use other export formats (PO, RESX, i18next JSON)?**
Not with CDN Distributions. Those formats require installing community exporter apps from the [Crowdin Store](https://store.crowdin.com/). CDN Distributions only support the three system default exporters (Android XML, iOS Strings, XLIFF), and this provider uses Android XML.

---

**See also:** [Configuration](../Configuration.md) for cache settings and multi-provider setup · [Extractor CLI](../Extractor.md) for generating source files to upload
