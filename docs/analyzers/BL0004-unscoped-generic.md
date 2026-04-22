[< Back to Analyzers](../Analyzers.md)

# BL0004 — Redundant Type Parameter on IStringLocalizer

| Property | Value |
|----------|-------|
| Severity | Info |
| Category | Awareness |
| Code fix | Yes — removes `<T>` |

`ProviderBasedStringLocalizerFactory` ignores the type parameter entirely — `IStringLocalizer<Home>` and `IStringLocalizer<About>` return the exact same translations for the same key. The `<T>` gives a false impression of scoping that doesn't exist.

This rule only fires when your project uses BlazorLocalization. In a stock `.resx` project, `<T>` determines which resource file to load — there it's meaningful.

## Examples

### Don't do this

```csharp
public class MyPage(IStringLocalizer<MyPage> localizer) { }   // BL0004

[Inject]
public IStringLocalizer<MyPage> Loc { get; set; } = default!;  // BL0004
```

```razor
@inject IStringLocalizer<MyPage> Loc                            @* BL0004 *@
```

### Do this instead

```csharp
public class MyPage(IStringLocalizer localizer) { }

[Inject]
public IStringLocalizer Loc { get; set; } = default!;
```

```razor
@inject IStringLocalizer Loc
```

## Code fix

| Before | After |
|--------|-------|
| `IStringLocalizer<MyPage>` | `IStringLocalizer` |

## Configure

```ini
[*.cs]
dotnet_diagnostic.BL0004.severity = warning  # promote from Info to Warning
dotnet_diagnostic.BL0004.severity = none     # suppress if your team prefers the generic form
```

---

**See also:** [BL0002 — Use Translation() API](BL0002-use-translation-api.md) · [Configuration](../Configuration.md) · [Analyzers overview](../Analyzers.md)
