[< Back to Analyzers](../Analyzers.md)

# BL0002 — Use Translation() API

| Property | Value |
|----------|-------|
| Severity | Info |
| Category | Migration |
| Code fix | Yes — wraps in `Translation()` |

`Loc["key"]` and `GetString("key")` return the raw key when a translation is missing — users see cryptic strings like `"Home.Title"`. `Translation()` returns your source text as the fallback instead, and adds named placeholders, plural support, and inline translations.

## Examples

### Don't do this

```csharp
Loc["Home.Title"]                           // BL0002
Loc["Home.Title", arg1, arg2]               // BL0002
Loc.GetString("Home.Title")                 // BL0002
```

### Do this instead

```csharp
Loc.Translation(key: "Home.Title", message: "Welcome to our app")
```

Users never see blank strings or raw keys — your source text is always the fallback.

## Code fix

| Before | After |
|--------|-------|
| `Loc["Home.Title"]` | `Loc.Translation(key: "Home.Title", message: "")` |
| `Loc.GetString("Home.Title")` | `Loc.Translation(key: "Home.Title", message: "")` |

Fill in `message` with the actual source text. For calls with positional args, the fix adds a `// TODO` for migrating `{0}` to named placeholders like `{Name}`.

## Configure

```ini
[*.cs]
dotnet_diagnostic.BL0002.severity = warning  # promote from Info to Warning
dotnet_diagnostic.BL0002.severity = none     # suppress if you prefer the indexer style
```

---

**See also:** [BL0001 — Empty Key](BL0001-empty-key.md) · [BL0004 — Redundant `<T>`](BL0004-unscoped-generic.md) · [Analyzers overview](../Analyzers.md)
