<div align="center">

![BlazorLocalization logo](src/icon.png)
	
# BlazorLocalization

</div>

<div align="center">

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/BlazorLocalization.Extensions.svg)](https://www.nuget.org/packages/BlazorLocalization.Extensions)

</div>

### Your users see text in their language. Always.

Inline translations · Pluggable providers · CLDR plurals · Any `IStringLocalizer` project

---

## ⭐ Quick Start

**1. Install:**

```bash
dotnet add package BlazorLocalization.Extensions
```

**2. Register in `Program.cs`:**

```csharp
// Replaces services.AddLocalization():
builder.Services.AddProviderBasedLocalization();
```

That's it — translations defined in your code work immediately. No provider, no config files.

**3. Use in your components:**

```razor
@inject IStringLocalizer<Home> Loc

<h1>@(Loc.Translation("Home.Title", "Welcome to our app")
    .For("da", "Velkommen til vores app")
    .For("de", "Willkommen in unserer App"))</h1>

<p>@Loc.Translation("Home.Greeting", "Hello, {Name}!", new { Name = user.Name })</p>

@* Plurals — different wording depending on quantity *@
<p>@(Loc.Translation("Cart.Items", howMany: cartCount, replaceWith: new { ItemCount = cartCount })
    .One("1 item in your cart")
    .Other("{ItemCount} items in your cart"))</p>

@* Enums — translated display names instead of raw member names *@
<p>@Loc.Display(FlightStatus.Delayed)</p>
```

If translations haven't loaded yet, your source text is shown — users never see blank strings or raw keys.

You still need ASP.NET Core's `UseRequestLocalization()` middleware for culture detection. See [Configuration](docs/Configuration.md) for the full setup.

**4. Add a translation provider when you're ready:**

Translation providers are pluggable and optional. Use them when you outgrow inline translations or want to connect a translation management platform.

```csharp
// Load from JSON files on disk (ships with Extensions — no extra package):
builder.Services.AddProviderBasedLocalization(builder.Configuration)
    .AddJsonFileTranslationProvider();

// — or fetch over-the-air from Crowdin CDN (separate package):
builder.Services.AddProviderBasedLocalization()
    .AddCrowdinTranslationProvider();
```

The provider always wins when it has a translation. Inline `.For()` translations serve as a starting point for translators and a runtime fallback.

See [Providers](docs/Configuration.md#translation-providers) for all available providers and their setup.

---

## 🎬 String Extraction

Already using `IStringLocalizer`? The Extractor scans your `.razor`, `.cs`, and `.resx` files and exports every translation string — no matter which localization backend you use.

```bash
dotnet tool install -g BlazorLocalization.Extractor

# Interactive wizard — run with no arguments
blazor-loc

# Or go direct
blazor-loc extract ./src -f po -o ./translations
```

<img src="docs/assets/blazor-loc.svg" alt="blazor-loc interactive wizard demo" width="750">

Upload the generated files to Crowdin, Lokalise, or any translation management system.
See [Extractor CLI](docs/Extractor.md) for recipes, CI integration, and export formats.

---

## ✨ Why BlazorLocalization?

Microsoft's [`IStringLocalizer`](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization?view=aspnetcore-10.0) is deeply embedded in ASP.NET Core — Blazor, MVC, Razor Pages, APIs. It works. But the default backend is `ResourceManager` with `.resx` files:

- Merge conflicts — `.resx` XML files conflict constantly across team branches
- No over-the-air updates — change a translation? Rebuild and redeploy
- No plural support — `IStringLocalizer` has no built-in plural category handling
- No distributed caching — translations live in flat files, not in Redis or a database

BlazorLocalization keeps `IStringLocalizer` as the interface (it's everywhere — why fight it?) but [replaces `AddLocalization()`](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization-extensibility?view=aspnetcore-10.0) and its `ResourceManager` / `.resx` backend entirely:

- **Over-the-air translations** — refresh from your provider without redeployment
- **Source text fallback** — if translations haven't loaded yet, users see your source text, never blank strings or keys
- **CLDR plural support** — plural categories, ordinals, gender/select. ICU concepts, C# ergonomics
- **Distributed caching** — L1 memory out of the box, optional L2 via any `IDistributedCache` (Redis, SQLite, etc.)
- **Pluggable providers** — stack translation sources with fallback chains. First non-null wins

Built on [Microsoft's `IStringLocalizer`](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization-extensibility?view=aspnetcore-10.0), [FusionCache](https://github.com/ZiggyCreatures/FusionCache), and [SmartFormat.NET](https://github.com/axuno/SmartFormat).

---

## Comparison

| Feature | `.resx` / ResourceManager | OrchardCore PO | **BlazorLocalization** |
|---------|:-------------------------:|:--------------:|:----------------------:|
| Over-the-air updates | No | No | Yes |
| Distributed cache (Redis, etc.) | No | No | Yes |
| Plural support | No | Yes | Yes — CLDR 46, ordinals, select |
| Source text as fallback | No | Key = text* | Yes — separate key + source text |
| Named placeholders | No | No | Yes — via SmartFormat |
| External provider support | No | No | Yes — pluggable |
| Merge-conflict-free | No — XML | No — PO files | Yes — with OTA providers. File-based providers are opt-in |
| Automated string extraction | Manual | Manual | Roslyn-based CLI |
| Standard `IStringLocalizer` | Yes | Yes | Yes |
| Battle-tested | Yes — 20+ years | Yes | New |

\* OrchardCore uses the `IStringLocalizer` indexer key as both the lookup key and the source text. Updating the original text creates a new entry — existing translations are orphaned.

---

## Packages

| Package | Version | Install |
|---------|:-------:|--------:|
| [**BlazorLocalization.Extensions**](https://www.nuget.org/packages/BlazorLocalization.Extensions) <br/> Runtime library — cache-backed `IStringLocalizer` with plural support and pluggable translation providers | [![NuGet](https://img.shields.io/nuget/v/BlazorLocalization.Extensions.svg)](https://www.nuget.org/packages/BlazorLocalization.Extensions) | `dotnet add package BlazorLocalization.Extensions` |
| [**BlazorLocalization.Extractor**](https://www.nuget.org/packages/BlazorLocalization.Extractor) <br/> CLI tool (`blazor-loc`) — Roslyn-based scanner that extracts source strings from `.razor`, `.cs`, and `.resx` files | [![NuGet](https://img.shields.io/nuget/v/BlazorLocalization.Extractor.svg)](https://www.nuget.org/packages/BlazorLocalization.Extractor) | `dotnet tool install -g BlazorLocalization.Extractor` |

Translation providers:

| Package | Version | Install |
|---------|:-------:|--------:|
| [**BlazorLocalization.TranslationProvider.Crowdin**](https://www.nuget.org/packages/BlazorLocalization.TranslationProvider.Crowdin) <br/> Fetch translations from [Crowdin](https://crowdin.com/) OTA CDN | [![NuGet](https://img.shields.io/nuget/v/BlazorLocalization.TranslationProvider.Crowdin.svg)](https://www.nuget.org/packages/BlazorLocalization.TranslationProvider.Crowdin) | `dotnet add package BlazorLocalization.TranslationProvider.Crowdin` |
| **JsonFile** <br/> Load translations from flat JSON files on disk | Ships with Extensions | — |
| **PoFile** <br/> Load translations from GNU Gettext PO files | Ships with Extensions | — |

---

## Documentation

| Topic | Description |
|-------|-------------|
| [Examples](docs/Examples.md) | Translation() usage — simple, placeholders, plurals, ordinals, select, inline translations |
| [Extractor CLI](docs/Extractor.md) | Install, interactive wizard, common recipes, CI integration, export formats |
| [Configuration](docs/Configuration.md) | Cache settings, `appsettings.json` binding, multiple providers, code-only config |
| [Crowdin Provider](docs/Providers/Crowdin.md) | Crowdin OTA setup — distribution hash, export formats, error handling |
| [JSON File Provider](docs/Providers/JsonFile.md) | Load translations from flat JSON files on disk |
| [PO File Provider](docs/Providers/PoFile.md) | Load translations from GNU gettext PO files |
| [Samples](samples/) | Runnable Blazor Server + Minimal API projects with full setup |

---

## FAQ

**Does this only work with Blazor?**
No. It works with anything that uses `IStringLocalizer` — Blazor Server, Blazor WASM, MVC, Razor Pages, Web APIs, minimal APIs. "Blazor" is in the name because that's where most developers first hit the `.resx` wall.

**Do I need a translation provider?**
No. `Translation("key", "source text")` works on its own for your default language. Add `.For()` when you need additional languages inline. When you're ready to connect a translation management platform or load from files, add a provider — Extensions ships with JSON file and PO file providers, and Crowdin is available as a separate package.

**Is this production-ready?**
Born from real frustration with `.resx` in a multilingual product. Actively maintained. If you find it useful, give it a ⭐.

---

## Contributing

Contributions welcome! Built a translation provider for a platform not yet covered? Consider submitting it as a package.

## License

[MIT](LICENSE)
