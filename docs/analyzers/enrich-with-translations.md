[< Back to Analyzers](../Analyzers.md)

# Enrich with Translations

| Property | Value |
|----------|-------|
| Type | Refactoring (hammer / screwdriver menu) |
| Trigger | Cursor on a `Translation("key", "message")` call |

Appends `.For("culture", "text")` calls to an existing `Translation()` invocation using data from your project's `.resx` files. One click fills in every culture your resource files define.

## When It Appears

The refactoring is offered when all three conditions are met:

1. The cursor is on a `Translation("key", "message")` call (or inside its `.For()` chain)
2. The key exists in your `.resx` files without conflicts (no BL0006)
3. At least one culture-specific `.resx` file defines a translation for that key

## Example

### Before

```csharp
@Loc.Translation("Home.Title", "Welcome to our app")
```

### After — "Enrich with translations from resource files"

```csharp
@Loc.Translation("Home.Title", "Welcome to our app")
    .For("da", "Velkommen til vores app")
    .For("es", "Bienvenido a nuestra aplicación")
```

## Existing `.For()` Calls Are Preserved

If you already have `.For("da", "Velkommen")` on the expression, the refactoring skips `da` and only adds missing cultures.

## Not Offered When

- The key isn't in any `.resx` file
- The key has conflicting values across files (fix BL0006 first)
- No culture-specific `.resx` files define translations for the key (nothing to add)

---

**See also:** [BL0006 — Translation File Conflict](BL0006-translation-file-conflict.md) · [Extract Translation Definition](extract-translation-definition.md) · [Analyzers overview](../Analyzers.md)
