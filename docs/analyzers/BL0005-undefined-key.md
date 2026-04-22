[< Back to Analyzers](../Analyzers.md)

# BL0005 — Undefined Translation Key

| Property | Value |
|----------|-------|
| Severity | Warning |
| Category | Correctness |
| Code fix | No |

A key-only `Translation("key")` call uses a key that nobody defined. Without a definition, there's no source text to fall back on — your users see whatever the translation provider returns, or nothing.

## Examples

### Don't do this

```csharp
// No definition for "Orphan.Key" anywhere in the project
@Loc.Translation("Orphan.Key")          // ⚠️ BL0005

@Loc.Translation("Missing.Count", 5)    // ⚠️ BL0005 (key-only plural)
```

### Do this instead — add a definition

```csharp
// CommonTranslations.cs
public static readonly SimpleDefinition Save =
    TranslationDefinitions.DefineSimple("Common.Save", "Save");

// Home.razor — key-only reference, satisfied by the definition above
@Loc.Translation("Common.Save")         // ✅ no BL0005
```

### Or define inline — the message parameter counts

```csharp
// Page A
@Loc.Translation("Home.Title", "Welcome")  // defines the key (has message)

// Page B
@Loc.Translation("Home.Title")             // ✅ no BL0005
```

## What counts as a definition

| Call | Defines? |
|------|----------|
| `DefineSimple("key", "msg")` | Yes |
| `DefinePlural("key")` | Yes |
| `DefineSelect<T>("key")` | Yes |
| `DefineSelectPlural<T>("key")` | Yes |
| `Translation("key", "msg")` | Yes — has `message` parameter |
| `Translation("key")` | No — key-only reference |
| `Translation("key", howMany)` | No — key-only reference |
| `Loc["key"]` / `GetString("key")` | Not tracked — see BL0002 |

## Configure

```csharp
#pragma warning disable BL0005
@Loc.Translation("RuntimeOnly.Key")
#pragma warning restore BL0005
```

Or in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.BL0005.severity = none
```

---

**See also:** [BL0003 — Duplicate Key](BL0003-duplicate-key.md) · [Reusable Definitions](../Examples.md#reusable-definitions) · [Analyzers overview](../Analyzers.md)
