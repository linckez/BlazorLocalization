[< Back to Analyzers](../Analyzers.md)

# BL0006 — Translation File Conflict

| Property | Value |
|----------|-------|
| Severity | Warning |
| Category | Correctness |
| Code fix | No |

Two or more translation files define different values for the same key and culture. Your code fixes and refactorings can't enrich `Translation()` calls with data from the files until you reconcile them.

## Examples

### Don't do this

```xml
<!-- Resources/Home.resx -->
<data name="Home.Title"><value>Welcome to our app</value></data>

<!-- Resources/Common.resx -->
<data name="Home.Title"><value>Welcome!</value></data>
```

```
⚠️ BL0006: Key 'Home.Title' has conflicting source text across translation files:
    "Welcome to our app" (Home.resx) vs "Welcome!" (Common.resx)
```

### Do this instead — use one value per key per culture

```xml
<!-- Resources/Home.resx — the only file defining Home.Title -->
<data name="Home.Title"><value>Welcome to our app</value></data>
```

Once the conflict is gone, the "Use Translation() — with source text" code fix and the "Enrich with translations" refactoring light up automatically.

### Culture-level conflicts work the same way

```xml
<!-- Resources/Home.da.resx -->
<data name="Home.Title"><value>Velkommen</value></data>

<!-- Resources/Common.da.resx -->
<data name="Home.Title"><value>Hej!</value></data>
```

```
⚠️ BL0006: Key 'Home.Title' has conflicting translation for culture 'da' across translation files:
    "Velkommen" (Home.da.resx) vs "Hej!" (Common.da.resx)
```

## What counts as a conflict

| Scenario | Conflict? |
|----------|-----------|
| Same key, same culture, different values | Yes — BL0006 fires |
| Same key, same culture, same value | No — merged silently |
| Same key, different cultures | No — each culture keeps its value |

## Configure

```ini
[*.cs]
dotnet_diagnostic.BL0006.severity = none
```

---

**See also:** [BL0003 — Duplicate Key](BL0003-duplicate-key.md) · [Analyzers overview](../Analyzers.md)
