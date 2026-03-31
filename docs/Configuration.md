[< Back to README](../README.md)

# Configuration

`AddProviderBasedLocalization()` replaces `AddLocalization()`. Do not call both — both register `IStringLocalizerFactory`, the last one wins, and behavior becomes unpredictable.

> **Note:** You still need ASP.NET Core's `UseRequestLocalization()` middleware for culture detection. BlazorLocalization looks up translations for the current culture — but determining *which* culture applies to a request is the framework's job.

**On this page:** [Culture & Request Pipeline](#culture--request-pipeline) · [Minimal Setup](#minimal-setup) · [Cache Options](#cache-options) · [Custom Configuration Section](#custom-configuration-section) · [Multiple Providers](#multiple-providers) · [L2 Distributed Cache](#l2-distributed-cache) · [Code-Only Configuration](#code-only-configuration) · [Full Example](#full-example) · [Translation Providers](#translation-providers) · [How It Works](#how-it-works)

## Culture & Request Pipeline

Add the request localization middleware **before** `MapRazorComponents` (Blazor) or `MapControllers` / `app.Run()` (API):

```csharp
var supportedCultures = new[] { "en-US", "de", "pl", "da" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));
```

The middleware evaluates culture providers in order: **query string → cookie → `Accept-Language` header → default culture**. On a first visit (no cookie), the browser's `Accept-Language` header auto-detects culture — just like Web APIs.

When the exact culture isn't available, your app automatically tries the parent culture: `es-MX` → `es` → source text. Your default culture's source text is always the last resort — users never see blank strings or raw keys.

For **Blazor Server**, persist the detected culture into a cookie so subsequent requests stay consistent.

For **Web APIs**, `Accept-Language` drives every request — no cookie needed.

> For the full picture on culture determination, cookie providers, and client-side Blazor, see Microsoft's [Blazor globalization and localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization?view=aspnetcore-10.0).
>
> For working examples with everything wired up, see the [samples/](../samples/) directory.

## Minimal Setup

With no provider, inline translations and source text work immediately:

```csharp
builder.Services.AddProviderBasedLocalization();
```

Add a provider when you're ready. For example, Crowdin:

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider();
```

```json
{
  "Localization": {
    "TranslationProviders": {
      "Crowdin": {
        "DistributionHash": "e-abc123def456"
      }
    }
  }
}
```

`AddProviderBasedLocalization()` looks for a `"Localization"` section by default. Provider options come from `Localization:TranslationProviders:{ProviderName}`.

See [Providers/](Providers/) for provider-specific setup and available options.

## Cache Options

All cache options live directly under the `"Localization"` section:

```json
{
  "Localization": {
    "TranslationDuration": "01:00:00",
    "FailSafeMaxDuration": "7.00:00:00",
    "CacheName": "BlazorLocalization",
    "TranslationProviders": { }
  }
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `TranslationDuration` | TimeSpan | `01:00:00` (1 h) | How long a translation is fresh. After expiry, FusionCache refreshes in the background. |
| `FailSafeMaxDuration` | TimeSpan | 365 days | How long your app keeps using stale translations when providers are down. |
| `CacheName` | string | `"BlazorLocalization"` | FusionCache instance name. Change only to avoid collisions with your own FusionCache usage. |

Equivalent in code:

```csharp
builder.Services.AddProviderBasedLocalization(options =>
{
    options.TranslationDuration = TimeSpan.FromMinutes(30);
    options.FailSafeMaxDuration = TimeSpan.FromDays(7);
});
```

When both `appsettings.json` and code are used, `appsettings.json` is applied first, then code overrides.

## Custom Configuration Section

If your settings live under a different path:

```csharp
builder.Services.AddProviderBasedLocalization(
        builder.Configuration.GetSection("MyApp:Localization"))
    .AddCrowdinTranslationProvider();
```

```json
{
  "MyApp": {
    "Localization": {
      "TranslationProviders": {
        "Crowdin": {
          "DistributionHash": "e-abc123def456"
        }
      }
    }
  }
}
```

## Multiple Providers

Register providers in the order you want them tried — first one with a translation wins.

**Common pattern** — Crowdin with local JSON fallback:

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider()         // try Crowdin first
    .AddJsonFileTranslationProvider(opts =>  // fall back to local files
        opts.TranslationsPath = "Translations");
```

Multiple Crowdin distributions with unique names:

```csharp
builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider()                          // "Crowdin" (default)
    .AddCrowdinTranslationProvider("InternalDocs");           // "InternalDocs"
```

```json
{
  "Localization": {
    "TranslationProviders": {
      "Crowdin": {
        "DistributionHash": "e-abc123def456"
      },
      "InternalDocs": {
        "DistributionHash": "x-789ghi012jkl"
      }
    }
  }
}
```

Registering the same name twice throws `InvalidOperationException`.

## L2 Distributed Cache

Register any `IDistributedCache` **before** `AddProviderBasedLocalization()`. FusionCache picks it up automatically:

```csharp
// SQLite (survives restarts, no external infra)
builder.Services.AddSqliteCache(o => o.CachePath = "translations.db");

// — or Redis, SQL Server, etc. —

builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider();
```

No L2 registered? Runs L1-only (in-memory). Translations are re-fetched on restart.

## Code-Only Configuration

For environments where `appsettings.json` isn't used (e.g. tests, serverless):

```csharp
builder.Services.AddProviderBasedLocalization(options =>
    {
        options.TranslationDuration = TimeSpan.FromMinutes(5);
    })
    .AddCrowdinTranslationProvider(options =>
    {
        options.DistributionHash = "e-abc123def456";
    });
```

## Full Example

```json
{
  "Localization": {
    "TranslationDuration": "00:30:00",
    "FailSafeMaxDuration": "7.00:00:00",
    "TranslationProviders": {
      "Crowdin": {
        "DistributionHash": "e-abc123def456"
      }
    }
  }
}
```

```csharp
builder.Services.AddSqliteCache(o => o.CachePath = "translations.db");

builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider();
```

## Translation Providers

For provider-specific setup, file formats, and options, see the dedicated docs:

- [Crowdin Provider](Providers/Crowdin.md) — Over-the-air translations from Crowdin
- [JSON File Provider](Providers/JsonFile.md) — flat JSON files on disk
- [PO File Provider](Providers/PoFile.md) — GNU gettext PO files on disk

## How It Works

**Build time:** The [Extractor CLI](Extractor.md) scans your code, finds every `IStringLocalizer` usage, and exports source strings. Upload these to your translation provider — manually, or automate it in CI.

**Each `Translation()` call:** Your app checks the cache first. On a cache miss, it fetches from your provider(s) and tries parent cultures if the exact locale isn't available (`es-MX` → `es` → source text). Translations are cached by [FusionCache](https://github.com/ZiggyCreatures/FusionCache) — L1 in-memory, optional L2 distributed.

On a fresh start with an empty cache, users see your source text while translations load in the background. Once the provider responds, translations appear automatically. With an L2 cache (Redis, SQLite), translations survive restarts — your app serves them from L2 while the provider refreshes in the background.

---

**See also:** [Examples](Examples.md) for `Translation()` usage · [Extractor CLI](Extractor.md) for string extraction
