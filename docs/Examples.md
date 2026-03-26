[< Back to README](../README.md)

# Examples

Every method shown here works with any translation provider — or no provider at all. Inline translations and source-text fallback work out of the box.

All examples assume an injected localizer:

```razor
@inject IStringLocalizer<Home> Loc
```

**On this page:** [Simple](#simple) · [Placeholders](#placeholders) · [Plurals](#plurals) · [Ordinals](#ordinals) · [Exact Counts](#exact-counts) · [Select](#select) · [Select + Plural](#select--plural) · [Inline Translations](#inline-translations) · [Enums](#enums) · [Standard IStringLocalizer](#standard-istringlocalizer)

---

## Simple

```razor
<h1>@Loc.Translation(key: "Home.Title", message: "Welcome to our app")</h1>
```

Your source text is always the fallback — users never see blank strings or raw keys.

## Placeholders

Named placeholders are resolved by [SmartFormat](https://github.com/axuno/SmartFormat). Pass any object — properties become placeholders.

```razor
<p>@Loc.Translation(key: "Home.Greeting", message: "Hello, {Name}!", replaceWith: new { Name = user.Name })</p>

<p>@Loc.Translation(key: "Home.Stats", message: "Showing {Count} of {Total} items", replaceWith: new { Count = 5, Total = 100 })</p>
```

## Plurals

Chain `.One()`, `.Other()`, and any other [CLDR plural category](https://www.unicode.org/cldr/charts/46/supplemental/language_plural_rules.html) your target languages need. The correct form is chosen automatically based on the current culture.

```razor
<p>@(Loc.Translation(key: "Cart.Items", howMany: cartCount, replaceWith: new { ItemCount = cartCount })
    .One(message: "1 item in your cart")
    .Other(message: "{ItemCount} items in your cart"))</p>
```

`howMany` determines which form to pick. Pass it in `replaceWith` too if the message needs to display it.

**Most languages only need `.One()` and `.Other()`.** Some languages need additional categories — Arabic uses all six CLDR categories:

```razor
<p>@(Loc.Translation(key: "Items", howMany: itemCount, replaceWith: new { ItemCount = itemCount })
    .Zero(message: "لا عناصر")
    .One(message: "عنصر واحد")
    .Two(message: "عنصران")
    .Few(message: "{ItemCount} عناصر")
    .Many(message: "{ItemCount} عنصرًا")
    .Other(message: "{ItemCount} عنصر"))</p>
```

**Corresponding translation files:**

```json
{
  "Cart.Items_one": "1 vare i din kurv",
  "Cart.Items_other": "{ItemCount} varer i din kurv"
}
```

## Ordinals

Ordinal ranking (1st, 2nd, 3rd, …). Pass `ordinal: true` to use ordinal rules instead of cardinal.

```razor
<p>@(Loc.Translation(key: "Race.Place", howMany: position, ordinal: true, replaceWith: new { Position = position })
    .One(message: "{Position}st place")
    .Two(message: "{Position}nd place")
    .Few(message: "{Position}rd place")
    .Other(message: "{Position}th place"))</p>
```

The categories above are for English. Other languages define their own ordinal rules in CLDR — for example, Swedish uses only `Other` for all ordinals. The correct form is chosen automatically based on the current culture.

## Exact Counts

Override a specific count with a precise message. Checked before CLDR category rules.

```razor
<p>@(Loc.Translation(key: "Cart.Items", howMany: cartCount)
    .Exactly(value: 0, message: "Your cart is empty")
    .One(message: "1 item in your cart")
    .Other(message: "Several items in your cart"))</p>
```

## Select

Branch on a categorical enum value — gender, role, formality, or any domain concept.

```razor
<p>@(Loc.Translation(key: "Greeting", select: userTier)
    .When(select: Tier.Premium, message: "Welcome back, VIP!")
    .Otherwise(message: "Welcome!"))</p>
```

## Select + Plural

Combine categorical branching with plural forms. Here a premium user gets a different message than a free user, and both vary by quantity:

```razor
<p>@(Loc.Translation(key: "Cart.Summary", select: userTier, howMany: itemCount, replaceWith: new { ItemCount = itemCount })
    .When(select: Tier.Premium)
    .One(message: "1 item in your cart — free express shipping!")
    .Other(message: "{ItemCount} items in your cart — free express shipping!")
    .Otherwise()
    .One(message: "1 item in your cart")
    .Other(message: "{ItemCount} items in your cart"))</p>
```

With `itemCount = 3` and `Tier.Premium`, this renders: **"3 items in your cart — free express shipping!"**.

## Inline Translations

Already know the translation? Write it where you have the context — no need to switch to Crowdin and back:

```razor
<h1>@(Loc.Translation(key: "Home.Title", message: "Welcome!")
    .For(locale: "da", message: "Velkommen!")
    .For(locale: "es", message: "¡Bienvenido!"))</h1>
```

Works on all builder types. Here's a plural with inline Danish translations:

```razor
<p>@(Loc.Translation(key: "Cart.Items", howMany: itemCount)
    .One(message: "1 item").Other(message: "Several items")
    .For(locale: "da")
    .One(message: "1 vare").Other(message: "Flere varer"))</p>
```

The translation provider always wins when a translation exists. Inline per-locale source texts serve as a starting point for translators and a fallback when the provider hasn't delivered yet.

## Enums

Mark enum members with `[Translation]`, resolve with `Display()`.

```csharp
public enum FlightStatus
{
    [Translation("Delayed")]
    [Translation("Forsinket", Locale = "da")]
    [Translation("Retrasado", Locale = "es-MX")]
    Delayed,

    [Translation("On time")]
    OnTime
}
```

```razor
<p>@Loc.Display(FlightStatus.Delayed)</p>
```

The auto-generated key is `Enum.{TypeName}_{MemberName}` (e.g. `Enum.FlightStatus_Delayed`).

Same fallback chain as `Translation()`: translation provider → inline per-locale source text → source text → member name.

Override the auto-generated key with `Key`:

```csharp
[Translation("Arrived a bit late", Key = "Flight.Late")]
ArrivedABitLate
```

---

## Standard IStringLocalizer

This is a standard `IStringLocalizer` — the built-in indexer and `GetString()` work as expected:

```razor
<h1>@Loc["Home.Title"]</h1>
<p>@Loc.GetString("Home.Title")</p>
```

With the indexer, the raw key is returned when translations haven't loaded (`"Home.Title"`). Prefer `Translation()` — it gives users readable source text instead of a cryptic key.

---

**Next:** [Configuration](Configuration.md) for cache settings, culture detection, and provider setup.
