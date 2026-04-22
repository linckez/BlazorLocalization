[< Back to Analyzers](../Analyzers.md)

# BL0003 — Duplicate Translation Key

| Property | Value |
|----------|-------|
| Severity | Warning |
| Category | Correctness |
| Code fix | No |

All translations share one flat cache — `IStringLocalizer<T>` scoping has no effect. When the same key appears with different source texts, whichever is evaluated last wins, and the other silently disappears. The result is unpredictable text in production.

## Examples

### Don't do this — same key, different text

**Duplicate definitions:**

```csharp
// CommonTranslations.cs
public static readonly SimpleDefinition Save =
    DefineSimple("Common.Save", "Save");            // BL0003

// OtherTranslations.cs
public static readonly SimpleDefinition SaveBtn =
    DefineSimple("Common.Save", "Save button");     // BL0003 — same key, different text
```

**Conflicting inline calls:**

```csharp
// Home.razor
@Loc.Translation(key: "Home.Title", message: "Welcome")       // BL0003

// About.razor
@Loc.Translation(key: "Home.Title", message: "Hello there")   // BL0003 — same key, different text
```

**Definition + inline overlap:**

```csharp
// CommonTranslations.cs
public static readonly SimpleDefinition Save =
    DefineSimple("Common.Save", "Save");

// Home.razor — use Loc.Translation(CommonTranslations.Save) instead
@Loc.Translation(key: "Common.Save", message: "Save")         // BL0003
```

### Do this instead — one key, one source text

Define once, reference everywhere:

```csharp
// CommonTranslations.cs
public static readonly SimpleDefinition Save =
    DefineSimple("Common.Save", "Save");

// Home.razor
@Loc.Translation(CommonTranslations.Save)         // ✅

// About.razor
@Loc.Translation(CommonTranslations.Save)         // ✅
```

Two calls with the same key AND the same message are fine — that's reuse. But a reusable definition is cleaner.

## Configure

```ini
[*.cs]
dotnet_diagnostic.BL0003.severity = error    # or none to suppress
```

---

**See also:** [BL0001 — Empty Key](BL0001-empty-key.md) · [Extract Translation Definition](extract-translation-definition.md) · [Reusable Definitions](../Examples.md#reusable-definitions) · [Analyzers overview](../Analyzers.md)
