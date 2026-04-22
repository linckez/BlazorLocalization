[< Back to Analyzers](../Analyzers.md)

# BL0001 — Empty Translation Key

| Property | Value |
|----------|-------|
| Severity | Warning |
| Category | Correctness |
| Code fix | No |

An empty key will never match a real translation. Your users see the source text every time — not because of a missing translation, but because the key is blank. It's a bug.

## Examples

### Don't do this

```csharp
Loc.Translation(key: "", message: "Welcome")       // BL0001
Loc[""]                                             // BL0001
Loc.GetString("")                                   // BL0001
```

### Do this instead

```csharp
Loc.Translation(key: "Home.Title", message: "Welcome")
Loc["Home.Title"]
Loc.GetString("Home.Title")
```

## Configure

```ini
[*.cs]
dotnet_diagnostic.BL0001.severity = error    # or none to suppress
```

---

**See also:** [BL0002 — Use Translation() API](BL0002-use-translation-api.md) · [BL0003 — Duplicate Key](BL0003-duplicate-key.md) · [Analyzers overview](../Analyzers.md)
