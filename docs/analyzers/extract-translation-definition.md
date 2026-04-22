[< Back to Analyzers](../Analyzers.md)

# Extract Translation Definition

| Property | Value |
|----------|-------|
| Type | Refactoring (screwdriver menu) |
| Trigger | Cursor on any `Translation("key", "message")` call |

When the same key + message appears in multiple files, you're duplicating source text. This refactoring extracts it into a shared definition — one place to update, many places to use.

## Before

```razor
@Loc.Translation(key: "Common.Save", message: "Save changes")
```

## After

Two things happen in one action:

**1. A static field is created** in the same class:

```csharp
static readonly SimpleDefinition CommonSave =
    TranslationDefinitions.DefineSimple("Common.Save", "Save changes");
```

**2. The call site switches to the definition:**

```razor
@Loc.Translation(CommonSave)
```

The field name is derived from the key — `"Common.Save"` becomes `CommonSave`. If that name already exists, a suffix is added.

## What It Preserves

- `replaceWith:` arguments carry over to the new call site
- The key and message text stay identical — no change in behavior
- A `using` directive for `TranslationDefinitions` is added if missing
- The rename overlay activates on the field name so you can adjust it immediately

## When to Use It

You have the same `Translation("key", "message")` in two or more files. Extract it once, reference it everywhere. BL0003 stops firing, and translators see one entry instead of duplicates.

## Limitations

- Only works on simple `Translation(key, message)` calls — not plurals, select, or key-only overloads
- The field is created in the current class. Move it to a shared definitions class yourself if needed.

---

**See also:** [BL0003 — Duplicate Key](BL0003-duplicate-key.md) · [Reusable Definitions](../Examples.md#reusable-definitions) · [Analyzers overview](../Analyzers.md)
