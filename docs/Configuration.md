[< Back to README](../README.md)

# Configuration

`AddProviderBasedLocalization()` replaces `AddLocalization()`. Do not call both — both register `IStringLocalizerFactory`, the last one wins, and behavior becomes unpredictable.

> **Note:** You still need ASP.NET Core's `UseRequestLocalization()` middleware for culture detection. BlazorLocalization resolves translations for the current culture — but determining *which* culture applies to a request is the framework's job.

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

The middleware evaluates culture providers in order: **query string → cookie → `Accept-Language` header → default culture**. On a first visit (no cookie), the browser's `Accept-Language` header auto-detects culture — the same mechanism that drives Web APIs.

When the requested culture isn't available from any provider, BlazorLocalization walks the culture fallback chain automatically: `da-DK` → `da` → source text. Your default culture's source text is always the last resort — users never see blank strings or raw keys.

For **Blazor Server**, persist the detected culture into a cookie so subsequent requests stay consistent. `App.razor` does this in `OnInitialized()` by writing `CultureInfo.CurrentCulture` to `CookieRequestCultureProvider.DefaultCookieName`. A `CultureController` lets users override the auto-detected culture via a UI dropdown.

For **Web APIs**, `Accept-Language` drives every request — no cookie or controller needed.

> For the full picture on culture determination, cookie providers, and client-side Blazor, see Microsoft's [Blazor globalization and localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization?view=aspnetcore-10.0).
>
> For working examples with everything wired up, see the [samples/](../samples/) directory.

## Minimal Setup

With no provider, inline translations and source text work immediately:

```csharp
builder.Services.AddProviderBasedLocalization();
```

Add a provider when you're ready. For example, Crowdin OTA:

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

`AddProviderBasedLocalization()` looks for a `"Localization"` section by default. Provider options bind from `Localization:TranslationProviders:{ProviderName}`.

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
| `FailSafeMaxDuration` | TimeSpan | 365 days | How long a stale translation can be served when providers are down. |
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

Stack providers for fallback chains. Providers are tried in registration order — the first non-null result wins.

**Common pattern** — Crowdin OTA with local JSON fallback:

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

- [Crowdin Provider](Providers/Crowdin.md) — OTA translations from Crowdin CDN
- [JSON File Provider](Providers/JsonFile.md) — flat JSON files on disk
- [PO File Provider](Providers/PoFile.md) — GNU gettext PO files on disk

## How It Works

```
┌─────────────────────┐         ┌──────────────────┐         ┌─────────────────────┐
│   Your Blazor App   │  out →  │  Crowdin / API / │  in →   │   Your Blazor App   │
│                     │         │  Database / Disk │         │                     │
│  Loc.Translation()  │───────▶ │                  │───────▶ │  cached translations│
│  Loc["Key"]         │ blazor- │  translated      │ ITransl │  with fallback to   │
│  .resx files        │  loc    │  strings         │ ationPr │  your source text    │
│                     │ extract │                  │ ovider  │                     │
└─────────────────────┘         └──────────────────┘         └─────────────────────┘
   Extractor (build time)          Your translation            Extensions (runtime)
                                      provider
```

**Build time:** The Extractor scans your code via Roslyn, finds every `IStringLocalizer` usage, and exports source strings in your chosen export format. Upload these to your translation provider — manually, or automate it in CI (e.g. Crowdin GitHub Action).

**Export formats:** Crowdin i18next JSON, GNU Gettext PO, and plain key-value JSON. More formats are planned.

**Runtime:** The Extensions library replaces `ResourceManager` with a cache-backed translation loader. On each call, it checks [FusionCache](https://github.com/ZiggyCreatures/FusionCache) (L1 memory → L2 distributed), falls back to your `ITranslationProvider`(s), and walks the culture chain (`da-DK` → `da` → source text).

On a cold boot with an empty cache, `Translation()` calls show your source text as fallback. The background fetch starts immediately, and translations appear once the provider responds. With an L2 cache (Redis, SQLite), cold boots are warm — translations are served from L2 while the provider refreshes in the background.

---

**See also:** [Examples](Examples.md) for API usage · [Extractor CLI](Extractor.md) for string extraction
