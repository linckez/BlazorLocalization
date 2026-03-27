[< Back to README](../README.md)

# Extractor CLI

`blazor-loc` scans your `.razor`, `.cs`, and `.resx` files and exports source strings to translation files. Upload the output to Crowdin, Lokalise, or any translation management system.

It works with any `IStringLocalizer` codebase — regardless of which localization backend you use.

## Install

```bash
dotnet tool install -g BlazorLocalization.Extractor
```

To upgrade to the latest version:

```bash
dotnet tool update -g BlazorLocalization.Extractor
```

## Interactive Wizard

Run with no arguments to launch the interactive wizard. It walks you through project selection, export format, and output destination:

![blazor-loc interactive wizard demo](https://raw.githubusercontent.com/linckez/BlazorLocalization/main/docs/assets/blazor-loc.svg)

```bash
blazor-loc
```

## Common Recipes

Extract to Crowdin i18next JSON (the default format):

```bash
blazor-loc extract ./src -o ./translations
```

Extract to GNU Gettext PO (includes source file references for translator context):

```bash
blazor-loc extract ./src -f po -o ./translations
```

Extract to stdout (pipeable — useful for CI):

```bash
blazor-loc extract ./src -f po
```

When output is a directory, inline `.For()` per-locale source texts are automatically written as separate files (e.g. `Project.da.i18next.json`):

```bash
blazor-loc extract ./src -f i18next -o ./translations
```

To suppress per-locale files and export source strings only (useful when uploading to Crowdin — translators should provide translations, not your inline `.For()` text):

```bash
blazor-loc extract ./src -f i18next -o ./translations --source-only
```

Narrow to specific locales:

```bash
blazor-loc extract ./src -f i18next -o ./translations -l da -l es-MX
```

Debug what the scanner detects (raw calls + merged entries):

```bash
blazor-loc inspect ./src
```

`inspect` dumps every detected `IStringLocalizer` call with its key, source text, plural forms, and file location — useful for verifying the scanner found what you expected.

## Export Formats

| Format | Flag | Best for |
|--------|------|----------|
| **Crowdin i18next JSON** | `-f i18next` | Uploading to Crowdin or any i18next-compatible platform. Flat key/value, lightweight. |
| **GNU Gettext PO** | `-f po` | Maximum translator context — includes `#:` source references and `#.` comments. |
| **Generic JSON** | `-f json` | Debugging and custom tooling. Full-fidelity export with all metadata. |

New formats are easy to add — the exporter is a simple interface. PRs welcome.

## GitHub Actions

Automate extraction in CI so translation files stay in sync with your code:

```yaml
- name: Install blazor-loc
  run: dotnet tool install -g BlazorLocalization.Extractor

- name: Extract source strings
  run: blazor-loc extract ./src -f i18next -o ./translations

- name: Upload translations
  uses: crowdin/github-action@v2
  with:
    upload_sources: true
    source: translations/*.i18next.json
```

## Duplicate Key Detection

When the same key appears with different source texts, the extractor flags it as a conflict:

```bash
# Fail the build on duplicate keys (useful in CI)
blazor-loc extract ./src --exit-on-duplicate-key

# Keep the first-seen source text (default)
blazor-loc extract ./src --on-duplicate-key first

# Skip conflicting keys entirely
blazor-loc extract ./src --on-duplicate-key skip
```

## Full CLI Help

For all available flags and options:

```bash
blazor-loc extract --help
blazor-loc inspect --help
```

---

**See also:** [Examples](Examples.md) for `Translation()` API usage · [Configuration](Configuration.md) for runtime setup