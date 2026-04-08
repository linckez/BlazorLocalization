[< Back to README](../README.md)

# Extractor CLI

`blazor-loc` is a Roslyn-based CLI that understands both BlazorLocalization's `Translation()` API and Microsoft's built-in `IStringLocalizer["key"]` + `.resx`. No code changes or adoption required — point it at any `IStringLocalizer` project and go.

- **`inspect`** — Translation health audit. See every translation key, where it's used, what's missing, what conflicts, and how complete each locale is.
- **`extract`** — Scan your codebase and export every source string. Run it in CI on every merge to keep your translation platform in sync.

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

## Extract

Without tooling, keeping translations in sync means manually copying strings between your code and your translation platform — every key, every language, every time something changes. `extract` scans your entire codebase and exports every source string in one command. Run it locally or in CI on every merge.

Extract to Crowdin i18next JSON (the default format):

```bash
blazor-loc extract ./src -o ./translations
```

Extract to GNU Gettext PO (includes source file references for translator context):

```bash
blazor-loc extract ./src -f po -o ./translations
```

Extract to stdout (pipeable — outputs in the requested format):

```bash
blazor-loc extract ./src -f po
```

Export translations for a single locale to stdout:

```bash
blazor-loc extract ./src -f po -l da
```

Scan a specific `.csproj` file instead of a directory:

```bash
blazor-loc extract ./src/MyApp/MyApp.csproj -o ./translations
```

Scan multiple paths (directories or `.csproj` files):

```bash
blazor-loc extract ./src/WebApp ./src/Shared -o ./translations
```

When output is a directory, per-locale translations are automatically written as separate files (e.g. `Project.da.i18next.json`):

```bash
blazor-loc extract ./src -f i18next -o ./translations
```

To suppress per-locale files and export source strings only (useful when uploading to Crowdin — translators should provide translations, not your inline source texts):

```bash
blazor-loc extract ./src -f i18next -o ./translations --source-only
```

Narrow to specific locales:

```bash
blazor-loc extract ./src -f i18next -o ./translations -l da -l es-MX
```

## Inspect

Point `inspect` at your project and get a translation health audit — the full picture across every file, locale, and pattern in seconds.

```bash
blazor-loc inspect ./src
```

### What you see

**Translation entries** — one row per unique key, showing where it's used in your code, its source text, which form it takes (simple, plural, select...), and which locales have a translation. Spot a key that's missing `de` when every other row has it.

**Conflicts** — same key used with different source texts in different places. Almost always a bug. The table shows exactly which files disagree and what each one says.

**Extraction warnings** — the handful of calls the scanner couldn't confidently resolve. Things like `Loc[someVar ? Loc["..."] : Loc["..."]]` or mangled expressions that somehow made it through code review. By default, only these problem cases surface — not the hundreds of healthy calls.

**Locale coverage** — per-language summary: how many keys each locale has, what percentage that covers, and any keys that only exist in one locale but not the source. At a glance you see that `es-MX` is at 97.6% but `vi` is at 85.7%.

**Cross-reference summary** — one line bridging code and data: how many keys resolved, how many are missing, how many `.resx` entries have no matching code reference.

### Options

See full key/value tables per language (instead of the default summary):

```bash
blazor-loc inspect ./src --show-resx-locales
```

See every line of code where a translation was found (including all healthy ones):

```bash
blazor-loc inspect ./src --show-extracted-calls
```

Output as JSON (auto-enabled when stdout is piped):

```bash
blazor-loc inspect ./src --json
blazor-loc inspect ./src | jq '.translationEntries[] | select(.status == "Missing")'
```

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

**See also:** [Examples](Examples.md) for `Translation()` usage · [Configuration](Configuration.md) for setup